using System;
using System.IO;
using System.Text;

namespace DistributedJobScheduling.Logging
{
    enum LogType { INFORMATION, WARNING, ERROR }

    public class CsvLogger : ILogger
    {
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

            if (!File.Exists(_directory))
                Directory.CreateDirectory(_directory);

            if (!File.Exists(_filepath))
                File.Create(_filepath);
        }

        private string Compile(params string[] elements)
        {
            StringBuilder stringBuilder = new StringBuilder();
            return stringBuilder.AppendJoin(_sepatator, elements).ToString();
        }

        private void Log(LogType type, Tag tag, string content, Exception e)
        {
            string entry = Compile(type.ToString(), tag.ToString(), content, e?.Message);
            File.AppendAllText(_filepath, entry);
            if (_consoleWrite) Console.WriteLine(entry);
            string exceptionPath = $"{_directory}/{DateTime.Now.ToString("ddMMyyHHmmssfff")}.txt";
            File.WriteAllText(exceptionPath, e.StackTrace);
        }

        public void Error(Tag tag, Exception e) => Log(LogType.ERROR, tag, null, e);
        public void Error(Tag tag, string content, Exception e) => Log(LogType.ERROR, tag, content, e);
        public void Log(Tag tag, string content) => Log(LogType.INFORMATION, tag, content, null);
        public void Warning(Tag tag, string content) => Log(LogType.WARNING, tag, content, null);
        public void Warning(Tag tag, string content, Exception e) => Log(LogType.WARNING, tag, content, e);
    }
}