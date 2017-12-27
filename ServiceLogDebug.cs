using System;
using System.Diagnostics;

namespace CustomWatchdog
{
    /// <summary>
    /// Writes to Debug.Print
    /// </summary>
    class ServiceLogDebug : IServiceLog
    {
        public ServiceLogLevel Level { get; set; }

        public void Dispose()
        {
            
        }

        public void Write(ServiceLogLevel level, string msg)
        {
            Debug.Print($"{DateTime.Now.ToString("yyMMdd HH:mm:ss,ffff")} [{level}] {msg}");
        }

        internal static ServiceLogDebug Create(RecoveryConfigLog logDef)
        {
            return new ServiceLogDebug
            {
                Level = logDef.Enums.Level
            };
        }

        public override string ToString()
        {
            return $"[DEBUG][{Level}";
        }
    }
}