using System;
using System.IO;
using System.Text;
using DistributedJobScheduling.LifeCycle;
using DistributedJobScheduling.Storage;

namespace DistributedJobScheduling.Logging
{
    enum LogType { INFORMATION, WARNING, ERROR, FATAL }

    public class CsvLogger : ILogger, IInitializable
    {
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
        }

        public void Init()
        {
            if (!File.Exists(_directory))
                Directory.CreateDirectory(_directory);
            
            File.WriteAllText(_filepath, Compile("Index", "TimeStamp", "Type", "Tag", "Content", "Exception"));
            
            _startupTime = DateTime.Now;
            _reusableIndex = new ReusableIndex();
        }

        private string Compile(params string[] elements)
        {
            StringBuilder stringBuilder = new StringBuilder();
            return stringBuilder.AppendJoin(_sepatator, elements).ToString() + Environment.NewLine;
        }

        private void Log(LogType type, Tag tag, string content, Exception e)
        {
            string entry = Compile(_reusableIndex.NewIndex.ToString(), (DateTime.Now - _startupTime).ToString(), type.ToString(), tag.ToString(), content, e?.Message);
            File.AppendAllText(_filepath, entry);

            if (_consoleWrite) 
            {
                if (type == LogType.WARNING) Console.ForegroundColor = ConsoleColor.DarkYellow;
                if (type == LogType.ERROR) Console.ForegroundColor = ConsoleColor.Red;
                if (type == LogType.FATAL) Console.ForegroundColor = ConsoleColor.Magenta;
                Console.Write(entry);
                Console.ResetColor();
            }

            if(e != null)
            {
                string exceptionPath = $"{_directory}/{DateTime.Now.ToString("ddMMyyHHmmssfff")}.txt";
                File.WriteAllText(exceptionPath, e.StackTrace);
            }
        }

        public void Error(Tag tag, Exception e) => Log(LogType.ERROR, tag, null, e);
        public void Error(Tag tag, string content, Exception e) => Log(LogType.ERROR, tag, content, e);
        public void Log(Tag tag, string content) => Log(LogType.INFORMATION, tag, content, null);
        public void Warning(Tag tag, string content) => Log(LogType.WARNING, tag, content, null);
        public void Warning(Tag tag, string content, Exception e) => Log(LogType.WARNING, tag, content, e);
        public void Fatal(Tag tag, string content, Exception e)
        {
            Log(LogType.FATAL, tag, content, e);
            SystemLifeCycle.Shutdown?.Invoke();
        }
    }
}