using System;
using System.Diagnostics;

namespace CustomWatchdog
{
    internal class ServiceLogEvent : IServiceLog
    {
        internal static IServiceLog Create(RecoveryConfigLog logDef)
        {
            var source = logDef.Path;

            if (!EventLog.SourceExists(source))
                EventLog.CreateEventSource(source, "Application");
            return new ServiceLogEvent(new StringSourceProvider(source))
            {
                Level = logDef.Enums.Level
            };
        }

        private readonly SourceProvider m_provider;

        public ServiceLogEvent(CustomBatchWatchdog service)
            : this(new ServiceSourceProvider(service))
        {
            Level = ServiceLogLevel.Info;
        }

        private ServiceLogEvent(SourceProvider provider)
        {
            m_provider = provider;
        }

        public string Source { get { return m_provider.Source; } }

        public ServiceLogLevel Level { get; set; } = ServiceLogLevel.Info;

        
        public void Write(ServiceLogLevel level, string evt)
        {
            switch (level)
            {
                case ServiceLogLevel.Info:
                    EventLog.WriteEntry(Source, evt, EventLogEntryType.Information, 0x03);
                    break;
                case ServiceLogLevel.Warning:
                    EventLog.WriteEntry(Source, evt, EventLogEntryType.Warning, 0x01);
                    break;
                case ServiceLogLevel.Error:
                    EventLog.WriteEntry(Source, evt, EventLogEntryType.Error, 0x02);
                    break;
                case ServiceLogLevel.Trace:
                case ServiceLogLevel.Debug:
                default:
                    break;
            }
        }

        public void Dispose()
        {
            
        }

        public override string ToString()
        {
            return $"[Event][{Level} {Source}";
        }

        private abstract class SourceProvider
        {
            public abstract string Source { get;  }
        }

        private class ServiceSourceProvider : SourceProvider
        {
            private readonly CustomBatchWatchdog m_service;

            public ServiceSourceProvider(CustomBatchWatchdog service)
            {
                m_service = service;
            }
            public override string Source => m_service.EventLogSource;
        }

        private class StringSourceProvider : SourceProvider
        {
            public StringSourceProvider(string src)
            {
                Source = src;
            }
            public override string Source { get; }
        }
    }
}