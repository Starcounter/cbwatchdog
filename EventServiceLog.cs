using System.Diagnostics;

namespace CustomWatchdog
{
    internal class EventServiceLog : IServiceLog
    {
        private readonly CustomBatchWatchdog m_service;

        public EventServiceLog(CustomBatchWatchdog service)
        {
            m_service = service;
        }

        public string Source { get { return m_service.EventLogSource; } } 

        public void Error(string evt) => EventLog.WriteEntry(Source, evt, EventLogEntryType.Error, 0x02); 

        public void Info(string evt) => EventLog.WriteEntry(Source, evt, EventLogEntryType.Information, 0x03);

        public void Warn(string evt) => EventLog.WriteEntry(Source, evt, EventLogEntryType.Warning, 0x01); 
    }
}