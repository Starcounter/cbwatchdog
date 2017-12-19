using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CustomWatchdog
{
    class ServiceLogManager : IServiceLog
    {
        private readonly List<IServiceLog> m_logs = new List<IServiceLog>();

        public ServiceLogLevel Level { get; set; }

        public void Add(IServiceLog log)
        {
            m_logs.Add(log);
            var l = log.Level;

            if (l < Level)
            {
                Level = l;
            }
        }

        public void Write(ServiceLogLevel level, string msg)
        {
            foreach (var log in m_logs)
            {
                if (log.Level <= level)
                {
                    log.Write(level, msg);
                }
            }   
        }

        internal static IEnumerable<IServiceLog> Create(List<RecoveryConfigLog> logDefs)
        {
            if (logDefs != null && logDefs.Count > 0)
            {
                var logs = new List<IServiceLog>(logDefs.Count);

                foreach (var logDef in logDefs)
                {
                    switch (logDef.Enums.LogType)
                    {
                        case ServiceLogType.File:
                            logs.Add(ServiceLogFile.Create(logDef));
                            break;
                        case ServiceLogType.Debug:
                            logs.Add(ServiceLogDebug.Create(logDef));
                            break;
                        case ServiceLogType.Event:
                            logs.Add(ServiceLogEvent.Create(logDef));
                            break;
                        default:
                            throw new NotImplementedException(logDef.ToString());
                    }
                }
                return logs;
            }
            return Enumerable.Empty<IServiceLog>();
        }

        public void Dispose()
        {
            foreach (var item in m_logs)
            {
                try
                {
                    item.Dispose();
                }
                catch
                {
                }
            }
        }
    }
}
