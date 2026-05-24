using System;
using System.Threading;
using System.Threading.Tasks;
using ToastAlert.Config;
using ToastAlert.Models;
using ToastAlert.Services;

namespace ToastAlert.UI
{
    public class ConsoleUi
    {
        private readonly Config.Config _config;
        private readonly TtsService _tts;
        private readonly MqttService _mqtt;
        private readonly DeduplicationService _dedup;
        private readonly Stats _stats;
        private bool _isRunning = true;
        private CancellationTokenSource _cts = new();

        public ConsoleUi(Config.Config config, TtsService tts, MqttService mqtt, DeduplicationService dedup, Stats stats)
        {
            _config = config;
            _tts = tts;
            _mqtt = mqtt;
            _dedup = dedup;
            _stats = stats;
        }

        public async Task HandleKeyboardAsync()
        {
            await Task.Run(() => { }); // принудительный async, убирает CS1998
            while (_isRunning && !_cts.Token.IsCancellationRequested)
            {
                var key = Console.ReadKey(true);
                string statsKey = _config.Additional.HotkeyStats.ToUpper();
                string configKey = _config.Additional.HotkeyConfig.ToUpper();
                string muteKey = _config.Additional.HotkeyMute.ToUpper();

                try
                {
                    if (key.Key.ToString() == statsKey)
                    {
                        Console.WriteLine($"\n📊 СТАТИСТИКА: Всего: {_stats.Total}, Озвучено: {_stats.Spoken}, MQTT: {_stats.MqttSent}, Пропущено: {_stats.Skipped}, Громкость: {_stats.CurrentVolume}%, Звук: {(_tts.IsMuted ? "🔇 Выкл" : "🔊 Вкл")}");
                    }
                    else if (key.Key.ToString() == configKey)
                    {
                        ShowConfig();
                    }
                    else if (key.Key == ConsoleKey.OemPlus || key.Key == ConsoleKey.Add)
                    {
                        _tts.ChangeVolume(10);
                        if (_config.Additional.RememberVolume)
                            ConfigLoader.Save(_config);
                    }
                    else if (key.Key == ConsoleKey.OemMinus || key.Key == ConsoleKey.Subtract)
                    {
                        _tts.ChangeVolume(-10);
                        if (_config.Additional.RememberVolume)
                            ConfigLoader.Save(_config);
                    }
                    else if (key.Key == ConsoleKey.R)
                    {
                        await ReloadConfig();
                    }
                    else if (key.Key == ConsoleKey.Q)
                    {
                        await _mqtt.ToggleAsync();
                    }
                    else if (key.Key == ConsoleKey.D)
                    {
                        _dedup.ClearBuffer();
                        Console.WriteLine("\n🧹 Буфер дедупликации очищен");
                    }
                    else if (key.Key == ConsoleKey.H || key.Key == ConsoleKey.F1)   // НОВАЯ КЛАВИША
                    {
                        ShowHelp();
                    }
                    else if (key.Key.ToString() == muteKey)
                    {
                        _tts.IsMuted = !_tts.IsMuted;
                        Console.WriteLine($"\n🔇 Звук {(_tts.IsMuted ? "ВЫКЛЮЧЕН" : "ВКЛЮЧЕН")}");
                    }
                    else if (key.Key == ConsoleKey.Escape)
                    {
                        _isRunning = false;
                        _cts.Cancel();
                        Environment.Exit(0);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"\n⚠️ Ошибка в обработчике клавиш: {ex.Message}");
                }
            }
            await Task.CompletedTask;
        }

        private async Task ReloadConfig()
        {
            Console.WriteLine("\n🔄 Перезагрузка конфигурации...");
            try
            {
                var newConfig = ConfigLoader.Load();
                if (newConfig != null)
                {
                    _config.Monitoring = newConfig.Monitoring;
                    _config.Filtering = newConfig.Filtering;
                    _config.VoiceAlert = newConfig.VoiceAlert;
                    _config.NotificationDeletion = newConfig.NotificationDeletion;
                    _config.TextCleaning = newConfig.TextCleaning;
                    _config.Abbreviations = newConfig.Abbreviations;
                    _config.Logging = newConfig.Logging;
                    _config.Sounds = newConfig.Sounds;
                    _config.Additional = newConfig.Additional;
                    _config.Priorities = newConfig.Priorities;
                    _config.Mqtt = newConfig.Mqtt;
                    _config.HealthCheck = newConfig.HealthCheck;
                    _config.Deduplication = newConfig.Deduplication;

                    _tts.Dispose();
                    _tts.Initialize();

                    Console.WriteLine($"   ✅ Конфиг перезагружен");
                    Console.Beep(1000, 100);
                    Console.Beep(1200, 100);
                }
            }
            catch (Exception ex) { Console.WriteLine($"   ❌ Ошибка: {ex.Message}"); Console.Beep(500, 500); }
        }

        private void ShowConfig()
        {
            Console.WriteLine("\n📋 ТЕКУЩАЯ КОНФИГУРАЦИЯ:");
            Console.WriteLine($"   Отправители ({_config.Filtering.AllowedSenders.Count}): {string.Join(", ", _config.Filtering.AllowedSenders)}");
            Console.WriteLine($"   Ключевые слова (A): {string.Join(", ", _config.Filtering.ListA)}");
            Console.WriteLine($"   Список Б: {string.Join(", ", _config.Filtering.ListB)}");
            Console.WriteLine($"   Список С: {string.Join(", ", _config.Filtering.ListC)}");
            Console.WriteLine($"   Режим удаления: {_config.NotificationDeletion.DeleteMode}");
            Console.WriteLine($"   TTS: {(_config.VoiceAlert.Enabled ? "Вкл" : "Выкл")}, громкость: {_stats.CurrentVolume}%");
            Console.WriteLine($"   MQTT: {(_config.Mqtt.Enabled ? "Вкл" : "Выкл")}");
            Console.WriteLine($"   Дедупликация: {(_config.Deduplication.Enabled ? "Вкл" : "Выкл")}");
        }

        private void ShowHelp()
        {
            Console.WriteLine(@"
╔══════════════════════════════════════════════════════════════╗
║         Toast Alert Monitor - Уведомления Windows           ║
╚══════════════════════════════════════════════════════════════╝

ИСПОЛЬЗОВАНИЕ:
    ToastAlert.exe [опции]

ОПЦИИ:
    --help               Показать справку
    --test               Тест голоса
    --list               Показать конфигурацию
    --add-sender <имя>   Добавить отправителя
    --add-keyword <слово> Добавить ключевое слово
    --delete-mode <mode> never/immediate/on_exit

ГОРЯЧИЕ КЛАВИШИ:
    Ctrl+C      - Остановка
    S           - Статистика
    C           - Конфигурация
    M           - Вкл/Выкл звук
    + / -       - Увеличение/уменьшение громкости TTS
    R           - Перезагрузка config.json без перезапуска
    Q           - Вкл/Выкл MQTT
    D           - Очистить буфер дедупликации
    H / F1      - Показать эту справку
");
        }

        public void PrintBanner()
        {
            Console.WriteLine(@"
╔══════════════════════════════════════════════════════════════╗
║        TOAST ALERT MONITOR v13.0 - Refactored v1             ║
║     Мониторинг через центр уведомлений Windows               ║
╚══════════════════════════════════════════════════════════════╝");
            Console.WriteLine($"   🕐 Запуск: {DateTime.Now:HH:mm:ss}");
            Console.WriteLine($"   📡 Интервал: {_config.Monitoring.CheckIntervalSeconds}с");
            Console.WriteLine($"   🗑️  Удаление: {_config.NotificationDeletion.DeleteMode}");
            Console.WriteLine($"   📱 Каналов: {_config.Filtering.AllowedSenders.Count}");
            Console.WriteLine($"   🔑 Ключевых слов: {_config.Filtering.ListA.Count}");
            Console.WriteLine($"   🔊 TTS: {(_config.VoiceAlert.Enabled ? "Вкл" : "Выкл")}");
            Console.WriteLine($"   🔊 Громкость TTS: {_stats.CurrentVolume}%");
            Console.WriteLine($"   📡 MQTT: {(_config.Mqtt.Enabled && _config.Additional.MqttEnabled ? "Вкл" : "Выкл")}\n");
        }
    }
}