using System;
using System.IO;
using System.ServiceProcess;
using System.Diagnostics;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using System.Collections;
using System.Threading;
using Toolkit;
using System.Linq;

namespace CustomWatchdog
{
    public partial class CustomBatchWatchdog : ServiceBase
    {
        // defaults (can be overriden from a config file)
        int healthCheckInterval = 500;
        int recoveryPauseInterval = 500;
        int criticalCounts = 10;
        bool elevatedModeRecovery = false;
        List<RecoveryItem> recoveryItems = new List<RecoveryItem>();

        // local constants (not configurable)
        const string configFileName = "cbwatchdog.json";
        const string eventLogSource = "Custom Batch Watchdog";

        // Windows event log handling
        private void InitEventLog()
        {
            if (!EventLog.SourceExists(eventLogSource))
                EventLog.CreateEventSource(eventLogSource, "Application");
        }
        private void PrintWarning(string evt)
        { EventLog.WriteEntry(eventLogSource, evt, EventLogEntryType.Warning, 0x01); }
        private void PrintError(string evt)
        { EventLog.WriteEntry(eventLogSource, evt, EventLogEntryType.Error, 0x02); }
        private void PrintInfo(string evt)
        { EventLog.WriteEntry(eventLogSource, evt, EventLogEntryType.Information, 0x03); }

        private void LoadConfigFromFile()
        {
            try
            {
                JavaScriptSerializer ser = new JavaScriptSerializer();
                var dict = ser.Deserialize<Dictionary<string, object>>(File.ReadAllText(configFileName));

                if (dict.ContainsKey("healthCheckInterval"))
                {
                    healthCheckInterval = int.Parse((string)dict["healthCheckInterval"]);
                }
                if (dict.ContainsKey("recoveryPauseInterval"))
                {
                    recoveryPauseInterval = int.Parse((string)dict["recoveryPauseInterval"]);
                }
                if (dict.ContainsKey("criticalCounts"))
                {
                    criticalCounts = int.Parse((string)dict["criticalCounts"]);
                }
                if (dict.ContainsKey("elevatedModeRecovery"))
                {
                    elevatedModeRecovery = bool.Parse((string)dict["elevatedModeRecovery"]);
                }

                if (dict.ContainsKey("recoveryItems"))
                {
                    ArrayList recoveryItemDictList = (ArrayList)dict["recoveryItems"];
                    foreach (Dictionary<string, object> recoveryItemDict in recoveryItemDictList)
                    {
                        RecoveryItem recoveryItem = new RecoveryItem();

                        if (recoveryItemDict.ContainsKey("recoveryBatch"))
                        {
                            recoveryItem.RecoveryBatch = (string)recoveryItemDict["recoveryBatch"];
                        }
                        if (recoveryItemDict.ContainsKey("scDatabase"))
                        {
                            recoveryItem.ScDatabase = (string)recoveryItemDict["scDatabase"];
                        }

                        if (recoveryItemDict.ContainsKey("processes"))
                        {
                            ArrayList procsList = (ArrayList)recoveryItemDict["processes"];
                            foreach (var proc in procsList)
                            {
                                recoveryItem.Processes.Add((string)proc);
                            }
                        }
                        if (recoveryItemDict.ContainsKey("scAppNames"))
                        {
                            ArrayList appNameList = (ArrayList)recoveryItemDict["scAppNames"];
                            foreach (var appName in appNameList)
                            {
                                recoveryItem.ScAppNames.Add((string)appName);
                            }
                        }

                        this.recoveryItems.Add(recoveryItem);
                    }
                }

                string recoveryItemsInfo = string.Join("", recoveryItems);

                PrintInfo("Watchdog will be started with:\n" +
                   "    healthCheckInterval : " + healthCheckInterval.ToString() + "\n" +
                   "    recoveryPauseInterval : " + recoveryPauseInterval.ToString() + "\n" +
                   "    elevatedModeRecovery : " + elevatedModeRecovery.ToString() + "\n" +
                   "    criticalCounts : " + criticalCounts.ToString() + "\n" +
                   recoveryItemsInfo
                   );
            }
            catch (IOException e)
            {
                throw new Exception("Invalid format on: " + configFileName, e);
            }
        }

        private void Recover(RecoveryItem rc)
        {
            PrintInfo("Watchdog starts recovery procedure. Start executing file: " + rc.RecoveryBatch);
            if (elevatedModeRecovery)
            {
                ApplicationLoader.PROCESS_INFORMATION procInfo;
                ApplicationLoader.StartProcessAndBypassUAC(rc.RecoveryBatch, out procInfo);
            }
            else
            {
                System.Diagnostics.Process.Start(rc.RecoveryBatch);
            }
            PrintInfo("Watchdog's recovery procedure has finished. Finished executing file: " + rc.RecoveryBatch);
        }

        private bool Check(RecoveryItem rc)
        {
            Process[] processlist = Process.GetProcesses();
            foreach (string procName in rc.Processes)
            {
                bool found = false;
                foreach (Process theprocess in processlist)
                {
                    if (theprocess.ProcessName.Equals(procName))
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    PrintWarning("Watchdog couldn't find the process " + procName + ".");
                    return false;
                }
            }
            
            return CheckStarcounterApps(rc);
        }

        private bool CheckStarcounterApps(RecoveryItem rc)
        {
            System.Diagnostics.Process process = new System.Diagnostics.Process();
            System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
            startInfo.FileName = "staradmin.exe";
            startInfo.Arguments = $"--database={rc.ScDatabase} list app";
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardOutput = true;
            startInfo.CreateNoWindow = true;
            process.StartInfo = startInfo;
            process.Start();

            string stdOutput = process.StandardOutput.ReadToEnd();

            bool allAppsAreRunning = rc.ScAppNames.All(appName => stdOutput.Contains($"{appName} (in {rc.ScDatabase})"));

            return allAppsAreRunning;
        }

        private void RunForever()
        {
            while (true)
            {
            label:
                foreach (RecoveryItem rc in recoveryItems)
                {
                    if (!Check(rc))
                    {
                        Recover(rc);
                        PrintInfo("Watchdog will now wait for the suite to recover.");
                        int cntr = 0;
                        do
                        {
                            PrintInfo("Watchdog's attempt #" + (cntr + 1).ToString() + " to wait for recover...");
                            Thread.Sleep(recoveryPauseInterval);
                            cntr++;
                            if (cntr == criticalCounts)
                            {
                                PrintInfo("Waited critical number of times for the suite to recover, now will repeat recovery.");
                                goto label;
                            }
                        } while (!Check(rc));
                        PrintInfo("Watchdog's suite is now considered recovered!");
                    }
                }
                Thread.Sleep(healthCheckInterval);
            }
        }

        protected override void OnStart(string[] args)
        {
            //System.Diagnostics.Debugger.Launch();
            PrintInfo("Custom batch watchdog has been started.");
            LoadConfigFromFile();
            ThreadPool.QueueUserWorkItem(o => { RunForever(); });
        }

        public CustomBatchWatchdog() { InitializeComponent(); }
        protected override void OnStop()
        {
            PrintInfo("Custom batch watchdog has been signalled to stop.");
        }
    }
}
