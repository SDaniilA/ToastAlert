using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ToastAlert.Config;

namespace ToastAlert.Services
{
    public class LoggerService : IDisposable
    {
        private readonly Config.Config _config;
        private readonly ConcurrentQueue<string> _logQueue = new();
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _workerTask;
        private bool _disposed = false;

        public LoggerService(Config.Config config)
        {
            _config = config;
            _workerTask = Task.Run(ProcessQueue);
        }

        public void LogMessage(string sender, string message)
        {
            if (!_config.Logging.Enabled || !_config.Logging.LogToFile) return;

            try
            {
                var parts = new System.Collections.Generic.List<string>();
                if (_config.Logging.IncludeTimestamp)
                    parts.Add($"[{DateTime.Now.ToString(_config.Logging.TimestampFormat)}]");
                if (_config.Logging.IncludeSender) parts.Add(sender);
                if (_config.Logging.IncludeMessage) parts.Add(message);

                string logLine = string.Join(": ", parts);
                _logQueue.Enqueue(logLine + "\n" + _config.Logging.Separator + "\n");
            }
            catch { }
        }

        private async Task ProcessQueue()
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    // Используем out string? и проверяем на null
                    if (_logQueue.TryDequeue(out string? line) && line != null)
                    {
                        await WriteToFileAsync(line);
                    }
                    else
                    {
                        await Task.Delay(100, _cts.Token);
                    }
                }
                catch (OperationCanceledException) { break; }
                catch { }
            }

            // Финализирующая выгрузка
            while (_logQueue.TryDequeue(out string? line))
            {
                if (line != null)
                {
                    try { await WriteToFileAsync(line); } catch { }
                }
            }
        }

        private async Task WriteToFileAsync(string content)
        {
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
                await File.AppendAllTextAsync(logFile, content);
            }
            catch { }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _cts.Cancel();
            try { _workerTask.Wait(2000); } catch { }
            _cts.Dispose();
        }
    }
}