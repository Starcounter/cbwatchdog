using System;
using System.IO;
using System.ServiceProcess;
using System.Diagnostics;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using System.Collections;
using System.Threading;
using Toolkit;

namespace CustomWatchdog {

   public partial class CustomBatchWatchdog : ServiceBase {

      // defaults (can be overriden from a config file)
      string recoveryBatch = "cbwatchdog.bat";
      int healthCheckInterval = 500;
      int recoveryPauseInterval = 500;
      int criticalCounts = 10;
      bool elevatedModeRecovery = false;
      List<string> procNames = new List<string>();
      // local constants (not configurable)
      const string configFileName = "cbwatchdog.json";
      const string eventLogSource = "Custom Batch Watchdog";

      // Windows event log handling
      private void InitEventLog () {
         if (!EventLog.SourceExists(eventLogSource))
            EventLog.CreateEventSource(eventLogSource, "Application");
      }
      private void PrintWarning (string evt)
         { EventLog.WriteEntry(eventLogSource, evt, EventLogEntryType.Warning, 0x01); }
      private void PrintError (string evt)
         { EventLog.WriteEntry(eventLogSource, evt, EventLogEntryType.Error, 0x02); }
      private void PrintInfo (string evt)
         { EventLog.WriteEntry(eventLogSource, evt, EventLogEntryType.Information, 0x03); }

      private void LoadConfigFromFile () {
         try {
            JavaScriptSerializer ser = new JavaScriptSerializer();
            var dict = ser.Deserialize<Dictionary<string, object>>(File.ReadAllText(configFileName));
            if (!dict.ContainsKey ("processes"))
               throw new Exception("No processes are given to watch in a config file");
            if (dict.ContainsKey("recoveryBatch"))
               recoveryBatch = (string) dict["recoveryBatch"];
            if (dict.ContainsKey("healthCheckInterval"))
               healthCheckInterval = int.Parse((string) dict["healthCheckInterval"]);
            if (dict.ContainsKey("recoveryPauseInterval"))
               recoveryPauseInterval = int.Parse((string)dict["recoveryPauseInterval"]);
            if (dict.ContainsKey("criticalCounts"))
               criticalCounts = int.Parse((string)dict["criticalCounts"]);
            if (dict.ContainsKey("elevatedModeRecovery"))
               elevatedModeRecovery = bool.Parse((string)dict["elevatedModeRecovery"]);
            ArrayList procsList = (ArrayList)dict["processes"];
            foreach (var proc in procsList)
               procNames.Add((string)proc);
            PrintInfo("Watchdog will be started with:\n" +
               "   recoveryBatch : " + recoveryBatch.ToString() + "\n" +
               "   healthCheckInterval : " + healthCheckInterval.ToString() + "\n" +
               "   recoveryPauseInterval : " + recoveryPauseInterval.ToString() + "\n" +
               "   elevatedModeRecovery : " + elevatedModeRecovery.ToString() + "\n" +
               "   processes : " + string.Join("; ", procNames));
         } catch (IOException) {
            throw new Exception("Problem reading config file " + configFileName);
         }
      }

      private void Recover () {
         PrintInfo("Watchdog starts recovery procedure...");
         if (elevatedModeRecovery) {
            ApplicationLoader.PROCESS_INFORMATION procInfo;
            ApplicationLoader.StartProcessAndBypassUAC(recoveryBatch, out procInfo);
         } else {
            System.Diagnostics.Process.Start(recoveryBatch);
         }
         PrintInfo("Watchdog's recovery procedure has finished.");
      }
      
      private bool Check () {
         Process[] processlist = Process.GetProcesses();
         foreach (string procName in procNames) {
            bool found = false;
            foreach (Process theprocess in processlist) {
               if (theprocess.ProcessName.Equals(procName)) {
                  found = true;
                  break;
               }
            }
            if (!found) {
               PrintWarning("Watchdog couldn't find the process " + procName + ".");
               return false;
            }
         }
         return true;
      }

      private void RunForever () {
         while (true) {
            label:
            if (!Check()) {
               Recover();
               PrintInfo("Watchdog will now wait for the suite to recover.");
               int cntr = 0;
               do {
                  PrintInfo("Watchdog's attempt #" + (cntr+1).ToString() + " to wait for recover...");
                  Thread.Sleep(recoveryPauseInterval);
                  cntr++;
                  if (cntr == criticalCounts) {
                     PrintInfo("Waited critical number of times for the suite to recover, now will repeat recovery.");
                     goto label;
                  }
               } while (!Check());
               PrintInfo("Watchdog's suite is now considered recovered!");
            }
            Thread.Sleep(healthCheckInterval);
         }
      }

      protected override void OnStart (string[] args) {
         PrintInfo("Custom batch watchdog has been started.");
         LoadConfigFromFile();
         ThreadPool.QueueUserWorkItem(o => { RunForever(); });
      }

      public CustomBatchWatchdog () { InitializeComponent(); }
      protected override void OnStop () {
         PrintInfo("Custom batch watchdog has been signalled to stop.");
      }
   }
}
