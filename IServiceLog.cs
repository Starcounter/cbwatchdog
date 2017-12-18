namespace CustomWatchdog
{
    /// <summary>
    /// The interface of the log to use for the service.
    /// </summary>
    public interface IServiceLog
    {
        void Warn(string evt);
        void Error(string evt);
        void Info(string evt);
    }
}
