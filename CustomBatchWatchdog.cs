using System;
using System.IO;
using System.ServiceProcess;
using System.Diagnostics;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using System.Threading;
using Toolkit;
using System.Linq;
using System.Security.Principal;
using System.DirectoryServices.AccountManagement;

namespace CustomWatchdog
{
    public partial class CustomBatchWatchdog : ServiceBase
    {
        private readonly string m_assemblyDirectory;
        private readonly SecurityIdentifier m_sid;
        private readonly ManualResetEventSlim m_stopEvt = new ManualResetEventSlim(false);
        private readonly ManualResetEventSlim m_doneEvt = new ManualResetEventSlim(true);
        private readonly IServiceLog m_log;

        // defaults (can be overriden from a config file)
        private RecoveryConfig m_config;



        private string configFileName = "cbwatchdog.json";
        private string eventLogSource = "Custom Batch Watchdog";

        public CustomBatchWatchdog()
            : this(null)
        {
        }

        public CustomBatchWatchdog(IServiceLog log)
        {
            m_sid = UserPrincipal.Current.Sid;
            InitializeComponent();
            m_log = log ?? new EventServiceLog(this);
            IsServiceAccount = WellKnownSidType.LocalServiceSid.Equals(m_sid) || WellKnownSidType.LocalSystemSid.Equals(m_sid);
            var file = new Uri(GetType().Assembly.Location).LocalPath;
            m_assemblyDirectory = Path.GetDirectoryName(file);
        }

        /// <summary>
        /// True if running as Local System or Local Service
        /// </summary>
        public bool IsServiceAccount { get; }

        private List<RecoveryConfigItem> RecoveryItems => m_config?.RecoveryItems;

        internal string EventLogSource { get { return eventLogSource; } }

        // Windows event log handling
        private void InitEventLog()
        {
            if (!EventLog.SourceExists(eventLogSource))
                EventLog.CreateEventSource(eventLogSource, "Application");
        }
        private void PrintWarning(string evt) => m_log.Warn(evt);

        private void PrintError(string evt) => m_log.Error(evt);

        private void PrintInfo(string evt) => m_log.Info(evt);


        private void LoadConfigFromFile()
        {
            string cfgPath = null;
            try
            {
                JavaScriptSerializer ser = new JavaScriptSerializer();
                cfgPath = GetConfigFile();
                PrintInfo("Reading Configuration File :" + cfgPath);
                var fi = new FileInfo(cfgPath);
                var cfg = RecoveryConfig.Parse(fi);
                cfg.Validate();
                m_config = cfg;

                // Just print the json
                var nta = (NTAccount)m_sid.Translate(typeof(NTAccount));
                PrintInfo($"[{nta.Value}] Watchdog will be started with: {Environment.NewLine}{cfg}");
            }
            catch (IOException e)
            {
                var name = cfgPath ?? configFileName;
                throw new Exception("Invalid format on: " + name, e);
            }
        }

        /// <summary>
        /// Looks for a config file in the same directory as the assembly
        /// </summary>
        /// <returns></returns>
        private string GetConfigFile()
        {
            return GetFile(configFileName);
        }

        private string GetFile(string name)
        {
            // Check if the path is rooted
            if (Path.IsPathRooted(name))
            {
                return name;
            }
            else
            {

                return Path.Combine(m_assemblyDirectory, name);
            }
        }

        private void Recover(RecoveryConfigItem rc)
        {
            ApplicationLoader.PROCESS_INFORMATION procInfo;
            var timeout = m_config.RecoveryExecutionTimeout;

            if (rc.OverrideRecoveryExecutionTimeout != 0)
            {
                timeout = rc.OverrideRecoveryExecutionTimeout;
            }
            // This is the way to go if running as a service. But when debugging we don't have this privelege. Just spawn a new process
            if (IsServiceAccount)
            {
                ApplicationLoader.StartProcessAndBypassUAC(rc.RecoveryBatch, m_config.NoConsoleForRecoveryScript, timeout, PrintInfo, out procInfo);
            }
            else
            {
                ApplicationInlineLoader.Start(GetFile(rc.RecoveryBatch), m_config.NoConsoleForRecoveryScript, timeout, PrintInfo);
            }
            // It can take a little while for the process to be spawned, the question is how long...
        }



        private bool Check(RecoveryConfigItem rc)
        {
            // It's faster to get by name on each
            foreach (string procName in rc.Processes)
            {
                var found = Process.GetProcessesByName(procName).Length > 0;

                if (!found)
                {
                    PrintWarning("Watchdog couldn't find the process " + procName + ".");
                    return false;
                }
            }

            return CheckStarcounterApps(rc);
        }

        private bool CheckStarcounterApps(RecoveryConfigItem rc)
        {
            var apps = rc.ScAppNames;

            if (apps != null && apps.Count > 0)
            {
                var db = rc.ScDatabase ?? "default";
                var scFileName = "staradmin.exe";
                System.Diagnostics.Process process = new System.Diagnostics.Process();
                System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
                startInfo.FileName = string.IsNullOrEmpty(rc.StarcounterBinDirectory) ? scFileName : Path.Combine(rc.StarcounterBinDirectory, scFileName);
                startInfo.Arguments = $"--database={db} list app";
                startInfo.UseShellExecute = false;
                startInfo.RedirectStandardOutput = true;
                startInfo.CreateNoWindow = true;
                process.StartInfo = startInfo;
                process.Start();

                string stdOutput = process.StandardOutput.ReadToEnd();

                bool allAppsAreRunning = apps.All(appName => stdOutput.Contains($"{appName} (in {db})"));

                return allAppsAreRunning;
            }
            return true;
        }

        private void RunForever()
        {
            m_doneEvt.Reset();

            try
            {
                DoRun();
            }
            finally
            {
                m_doneEvt.Set();
            }
        }

        private void DoRun()
        {
            var healthCheckInterval = (int)m_config.HealthCheckInterval;
            var criticalCounts = (int)m_config.CriticalCounts;

            // Apply sanity check for the values
            healthCheckInterval = Math.Max(1, healthCheckInterval); // Sleep at least 1 ms
            criticalCounts = Math.Max(1, criticalCounts); // Make at least one attempt


            while (!m_stopEvt.IsSet)
            {
                foreach (RecoveryConfigItem rc in RecoveryItems)
                {
                    bool check = Check(rc);
                    int cntr = 0;

                    if (check == false)
                    {
                        do
                        {
                            // Make at least one attempt
                            if (cntr++ == criticalCounts)
                            {
                                // maximum number of recovery attemps has been succeeded, abort
                                PrintInfo($"{(criticalCounts).ToString()} recovery attemps for {rc.RecoveryBatch} file has been made, aborting further attemps and moving on with next revoceryItem");
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
                m_stopEvt.Wait(healthCheckInterval);
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

            if (RecoveryItems.Any())
            {
                ThreadPool.QueueUserWorkItem(o => { RunForever(); });
            }
            else
            {
                PrintWarning("No recovery items, nothing to do");
            }
        }

        private void OverrideSettings(string[] args)
        {
            if (args.Length > 0)
            {
                configFileName = args[0];
                if (configFileName.IndexOf('/') > -1)
                {
                    configFileName = configFileName.Replace("/", string.Empty);
                }

                PrintInfo("Config file updated to: " + configFileName);
            }
            if (args.Length > 1)
            {
                eventLogSource = args[1];
                if (eventLogSource.IndexOf('/') > -1)
                {
                    eventLogSource = eventLogSource.Replace("/", string.Empty);
                }
                PrintInfo("Event Source Name updated to: " + eventLogSource);
            }
        }



        protected override void OnStop()
        {
            PrintInfo("Custom batch watchdog has been signalled to stop.");
            m_stopEvt.Set();
            // Give the loop some time to exit
            m_doneEvt.Wait(TimeSpan.FromSeconds(5));
        }
    }
}
