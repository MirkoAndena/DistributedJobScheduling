using System.Threading;
using System.Threading.Tasks;
using System;
using System.IO;
using System.Text;
using DistributedJobScheduling.LifeCycle;
using DistributedJobScheduling.Storage;
using DistributedJobScheduling.Queues;

namespace DistributedJobScheduling.Logging
{
    enum LogType { INFORMATION, WARNING, ERROR, FATAL }

    public class CsvLogger : ILogger, IInitializable, IStartable
    {
        private AsyncGenericQueue<(string, LogType, Exception)> _logQueue;
        private Task _loggerTask;
        private CancellationTokenSource _loggerCancellationToken;
        private DateTime _startupTime;
        private ReusableIndex _reusableIndex;
        private string _sepatator;
        private string _directory;
        private string _filepath;
        private bool _consoleWrite;
        
        public CsvLogger(string path, bool consoleWrite = true, string separator = ",")
        {
            _directory = $"{path}/Logs";
            _filepath = $"{_directory}/logs.csv";

            _sepatator = separator;
            _consoleWrite = consoleWrite;
            _loggerCancellationToken = new CancellationTokenSource();
        }

        public void Init()
        {
            _logQueue = new AsyncGenericQueue<(string, LogType, Exception)>();
            if (!File.Exists(_directory))
                Directory.CreateDirectory(_directory);
            
            File.WriteAllText(_filepath, Compile("Index", "TimeStamp", "Type", "Tag", "Content", "Exception"));
            
            _startupTime = DateTime.Now;
            _reusableIndex = new ReusableIndex();
        }

        private async Task LoggerLoop(CancellationToken token)
        {
            try
            {
                while(!token.IsCancellationRequested)
                {
                    var log = await _logQueue.Dequeue(token);
                    CommitLog(log);
                }
            }
            catch {}
        }

        private string Compile(params string[] elements)
        {
            StringBuilder stringBuilder = new StringBuilder();
            return stringBuilder.AppendJoin(_sepatator, elements).ToString() + Environment.NewLine;
        }

        private void CommitLog((string, LogType, Exception) content)
        {
            File.AppendAllText(_filepath, content.Item1);

            if (_consoleWrite) 
            {
                var type = content.Item2;
                if (type == LogType.WARNING) Console.ForegroundColor = ConsoleColor.DarkYellow;
                if (type == LogType.ERROR) Console.ForegroundColor = ConsoleColor.Red;
                if (type == LogType.FATAL) Console.ForegroundColor = ConsoleColor.Magenta;
                Console.Write(content);
                Console.ResetColor();
            }

            var e = content.Item3;
            if(e != null)
            {
                string exceptionPath = $"{_directory}/{DateTime.Now.ToString("ddMMyyHHmmssfff")}.txt";
                File.WriteAllText(exceptionPath, e.Message + Environment.NewLine + e.StackTrace);
            }
        }

        private void Log(LogType type, Tag tag, string content, Exception e)
        {
            string entry = Compile(_reusableIndex.NewIndex.ToString(), (DateTime.Now - _startupTime).ToString(), type.ToString(), tag.ToString(), content, e?.Message);
            var log = (entry, type, e);

            if(type == LogType.FATAL)
                CommitLog(log);
            else
                _logQueue.Enqueue(log);
        }

        public void Error(Tag tag, Exception e) => Log(LogType.ERROR, tag, null, e);
        public void Error(Tag tag, string content, Exception e = null) => Log(LogType.ERROR, tag, content, e);
        public void Log(Tag tag, string content) => Log(LogType.INFORMATION, tag, content, null);
        public void Warning(Tag tag, string content) => Log(LogType.WARNING, tag, content, null);
        public void Warning(Tag tag, string content, Exception e) => Log(LogType.WARNING, tag, content, e);
        public void Fatal(Tag tag, string content, Exception e)
        {
            Log(LogType.FATAL, tag, content, e);
            SystemLifeCycle.Shutdown?.Invoke();
        }

        public void Start()
        {
            _loggerCancellationToken = new CancellationTokenSource();
            _loggerTask = LoggerLoop(_loggerCancellationToken.Token);
        }

        public void Stop()
        {
            _loggerCancellationToken.Cancel();
            _loggerCancellationToken = null;
            _loggerTask = null;
        }
    }
}