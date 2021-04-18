using System;
using System.Threading.Tasks;
using DistributedJobScheduling.Communication.Basic;
using DistributedJobScheduling.Logging;
using Xunit.Abstractions;

namespace DistributedJobScheduling.Tests
{
    public class StubLogger : ILogger
    {
        private Node _boundNode;
        private ITestOutputHelper _output;

        public StubLogger(ITestOutputHelper outputHelper) : this(null, outputHelper) { }

        public StubLogger(Node node, ITestOutputHelper output)
        {
            _boundNode = node;
            _output = output;
        }

        public Task LogginTask => new Task(Flush);

        public void Error(Tag tag, Exception e) => WriteLog(tag, $"ERROR: {e.Message}", e);

        public void Error(Tag tag, string content, Exception e) => WriteLog(tag, $"ERROR: {content}", e);

        public void Fatal(Tag tag, string content, Exception e)
        {
            WriteLog(tag, $"FATAL: {content}", e);

            //FIXME: Handle fatal?
            throw e;
        }

        public void Flush()
        {
            // Nothing
        }

        public void Log(Tag tag, string content) => WriteLog(tag, $"INFO: {content}");

        public void Warning(Tag tag, string content) => WriteLog(tag, $"WARN: {content}");

        public void Warning(Tag tag, string content, Exception e) => WriteLog(tag, $"WARN: {content}", e);

        private void WriteLog(Tag tag, string content, Exception e = null)
        {
            string message = $"|{DateTime.Now.ToString("hh:mm:ss.fff")}|{{{_boundNode?.ToString()}}} [{Enum.GetName(typeof(Tag), tag)}] \t {content}";
            Console.WriteLine(message);
            _output.WriteLine(message);
            if(e != null)
            {
                Console.WriteLine(e.StackTrace);
                _output.WriteLine(e.StackTrace);
            } 
        }
    }
}