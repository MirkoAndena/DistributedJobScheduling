using System;
using DistributedJobScheduling.Communication.Basic;
using DistributedJobScheduling.Logging;
using Xunit.Abstractions;

namespace DistributedJobScheduling.Tests
{
    public class StubLogger : ILogger
    {
        private Node _boundNode;
        private ITestOutputHelper _output;
        public StubLogger(Node node, ITestOutputHelper output)
        {
            _boundNode = node;
            _output = output;
        }
        
        public void Error(Tag tag, Exception e) => WriteLog(tag, $"ERROR: {e.Message}", e);

        public void Error(Tag tag, string content, Exception e) => WriteLog(tag, $"ERROR: {content}", e);

        public void Fatal(Tag tag, string content, Exception e)
        {
            WriteLog(tag, $"FATAL: {content}", e);

            //FIXME: Handle fatal?
            throw e;
        }

        public void Log(Tag tag, string content) => WriteLog(tag, $"INFO: {content}");

        public void Warning(Tag tag, string content) => WriteLog(tag, $"WARN: {content}");

        public void Warning(Tag tag, string content, Exception e) => WriteLog(tag, $"WARN: {content}", e);

        private void WriteLog(Tag tag, string content, Exception e = null)
        {
            _output.WriteLine($"|{DateTime.Now.ToString("hh:mm:ss.fff")}|{{{_boundNode.ToString()}}} [{Enum.GetName(typeof(Tag), tag)}] \t {content}");
            if(e != null)
                _output.WriteLine(e.StackTrace);
        }
    }
}