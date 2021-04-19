using System.Threading;
using System;

namespace DistributedJobScheduling.Logging
{
    public interface ILogger
    {
        Thread LogginThread { get; }
        void Log(Tag tag, string content);
        void Warning(Tag tag, string content);
        void Warning(Tag tag, string content, Exception e);
        void Error(Tag tag, Exception e);
        void Error(Tag tag, string content, Exception e = null);
        void Fatal(Tag tag, string content, Exception e);

        void Flush();
    }
}