using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;
using ToastAlert.Config;
using ToastAlert.Models;
using ToastAlert.Services;
using ToastAlert.Utilities;
using Microsoft.Win32;
using ToastAlert.UI;
using System.Runtime.InteropServices;

namespace ToastAlert.Core
{
    public class NotificationMonitor
    {
        private readonly Config.Config _config;
        private readonly TtsService _tts;
        private readonly MqttService _mqtt;
        private readonly DeduplicationService _dedup;
        private readonly LoggerService _logger;
        private readonly ConsoleUi _ui;
        private readonly Stats _stats;

        private UserNotificationListener? _listener;
        private HashSet<uint> _processedIds = new();
        private readonly object _processedIdsLock = new();
        private ConcurrentQueue<uint> _pendingDeletes = new();
        private DateTime _startTime;
        private bool _isRunning = true;
        private CancellationTokenSource _cts = new();

        public NotificationMonitor(Config.Config config, TtsService tts, MqttService mqtt, DeduplicationService dedup, LoggerService logger, ConsoleUi ui, Stats stats)
        {
            _config = config;
            _tts = tts;
            _mqtt = mqtt;
            _dedup = dedup;
            _logger = logger;
            _ui = ui;
            _stats = stats;
        }

        public void LoadProcessedIds()
        {
            if (!_config.Monitoring.SaveProcessedIds) return;
            try
            {
                string filePath = _config.Monitoring.ProcessedIdsFile;
                if (System.IO.File.Exists(filePath))
                {
                    var json = System.IO.File.ReadAllText(filePath);
                    var ids = Newtonsoft.Json.JsonConvert.DeserializeObject<uint[]>(json);
                    if (ids != null)
                    {
                        lock (_processedIdsLock) { _processedIds = new HashSet<uint>(ids); }
                    }
                    Console.WriteLine($"📊 Загружено ID: {_processedIds.Count}\n");
                }
            }
            catch { }
        }

        private void SaveProcessedIds()
        {
            if (!_config.Monitoring.SaveProcessedIds) return;
            try
            {
                uint[] idsCopy;
                lock (_processedIdsLock)
                {
                    if (_processedIds.Count > _config.Monitoring.MaxProcessedIdsCount)
                    {
                        var lastIds = _processedIds.TakeLast(_config.Monitoring.MaxProcessedIdsCount / 2).ToArray();
                        _processedIds = new HashSet<uint>(lastIds);
                    }
                    idsCopy = _processedIds.ToArray();
                }
                System.IO.File.WriteAllText(_config.Monitoring.ProcessedIdsFile, Newtonsoft.Json.JsonConvert.SerializeObject(idsCopy));
            }
            catch { }
        }

        public async Task RunAsync()
        {
            try
            {
                _listener = UserNotificationListener.Current;
                Console.WriteLine("🔐 Запрос доступа к уведомлениям...");
                var accessStatus = await _listener.RequestAccessAsync();
                if (accessStatus != UserNotificationListenerAccessStatus.Allowed)
                {
                    Console.WriteLine("\n❌ НЕТ ДОСТУПА К УВЕДОМЛЕНИЯМ!");
                    Console.WriteLine("Разрешите доступ в Настройках → Конфиденциальность → Уведомления и перезапустите.");
                    Console.ReadKey();
                    return;
                }
                Console.WriteLine("✅ ДОСТУП ПОЛУЧЕН\n");
                lock (_processedIdsLock) Console.WriteLine($"📊 Уже обработано: {_processedIds.Count} уведомлений\n");
                Console.WriteLine("🔄 МОНИТОРИНГ АКТИВЕН (Ctrl+C = стоп)\n");
                _startTime = DateTime.Now;

                // Запуск обработки горячих клавиш
                _ = Task.Run(_ui.HandleKeyboardAsync);

                int statsCounter = 0;
                while (_isRunning && !_cts.Token.IsCancellationRequested)
                {
                    await CheckNotificationsAsync();
                    statsCounter++;
                    int statsInterval = _config.Logging.StatsLogIntervalMinutes * 60 / _config.Monitoring.CheckIntervalSeconds;
                    if (statsCounter >= statsInterval && statsInterval > 0)
                    {
                        var uptime = (DateTime.Now - _startTime).TotalHours;
                        if (_config.Logging.LogToConsole)
                            Console.WriteLine($"\n📊 {uptime:F1}ч | Всего: {_stats.Total} | Озвучено: {_stats.Spoken} | Пропущено: {_stats.Skipped}");
                        statsCounter = 0;
                    }
                    await Task.Delay(_config.Monitoring.CheckIntervalSeconds * 1000, _cts.Token);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ Ошибка: {ex.Message}");
                if (_config.Additional.DebugMode) Console.WriteLine(ex.StackTrace);
            }
            finally
            {
                await CleanupAsync();
            }
        }

        private async Task CheckNotificationsAsync()
        {
            try
            {
                if (_listener == null) return;
                var notifications = await _listener.GetNotificationsAsync(NotificationKinds.Toast);
                foreach (var notif in notifications)
                {
                    uint id = notif.Id;
                    bool alreadyProcessed;
                    lock (_processedIdsLock) { alreadyProcessed = _processedIds.Contains(id); }
                    if (alreadyProcessed) continue;

                    string appName = notif.AppInfo.DisplayInfo.DisplayName;
                    if (string.IsNullOrEmpty(appName) || !appName.Contains("Telegram", StringComparison.OrdinalIgnoreCase)) continue;

                    var (sender, message) = ParseNotification(notif);
                    if (string.IsNullOrEmpty(sender) || string.IsNullOrEmpty(message)) continue;

                    await ProcessMessageAsync(id, sender, message);
                }
            }
            catch (UnauthorizedAccessException)
            {
                Console.WriteLine("⚠️ Потерян доступ к уведомлениям. Восстанавливаем...");
                await ReinitListenerAsync();
            }
            catch (COMException)
            {
                Console.WriteLine("⚠️ Ошибка COM при доступе к уведомлениям. Восстанавливаем...");
                await ReinitListenerAsync();
            }
            catch (Exception ex)
            {
                if (_config.Additional.DebugMode)
                    Console.WriteLine($"⚠️ Ошибка проверки: {ex.Message}");
            }
        }

        private (string? sender, string? message) ParseNotification(UserNotification notif)
        {
            try
            {
                var binding = notif.Notification.Visual.GetBinding(KnownNotificationBindings.ToastGeneric);
                if (binding == null) return (null, null);
                var texts = binding.GetTextElements();
                if (texts == null || texts.Count < 2) return (null, null);
                string sender = texts[0]?.Text?.Trim() ?? "";
                string message = texts[1]?.Text?.Trim() ?? "";
                message = StringUtilities.CleanMessage(message, _config);
                if (string.IsNullOrWhiteSpace(message) ||
                    message.Equals("новое сообщение", StringComparison.OrdinalIgnoreCase) ||
                    message.Equals("New message", StringComparison.OrdinalIgnoreCase))
                    return (null, null);
                return (sender, message);
            }
            catch { return (null, null); }
        }

        private async Task ProcessMessageAsync(uint id, string sender, string message)
        {
            Interlocked.Increment(ref _stats.Total);
            var cfg = _config;

            if (cfg.Monitoring.ConsoleOutputEnabled)
            {
                Console.WriteLine($"\n📨 [{_stats.Total}] {sender}");
                string shortMsg = cfg.Monitoring.ConsoleMaxMessageLength > 0 && message.Length > cfg.Monitoring.ConsoleMaxMessageLength
                    ? message.Substring(0, cfg.Monitoring.ConsoleMaxMessageLength)
                    : message;
                Console.WriteLine($"   {shortMsg}");
            }

            // Чёрный список отправителей
            if (IsSenderBlacklisted(sender))
            {
                if (cfg.Monitoring.ConsoleOutputEnabled) Console.WriteLine($"   ⛔ Черный список");
                Interlocked.Increment(ref _stats.Skipped);
                lock (_processedIdsLock) { _processedIds.Add(id); }
                return;
            }

            // Дедупликация
            if (_dedup.IsDuplicate(message, sender, out double sim, out string dupSender, out _))
            {
                if (cfg.Deduplication.LogDuplicates && cfg.Monitoring.ConsoleOutputEnabled)
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"   ⏭️ ДУБЛЬ ({sim:P0}) от {dupSender}");
                    Console.ResetColor();
                }
                Interlocked.Increment(ref _stats.Skipped);
                lock (_processedIdsLock) { _processedIds.Add(id); }
                return;
            }
            _dedup.AddToBuffer(message, sender, message);

            bool isAllowed = IsSenderAllowed(sender);
            bool matchesFilter = MatchesFilters(message);
            bool hasPriority = HasPriorityKeywords(message);
            bool shouldSpeak = false;

            if (isAllowed && matchesFilter)
            {
                _logger.LogMessage(sender, message);
                await _mqtt.PublishAsync(sender, message);
            }

            if (isAllowed)
                shouldSpeak = cfg.VoiceAlert.SpeakOnlyOnKeywords ? matchesFilter : true;

            if (shouldSpeak && !_tts.IsMuted && _tts.CanSpeakNow())
            {
                string speakText = BuildSpeechText(sender, message);
                _tts.Speak(speakText, hasPriority);
                _tts.UpdateLastSpeechTime();
                Interlocked.Increment(ref _stats.Spoken);
                if (cfg.Monitoring.ConsoleOutputEnabled)
                    Console.WriteLine($"   🔊 ОЗВУЧЕНО!{(matchesFilter ? " 🔥 КЛЮЧЕВОЕ СЛОВО!" : "")}");
                if (cfg.Monitoring.ShowPopupNotifications)
                    ShowPopupNotification(sender, message);
            }
            else
            {
                Interlocked.Increment(ref _stats.Skipped);
                if (cfg.Monitoring.ConsoleOutputEnabled)
                {
                    if (!isAllowed) Console.WriteLine($"   ⏭️ Отправитель не в списке");
                    else if (_tts.IsMuted) Console.WriteLine($"   🔇 Звук отключен (M)");
                    else if (!shouldSpeak) Console.WriteLine($"   ⏭️ Нет ключевых слов");
                    else if (!_tts.CanSpeakNow()) Console.WriteLine($"   ⏳ Задержка {cfg.VoiceAlert.MinDelayBetweenMessagesMs}мс");
                }
            }

            lock (_processedIdsLock) { _processedIds.Add(id); }

            if (cfg.NotificationDeletion.DeleteMode == "immediate")
            {
                try { _listener?.RemoveNotification(id); } catch { }
                if (cfg.Monitoring.ConsoleOutputEnabled) Console.WriteLine($"   🗑️ Удалено");
            }
            else if (cfg.NotificationDeletion.DeleteMode == "on_exit")
                _pendingDeletes.Enqueue(id);

            if (_stats.Total % 10 == 0 && cfg.Monitoring.SaveProcessedIds)
                SaveProcessedIds();
        }

        private bool IsSenderAllowed(string sender)
        {
            var allowed = _config.Filtering.AllowedSenders;
            if (allowed.Count == 0) return true;
            bool partial = _config.Filtering.PartialMatchSenders;
            bool cs = _config.Filtering.CaseSensitive;
            var comp = cs ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            foreach (var a in allowed)
                if (partial ? sender.Contains(a, comp) : string.Equals(sender, a, comp))
                    return true;
            return false;
        }

        private bool IsSenderBlacklisted(string sender)
        {
            var black = _config.Filtering.BlacklistedSenders;
            if (black.Count == 0) return false;
            bool partial = _config.Filtering.PartialMatchSenders;
            bool cs = _config.Filtering.CaseSensitive;
            var comp = cs ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            foreach (var b in black)
                if (partial ? sender.Contains(b, comp) : string.Equals(sender, b, comp))
                    return true;
            return false;
        }

        private bool MatchesFilters(string message)
        {
            var filter = _config.Filtering;
            var listA = filter.ListA;
            var listB = filter.ListB;
            var listC = filter.ListC;
            string op = filter.Operator;
            bool cs = filter.CaseSensitive;
            bool partial = filter.PartialMatchKeywords;
            string msg = cs ? message : message.ToLower();

            bool CheckList(List<string> list)
            {
                if (list.Count == 0) return true;
                foreach (var kw in list)
                {
                    string kwc = cs ? kw : kw.ToLower();
                    if (partial ? msg.Contains(kwc) : msg == kwc) return true;
                }
                return false;
            }

            if (listC.Count > 0 && CheckList(listC)) return true;
            bool a = CheckList(listA);
            bool b = CheckList(listB);
            if (listA.Count == 0 && listB.Count == 0) return true;
            return op == "И" ? a && b : a || b;
        }

        private bool HasPriorityKeywords(string message)
        {
            var pk = _config.Priorities.PriorityKeywords;
            if (pk.Count == 0) return false;
            string msgLow = message.ToLower();
            return pk.Any(kw => msgLow.Contains(kw.ToLower()));
        }

        private string BuildSpeechText(string sender, string message)
        {
            var vc = _config.VoiceAlert;
            var parts = new List<string>();
            if (vc.SpeakTime) parts.Add(SpeechUtilities.FormatTimeForSpeech(DateTime.Now, vc.TimeFormat));
            if (!string.IsNullOrEmpty(vc.PrefixText)) parts.Add(vc.PrefixText);
            if (vc.SpeakFullMessage && !vc.SpeakOnlyTitle)
            {
                string msg = message;
                if (msg.Length > vc.MaxMessageLength) msg = msg.Substring(0, vc.MaxMessageLength);
                parts.Add(StringUtilities.NormalizeTextForSpeech(msg, _config));
            }
            if (!string.IsNullOrEmpty(vc.SuffixText)) parts.Add(vc.SuffixText);
            if (vc.SpeakSenderName) parts.Add(sender);
            return string.Join(". ", parts);
        }

        private void ShowPopupNotification(string sender, string message)
        {
            try
            {
                string shortMsg = message.Length > 50 ? message.Substring(0, 50) + "..." : message;
                Console.WriteLine($"   💬 [Уведомление] {sender}: {shortMsg}");
            }
            catch { }
        }

        public async Task ReinitListenerAsync()
        {
            try
            {
                _listener = null;
                _listener = UserNotificationListener.Current;
                var status = await _listener.RequestAccessAsync();
                if (status == UserNotificationListenerAccessStatus.Allowed)
                {
                    Console.WriteLine("✅ Доступ к уведомлениям восстановлен");
                    await Task.Delay(2000);
                    lock (_processedIdsLock) { _processedIds.Clear(); }
                }
                else Console.WriteLine("⚠️ Не удалось восстановить доступ");
            }
            catch (Exception ex) { Console.WriteLine($"❌ Ошибка восстановления listener: {ex.Message}"); }
        }

        private async Task CleanupAsync()
        {
            _isRunning = false;
            _cts.Cancel();
            Console.WriteLine("\n🧹 Остановка и очистка...");
            await _mqtt.DisconnectAsync();
            if (_config.NotificationDeletion.DeleteMode == "on_exit")
            {
                int deleted = 0;
                while (_pendingDeletes.TryDequeue(out uint id))
                {
                    try { _listener?.RemoveNotification(id); deleted++; } catch { }
                }
                Console.WriteLine($"   ✅ Удалено: {deleted}");
            }
            SaveProcessedIds();
            _tts.Dispose();
            Console.WriteLine($"\n📊 ИТОГОВАЯ СТАТИСТИКА: Всего: {_stats.Total}, Озвучено: {_stats.Spoken}, Пропущено: {_stats.Skipped}");
            Console.WriteLine("✅ Завершено.");
        }
    }
}
