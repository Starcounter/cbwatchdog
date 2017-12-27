using System;

namespace CustomWatchdog
{
    /// <summary>
    /// The interface of the log to use for the service.
    /// </summary>
    public interface IServiceLog : IDisposable
    {
        /// <summary>
        /// The level that applies to this log
        /// </summary>
        ServiceLogLevel Level { get; set; }

        /// <summary>
        /// Write a log
        /// </summary>
        /// <param name="level"></param>
        /// <param name="msg"></param>
        void Write(ServiceLogLevel level, string msg);
    }
}
