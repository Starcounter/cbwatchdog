using System;
using System.IO;
using System.Threading;

namespace CustomWatchdog
{
    class ServiceLogFile : IServiceLog
    {
        private readonly string m_path;
        private readonly StreamWriter m_writer;
        private int m_isOpen;

        private ServiceLogFile(FileInfo fi, StreamWriter writer)
        {
            m_path = fi.FullName;
            m_writer = writer;
            m_isOpen = 1;
        }

        public static ServiceLogFile Create(RecoveryConfigLog def)
        {
            var fi = new FileInfo(def.Path);
            var di = fi.Directory;

            if (!di.Exists)
            {
                di.Create();
            }
            var writer = new StreamWriter(fi.Open(FileMode.Create, FileAccess.ReadWrite, FileShare.Read))
            {
                AutoFlush = true
            };

            return new ServiceLogFile(fi, writer)
            {
                Level = def.Enums.Level
            };

        }
        public ServiceLogLevel Level { get; set; }

        public void Write(ServiceLogLevel level, string msg)
        {
            if (m_isOpen == 1)
            {
                try
                {
                    m_writer.WriteLine($"{DateTime.Now.ToString("yyMMdd HH:mm:ss,ffff")} [{level}] {msg}");
                }
                catch
                {
                }
            }
        }

        public override string ToString()
        {
            return $"[File][{Level} {m_path}";
        }

        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref m_isOpen, 0, 1) == 1)
            {
                m_writer.Close();
                m_writer.Dispose();
            }
        }
    }
}
