using System.IO;
using System.ServiceProcess;
using System.Diagnostics;
using System.Collections.Generic;
using System;
using System.Threading;
using Toolkit;

namespace CustomWatchdog {

   public partial class CustomBatchWatchdog : ServiceBase {

      const int sleepTime = 500;    // time between consequitive checks
      const int bufferTime = 500;   // time to let whole suite started
      string healingBatch = "";
      const string fileName = "watchprocs.cfg";
      const string elogSource = "Custom Batch Watchdog";
      List<string> procNames = new List<string>();

      private void InitEventLog () {
         if (!EventLog.SourceExists(elogSource))
            EventLog.CreateEventSource(elogSource, "Application");
      }

      private void PrintWarning (string evt)
         { EventLog.WriteEntry(elogSource, evt, EventLogEntryType.Warning, 0x01); }
      private void PrintError (string evt)
         { EventLog.WriteEntry(elogSource, evt, EventLogEntryType.Error, 0x02); }
      private void PrintInfo (string evt)
         { EventLog.WriteEntry(elogSource, evt, EventLogEntryType.Information, 0x03); }

      private void LoadConfigFromFile () {
         try {
            using (StreamReader file = new StreamReader(fileName)) {
               string line;
               healingBatch = file.ReadLine();
               while ((line = file.ReadLine()) != null) {
                  procNames.Add(line);
               }
               file.Close();
            }
         } catch (IOException) {
            PrintError ("Problem reading config file " + fileName);
            throw new Exception("Problem reading config file " + fileName);
         }
      }

      private void Recover () {
         //System.Diagnostics.Process.Start(healingBatch);
         PrintWarning("Starting recovery procedure...");
         ApplicationLoader.PROCESS_INFORMATION procInfo;
         ApplicationLoader.StartProcessAndBypassUAC(healingBatch, out procInfo);
         PrintWarning("Recovery procedure has finished.");
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
               PrintWarning("Couldn't find the process " + procName + ".");
               return false;
            }
         }
         return true;
      }

      private void RunForever () {
         while (true) {
            if (!Check()) {
               Recover();
               PrintWarning("Waiting for the suite to recover...");
               while (!Check()) {
                  Thread.Sleep(bufferTime);
               }
               PrintWarning("Suite is now considered recovered!");
            } else {
               Thread.Sleep(sleepTime);
            }
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
