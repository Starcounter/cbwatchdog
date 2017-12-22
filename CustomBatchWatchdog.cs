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
using System.Text;

namespace CustomWatchdog
{
    public partial class CustomBatchWatchdog : ServiceBase
    {
        private readonly ServiceLogManager m_log = new ServiceLogManager();
        private readonly IServiceLog m_defaultLog;
        private readonly string m_assemblyDirectory;
        private readonly UserInfo m_user;
        private readonly ManualResetEventSlim m_stopEvt = new ManualResetEventSlim(false);
        private readonly ManualResetEventSlim m_doneEvt = new ManualResetEventSlim(true);

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
            m_user = new UserInfo();
            InitializeComponent();            
            var file = new Uri(GetType().Assembly.Location).LocalPath;
            m_assemblyDirectory = Path.GetDirectoryName(file);
            m_defaultLog = log ?? new ServiceLogEvent(this);
            m_log.Add(m_defaultLog);
        }


        private IList<RecoveryConfigItem> RecoveryItems => m_config?.RecoveryItems;

        internal string EventLogSource { get { return eventLogSource; } }

        private void PrintWarning(string evt) => m_log.Write(ServiceLogLevel.Warning, evt);

        private void PrintError(string evt) => m_log.Write(ServiceLogLevel.Error, evt);

        private void PrintInfo(string evt) => m_log.Write(ServiceLogLevel.Info, evt);

        private void PrintDebug(string evt) => m_log.Write(ServiceLogLevel.Debug, evt);

        private void PrintTrace(string evt) => m_log.Write(ServiceLogLevel.Trace, evt);


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
                ApplyLogs(cfg.Logs);

                // Just print the json
                var username = m_user.Name;
                PrintInfo($"[{username}] Watchdog will be started with: {Environment.NewLine}{cfg}");
            }
            catch (IOException e)
            {
                var name = cfgPath ?? configFileName;
                throw new Exception("Invalid format on: " + name, e);
            }
        }

        private void ApplyLogs(IList<RecoveryConfigLog> logs)
        {
            if (logs != null)
            {
                var definedLogs = ServiceLogManager.Create(logs);

                if (definedLogs.Any())
                {
                    // We got some user defined logs, lower the level of the default log
                    m_defaultLog.Level = ServiceLogLevel.Error;

                    foreach (var log in definedLogs)
                    {
                        m_log.Add(log);
                        m_log.Write(ServiceLogLevel.Debug, log.ToString());
                    }
                }
                m_log.Write(ServiceLogLevel.Debug, $"[{m_log.Level}] Enabled"); 
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

        private TimeSpan Recover(RecoveryConfigItem rc)
        {
            ApplicationLoader.PROCESS_INFORMATION procInfo;
            var timeout = m_config.RecoveryExecutionTimeout;

            if (rc.OverrideRecoveryExecutionTimeout != 0)
            {
                timeout = rc.OverrideRecoveryExecutionTimeout;
            }
            var recoverTime = TimeSpan.FromMilliseconds((int)timeout);
            var watch = Stopwatch.StartNew();
            // This is the way to go if running as a service. But when debugging we don't have this privelege. Just spawn a new process
            if (m_user.IsServiceAccount || m_user.IsSystemAccount)
            {
                ApplicationLoader.StartProcessAndBypassUAC(rc.RecoveryBatch, m_config.NoConsoleForRecoveryScript, timeout, PrintDebug, out procInfo);
            }
            else
            {
                ApplicationInlineLoader.Start(GetFile(rc.RecoveryBatch), m_config.NoConsoleForRecoveryScript, timeout, PrintDebug);
            }
            // Return the amount of time left to wait for recovery execution
            return recoverTime - watch.Elapsed;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="rc"></param>
        /// <param name="retryTimespan">Retry to find the process for this interval</param>
        /// <returns></returns>
        private bool Check(RecoveryConfigItem rc, TimeSpan retryTimespan)
        {
            var watch = Stopwatch.StartNew();
            string failed;

            do
            {
                if (DoCheck(rc, out failed))
                {
                    PrintDebug($"Find took {watch.Elapsed.TotalMilliseconds}ms");
                    return true;
                }
                Thread.Sleep(1);

            } while (watch.Elapsed < retryTimespan);

            return false;
        }

        private bool Check(RecoveryConfigItem rc)
        {
            string procName;

            if (!DoCheck(rc, out procName))
            {
                LogCheckFailed(procName);
                return false;
            }
            return true;
        }

        private void LogCheckFailed(string procName)
        {
            PrintWarning($"Watchdog couldn't find the process {procName}.");
        }

        private bool DoCheck(RecoveryConfigItem rc, out string failed)
        {
            // It's faster to get by name on each
            foreach (string procName in rc.Processes)
            {
                var procs = Process.GetProcessesByName(procName);
                var found = procs.Length > 0;

                if (!found)
                {

                    if (m_log.Level == ServiceLogLevel.Trace)
                    {
#if DBG_LOG
                        m_log.Write(ServiceLogLevel.Trace, $"Failed to find '{procName}'{Environment.NewLine}{GetRunning()}");
#else
                        m_log.Write(ServiceLogLevel.Trace, $"Failed to find '{procName}'");
#endif
                    }

                    failed = procName;
                    return false;
                }
                else if (m_log.Level == ServiceLogLevel.Trace)
                {
                    var procInfo = string.Join(Environment.NewLine, procs.Select(p => $"[{p.ProcessName}] {p.Id}"));
                    m_log.Write(ServiceLogLevel.Trace, $"Found '{procName}' Processes:{Environment.NewLine}{procInfo}");
                }
            }
            failed = null;
            return CheckStarcounterApps(rc);
        }

        private string GetRunning()
        {
            var procs = Process.GetProcesses();
            var sb = new StringBuilder(procs.Length * 10);
            var nameWidth = procs.Max(p => p.ProcessName.Length) + 1;
            var pidWidth = int.MaxValue.ToString().Length + 1;

            foreach (var p in procs.OrderBy(p => p.ProcessName))
            {
                try
                {
                    var paddedName = p.ProcessName.PadRight(nameWidth);
                    var paddedPid = p.Id.ToString().PadRight(pidWidth);
                    sb.AppendLine($"{paddedName}id: {paddedPid}, Session: {p.SessionId}");
                }
                catch
                {
                }
            }
            return sb.ToString();
        }

        private bool CheckStarcounterApps(RecoveryConfigItem rc)
        {
            var apps = rc.ScAppNames;

            if (apps != null && apps.Count > 0)
            {
                var appNames = string.Join(", ", apps);                
                var db = rc.ScDatabase ?? "default";
                var scFileName = "staradmin.exe";
                PrintDebug($"Checking starcounter apps: {appNames}, db: {db}");
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
            PrintDebug($"Skipped starcounter apps, not defined");
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
                            TimeSpan itemRecoveryTs;
                            // Make at least one attempt
                            if (cntr++ == criticalCounts)
                            {
                                // maximum number of recovery attemps has been succeeded, abort
                                PrintDebug($"{(criticalCounts).ToString()} recovery attemps for {rc.RecoveryBatch} file has been made, aborting further attemps and moving on with next revoceryItem");
                                break;
                            }
                            else
                            {
                                // execute recovery
                                PrintDebug("Watchdog's recovery attempt #" + (cntr).ToString() + " procedure started: " + rc.RecoveryBatch);
                                itemRecoveryTs = Recover(rc);
                            }

                            check = Check(rc, itemRecoveryTs);

                            if (check == true)
                            {
                                PrintDebug("Watchdog's recovery attempt #" + (cntr).ToString() + " SUCCESS: " + rc.RecoveryBatch);
                            }
                            else
                            {
                                // TODO: level?
                                PrintWarning("Watchdog's recovery attempt #" + (cntr).ToString() + " FAILED: " + rc.RecoveryBatch);
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
