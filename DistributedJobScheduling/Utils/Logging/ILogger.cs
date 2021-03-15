using System;

namespace DistributedJobScheduling.Logging
{
    public interface ILogger
    {
        void Log(Tag tag, string content);
        void Warning(Tag tag, string content);
        void Warning(Tag tag, string content, Exception e);
        void Error(Tag tag, Exception e);
        void Error(Tag tag, string content, Exception e);
    }
}