using System.Threading.Tasks;
using System;

namespace DistributedJobScheduling.Logging
{
    public interface ILogger
    {
        Task LogginTask { get; }
        void Log(Tag tag, string content);
        void Warning(Tag tag, string content);
        void Warning(Tag tag, string content, Exception e);
        void Error(Tag tag, Exception e);
        void Error(Tag tag, string content, Exception e = null);
        void Fatal(Tag tag, string content, Exception e);

        void Flush();
    }
}