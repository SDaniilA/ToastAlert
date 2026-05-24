using System;
using System.Reflection;
using System.Threading.Tasks;
using System.IO;
using System.Drawing;
using Microsoft.Win32;
using ToastAlert.Config;
using ToastAlert.Core;
using ToastAlert.Models;
using ToastAlert.Services;
using ToastAlert.UI;
using ToastAlert.Utilities;
using System.Linq;
namespace ToastAlert
{
    class Program
    {
        private static Config.Config? _config;
        private static Stats _stats = new();
        private static TtsService? _tts;
        private static MqttService? _mqtt;
        private static DeduplicationService? _dedup;
        private static LoggerService? _logger;
        private static ConsoleUi? _ui;
        private static NotificationMonitor? _monitor;

        static async Task Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            SetConsoleTitleWithVersion();
            SetConsoleIcon();

            SystemEvents.PowerModeChanged += OnPowerModeChanged;

            _config = ConfigLoader.Load();
            if (_config == null) return;

            _stats.CurrentVolume = _config.VoiceAlert.TtsVolume;
            _tts = new TtsService(_config, _stats);
            _mqtt = new MqttService(_config, _stats);
            _dedup = new DeduplicationService(_config);
            _logger = new LoggerService(_config);
            _ui = new ConsoleUi(_config, _tts, _mqtt, _dedup, _stats);
            _monitor = new NotificationMonitor(_config, _tts, _mqtt, _dedup, _logger, _ui, _stats);

            if (!ParseArgs(args)) return;
            if (_config.Additional.PreventSleep)
                PreventSleep(true);

            _ui.PrintBanner();
            _tts.Initialize();
            _monitor.LoadProcessedIds();
            await _mqtt.InitializeAsync();

            if (_config.Monitoring.StartMinimized)
                Console.WriteLine("🪟 Запуск в свернутом режиме...");

            await _monitor.RunAsync();
        }

        private static void SetConsoleTitleWithVersion()
        {
            var version = Assembly.GetEntryAssembly()
                ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion ?? "0.0.0";
            Console.Title = $"Toast Alert Monitor v{version}";
        }

        private static void SetConsoleIcon()
        {
            try
            {
                var handle = GetConsoleWindow();
                var assembly = Assembly.GetEntryAssembly();
                if (assembly != null)
                {
                    string? exePath = assembly.Location;
                    if (string.IsNullOrEmpty(exePath))
                        exePath = Path.Combine(AppContext.BaseDirectory, assembly.GetName().Name + ".exe");
                    if (File.Exists(exePath))
                    {
                        var icon = Icon.ExtractAssociatedIcon(exePath);
                        if (icon != null)
                            SendMessage(handle, 0x0080, IntPtr.Zero, icon.Handle);
                    }
                }
            }
            catch { }
        }

        private static void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode == PowerModes.Resume)
            {
                Console.WriteLine("\n🔄 Система вышла из сна, пересоздаём listener...");
                _ = Task.Run(async () => await _monitor!.ReinitListenerAsync());
            }
        }

        private static bool ParseArgs(string[] args)
        {
            if (args.Length == 0) return true;
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i].ToLower())
                {
                    case "--help":
                    case "-h":
                        ShowHelp();
                        return false;
                    case "--test":
                        TestTts();
                        return false;
                    case "--list":
                        ShowConfig();
                        return false;
                    case "--add-sender":
                        if (i + 1 < args.Length) AddSender(args[++i]);
                        return false;
                    case "--add-keyword":
                        if (i + 1 < args.Length) AddKeyword(args[++i]);
                        return false;
                    case "--delete-mode":
                        if (i + 1 < args.Length) SetDeleteMode(args[++i]);
                        return false;
                }
            }
            return true;
        }

        private static void ShowHelp()
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
");
        }

        private static void ShowConfig()
        {
            if (_config == null) return;
            Console.WriteLine("\n📋 ТЕКУЩАЯ КОНФИГУРАЦИЯ:");
            Console.WriteLine($"   Отправители: {string.Join(", ", _config.Filtering.AllowedSenders)}");
            Console.WriteLine($"   Ключевые слова (A): {string.Join(", ", _config.Filtering.ListA)}");
            Console.WriteLine($"   Список Б: {string.Join(", ", _config.Filtering.ListB)}");
            Console.WriteLine($"   Список С: {string.Join(", ", _config.Filtering.ListC)}");
            Console.WriteLine($"   Оператор: {_config.Filtering.Operator}");
            Console.WriteLine($"   Режим удаления: {_config.NotificationDeletion.DeleteMode}");
            Console.WriteLine($"   TTS: {(_config.VoiceAlert.Enabled ? "Вкл" : "Выкл")}");
            Console.WriteLine($"   MQTT: {(_config.Mqtt.Enabled ? "Вкл" : "Выкл")}");
            Console.WriteLine($"   Дедупликация: {(_config.Deduplication.Enabled ? "Вкл" : "Выкл")}");
        }

        private static void AddSender(string sender)
        {
            if (!_config!.Filtering.AllowedSenders.Contains(sender))
            {
                _config.Filtering.AllowedSenders.Add(sender);
                ConfigLoader.Save(_config);
                Console.WriteLine($"✅ Добавлен отправитель: {sender}");
            }
            else Console.WriteLine($"❌ Отправитель уже существует: {sender}");
        }

        private static void AddKeyword(string keyword)
        {
            string lowerKeyword = keyword.ToLower();
            if (!_config!.Filtering.ListA.Contains(lowerKeyword))
            {
                _config.Filtering.ListA.Add(lowerKeyword);
                ConfigLoader.Save(_config);
                Console.WriteLine($"✅ Добавлено ключевое слово: {keyword}");
            }
            else Console.WriteLine($"❌ Ключевое слово уже существует: {keyword}");
        }

        private static void SetDeleteMode(string mode)
        {
            var valid = new[] { "never", "immediate", "on_exit" };
            if (valid.Contains(mode.ToLower()))
            {
                _config!.NotificationDeletion.DeleteMode = mode.ToLower();
                ConfigLoader.Save(_config);
                Console.WriteLine($"✅ Режим удаления: {mode}");
            }
            else Console.WriteLine($"❌ Неверный режим. Доступно: {string.Join(", ", valid)}");
        }

        private static void TestTts()
        {
            Console.WriteLine("🔊 Тест голоса...");
            using (var tts = new System.Speech.Synthesis.SpeechSynthesizer())
            {
                tts.SetOutputToDefaultAudioDevice();
                tts.Speak("Проверка голосового оповещения. Если вы это слышите, всё работает.");
            }
            Console.WriteLine("✅ Тест завершён");
        }

        private static void PreventSleep(bool prevent)
        {
            const uint ES_CONTINUOUS = 0x80000000;
            const uint ES_SYSTEM_REQUIRED = 0x00000001;
            if (prevent)
                SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED);
            else
                SetThreadExecutionState(ES_CONTINUOUS);
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        static extern uint SetThreadExecutionState(uint esFlags);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern IntPtr GetConsoleWindow();

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
    }
}