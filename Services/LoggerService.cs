using System;
using System.Collections.Generic;
using System.IO;
using ToastAlert.Config;

namespace ToastAlert.Services
{
    public class LoggerService
    {
        private readonly Config.Config _config;

        public LoggerService(Config.Config config)
        {
            _config = config;
        }

        public void LogMessage(string sender, string message)
        {
            if (!_config.Logging.Enabled || !_config.Logging.LogToFile) return;
            try
            {
                string logFile = _config.Monitoring.LogFilePath;
                if (_config.Logging.RotateLogs)
                {
                    var fi = new FileInfo(logFile);
                    if (fi.Exists && fi.Length > _config.Logging.MaxLogFileSizeMB * 1024 * 1024)
                    {
                        string backup = logFile + ".bak";
                        if (File.Exists(backup)) File.Delete(backup);
                        File.Move(logFile, backup);
                    }
                }

                var parts = new List<string>();
                if (_config.Logging.IncludeTimestamp)
                    parts.Add($"[{DateTime.Now.ToString(_config.Logging.TimestampFormat)}]");
                if (_config.Logging.IncludeSender) parts.Add(sender);
                if (_config.Logging.IncludeMessage) parts.Add(message);

                string logLine = string.Join(": ", parts);
                File.AppendAllText(logFile, logLine + "\n" + _config.Logging.Separator + "\n");
            }
            catch { }
        }
    }
}
