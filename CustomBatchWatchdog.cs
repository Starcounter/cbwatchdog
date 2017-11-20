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
        int healthCheckInterval = 10000;
        uint recoveryExecutionTimeout = 60000 * 5;
        int criticalCounts = 10;
        bool noConsoleForRecoveryScript = false;
        List<RecoveryItem> recoveryItems = new List<RecoveryItem>();

        // local constants (not configurable)
        string configFileName = "cbwatchdog.json";
        string eventLogSource = "Custom Batch Watchdog";

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

                PrintInfo("Reading Configuration File :" + configFileName);

                var dict = ser.Deserialize<Dictionary<string, object>>(File.ReadAllText(configFileName));

                if (dict.ContainsKey("healthCheckInterval"))
                {
                    healthCheckInterval = int.Parse((string)dict["healthCheckInterval"]);
                }
                if (dict.ContainsKey("recoveryExecutionTimeout"))
                {
                    recoveryExecutionTimeout = uint.Parse((string)dict["recoveryExecutionTimeout"]);
                }
                if (dict.ContainsKey("criticalCounts"))
                {
                    criticalCounts = int.Parse((string)dict["criticalCounts"]);
                }
                if (dict.ContainsKey("noConsoleForRecoveryScript"))
                {
                    noConsoleForRecoveryScript = bool.Parse((string)dict["noConsoleForRecoveryScript"]);
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

                        if (recoveryItemDict.ContainsKey("overrideRecoveryExecutionTimeout"))
                        {
                            recoveryItem.overrideRecoveryExecutionTimeout = uint.Parse((string)recoveryItemDict["overrideRecoveryExecutionTimeout"]);
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
                   "    recoveryExecutionTimeout : " + recoveryExecutionTimeout.ToString() + "\n" +
                   "    noConsoleForRecoveryScript : " + noConsoleForRecoveryScript.ToString() + "\n" + 
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
            ApplicationLoader.PROCESS_INFORMATION procInfo;
            if (rc.overrideRecoveryExecutionTimeout != 0)
                recoveryExecutionTimeout = rc.overrideRecoveryExecutionTimeout;
            ApplicationLoader.StartProcessAndBypassUAC(rc.RecoveryBatch, noConsoleForRecoveryScript, recoveryExecutionTimeout, PrintInfo, out procInfo);
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
                foreach (RecoveryItem rc in recoveryItems)
                {
                    bool check = Check(rc);
                    int cntr = 0;

                    if (check == false)
                    {    
                        do
                        {
                            cntr++;

                            if (cntr == criticalCounts)
                            {
                                // maximum number of recovery attemps has been succeeded, abort
                                PrintInfo($"{(criticalCounts - 1).ToString()} recovery attemps for {rc.RecoveryBatch} file has been made, aborting further attemps and moving on with next revoceryItem");
                                break;
                            }
                            else
                            {
                                // execute recovery
                                PrintInfo("Watchdog's recovery attempt #" + (cntr).ToString() + " procedure started: " + rc.RecoveryBatch);
                                Recover(rc);
                            }

                            check = Check(rc);
                            if (check == true)
                            {
                                PrintInfo("Watchdog's recovery attempt #" + (cntr).ToString() + " SUCCESS: " + rc.RecoveryBatch);
                            }
                            else
                            {
                                PrintInfo("Watchdog's recovery attempt #" + (cntr).ToString() + " FAILED: " + rc.RecoveryBatch);
                            }
                        } while (check == false);
                    }
                }
                Thread.Sleep(healthCheckInterval);
            }
        }



        protected override void OnStart(string[] args)
        {
            //System.Diagnostics.Debugger.Launch();
            PrintInfo("Custom batch watchdog has been started.");

            if (args.Length > 0)
            {
                OverrideSettings(args);
            }

            LoadConfigFromFile();
            ThreadPool.QueueUserWorkItem(o => { RunForever(); });
        }
        private void OverrideSettings(string[] args)
        {
            if (args.Length > 0)
            {
                configFileName = args[0];
                if (configFileName.IndexOf('/') > -1)
                    configFileName = configFileName.Replace("/", string.Empty);
                PrintInfo("Config file updated to: " + configFileName);
            }
            if (args.Length > 1)
            {
                eventLogSource = args[1];
                PrintInfo("Event Source Name updated to: " + eventLogSource);
            }
        }

        public CustomBatchWatchdog() { InitializeComponent(); }
        protected override void OnStop()
        {
            PrintInfo("Custom batch watchdog has been signalled to stop.");
        }
    }
}
