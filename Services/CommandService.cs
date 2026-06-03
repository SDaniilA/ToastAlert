using System;
using System.Linq;
using System.Threading.Tasks;
using ToastAlert.Config;
using ToastAlert.Models;

namespace ToastAlert.Services
{
    public class CommandService
    {
        private readonly EasterEggService _easterEgg;
        private readonly QuickConfigService _quickConfig;
        private readonly Config.Config _config;
        private readonly Stats _stats;
        private bool _inCommandMode = false;

        public event Action<bool>? CommandModeChanged;

        public CommandService(EasterEggService easterEgg, QuickConfigService quickConfig, Config.Config config, Stats stats)
        {
            _easterEgg = easterEgg;
            _quickConfig = quickConfig;
            _config = config;
            _stats = stats;
        }

        public async Task EnterCommandModeAsync()
        {
            if (_inCommandMode) return;
            _inCommandMode = true;
            CommandModeChanged?.Invoke(true);

            Console.WriteLine();
            Console.Write("[CMD] > ");
            string? input = Console.ReadLine();
            Console.WriteLine();

            if (!string.IsNullOrWhiteSpace(input))
            {
                await ProcessCommandAsync(input.Trim());
            }

            _inCommandMode = false;
            CommandModeChanged?.Invoke(false);
        }

        private async Task ProcessCommandAsync(string command)
        {
            if (command.StartsWith("!"))
            {
                string rest = command.Substring(1);
                // Проверяем специальные команды для пасхалок
                if (rest == "ec")
                {
                    await _easterEgg.SaveDefaultConfigForActiveEgg();
                    return;
                }
                if (rest == "er")
                {
                    await _easterEgg.RestartCurrentEgg();
                    return;
                }

                // Сначала проверяем пасхалку (точное совпадение команды вместе с '!')
                bool handled = await _easterEgg.ActivateAsync(command);
                if (handled) return;

                // Команда !0 – специальная: деактивация пасхалки
                if (rest == "0")
                {
                    await _easterEgg.DeactivateCurrentAsync();
                    Console.WriteLine("   🧹 Режим пасхалки сброшен.");
                    return;
                }

                // Если остаток состоит только из цифр – обрабатываем каждую цифру последовательно
                if (!string.IsNullOrEmpty(rest) && rest.All(char.IsDigit))
                {
                    bool anyExecuted = false;
                    foreach (char c in rest)
                    {
                        if (_quickConfig.Execute(c))
                        {
                            anyExecuted = true;
                        }
                        else
                        {
                            Console.WriteLine($"   ⚠️ Код '{c}' не найден. Пропускаем.");
                        }
                    }
                    if (!anyExecuted)
                    {
                        Console.WriteLine($"   ❌ Неизвестная команда: {command}");
                        Console.WriteLine("   Доступные пасхалки: !ee");
                        Console.WriteLine(_quickConfig.GetHelp());
                    }
                    return;
                }

                // Если не цифры и не пасхалка
                Console.WriteLine($"   ❌ Неизвестная команда: {command}");
                Console.WriteLine("   Доступные пасхалки: !ee");
                Console.WriteLine(_quickConfig.GetHelp());
                return;
            }

            // Системные команды без '!'
            switch (command.ToLower())
            {
                case "cls":
                    Console.Clear();
                    break;
                case "exit":
                    Environment.Exit(0);
                    break;
                case "help":
                    ShowHelp();
                    break;
                case "stats":
                    Console.WriteLine($"\n📊 СТАТИСТИКА: Всего: {_stats.Total}, Озвучено: {_stats.Spoken}, MQTT: {_stats.MqttSent}, Пропущено: {_stats.Skipped}, Громкость: {_stats.CurrentVolume}%");
                    break;
                case "ee":
                    Console.WriteLine("\n🎭 Доступные пасхалки:");
                    Console.WriteLine(_easterEgg.GetEggsList());
                    break;
                default:
                    Console.WriteLine($"   ❌ Неизвестная команда: {command}. Введите 'help' для списка.");
                    break;
            }
        }

        private void ShowHelp()
        {
            Console.WriteLine(@"
Доступные команды:
  cls                – очистить экран
  exit               – выход из программы
  stats              – показать статистику
  ee                 – список пасхалок
  help               – эта справка

Команды с '!':
  !1984, !42 ...     – активировать пасхалку
  !0                 – деактивировать текущую пасхалку
  !123456...         – последовательное выполнение быстрых настроек (цифры)
  !ec                – сохранить конфиг текущей пасхалки (Easter Egg Config)
  !er                – перезагрузить конфиг и перезапустить текущую пасхалку (Easter Egg Reload)

Быстрые настройки (цифры):
  !1 – Mute TTS
  !2 – Переключить TestMode MQTT
  !3 – Громкость +10
  !4 – Громкость -10
  !5 – Вкл/Выкл TTS
  !6 – Вкл/Выкл вывод в консоль

Для выхода из командного режима – Enter без ввода, Esc или повторный хоткей (`).
");
        }

        public bool IsInCommandMode => _inCommandMode;
    }
}