using System;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
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

		private void ShowVoiceMenu()
		{
			var voices = _tts.GetVoicesList();
			if (voices.Count == 0)
			{
				Console.WriteLine("\n🎤 Нет доступных голосов TTS.");
				return;
			}

			Console.WriteLine("\n🎤 Доступные голоса (введите номер):");
			for (int i = 0; i < voices.Count; i++)
			{
				string marker = voices[i].IsDefault ? " [текущий]" : "";
				Console.WriteLine($"   {i + 1}. {voices[i].Name} ({voices[i].Culture}){marker}");
			}
			Console.Write("Ваш выбор (1-{0}) или '0' для отмены: ", voices.Count);

			// Читаем строку целиком
			string? input = Console.ReadLine();
			if (string.IsNullOrEmpty(input)) return;

			if (int.TryParse(input, out int num))
			{
				if (num >= 1 && num <= voices.Count)
				{
					if (_tts.SelectVoiceByIndex(num - 1))
					{
						Console.WriteLine($"\n✅ Голос переключён на {voices[num - 1].Name}");
					}
				}
				else if (num == 0)
				{
					Console.WriteLine("\nОтмена.");
				}
				else
				{
					Console.WriteLine("\n❌ Неверный номер.");
				}
			}
			else
			{
				Console.WriteLine("\n❌ Введите число.");
			}
		}

        public async Task HandleKeyboardAsync()
        {
            await Task.Yield();
			//await Task.Run(() => { }); // принудительный async, убирает CS1998
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
                    else if (key.Key.ToString() == muteKey)
                    {
                        _tts.IsMuted = !_tts.IsMuted;
                        Console.WriteLine($"\n🔇 Звук {(_tts.IsMuted ? "ВЫКЛЮЧЕН" : "ВКЛЮЧЕН")}");
                    }
					else if (key.Key == ConsoleKey.H || key.Key == ConsoleKey.F1)   // НОВАЯ КЛАВИША
                    {
                        ShowHelp();
                    }
					else if (key.Key == ConsoleKey.F2)
					{
						ShowVoiceMenu();
					}
					else if (key.Key == ConsoleKey.F3)
					{
						ShowTestMenu();
					}
					else if (key.Key == ConsoleKey.F4)
					{
						RunLearningMode();
					}
					else if (key.Key == ConsoleKey.F12)
					{
						await ManualMessageEntry();
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
		
		private async Task ManualMessageEntry()
		{
			Console.WriteLine("\n📝 Введите сообщение (Enter = отправить, Esc = отмена):");
			Console.Write("> ");
			string? input = Console.ReadLine();
			if (string.IsNullOrWhiteSpace(input))
			{
				Console.WriteLine("   Отмена.");
				return;
			}
			_tts.Speak(input, false);
			await _mqtt.PublishAsync("Ручной ввод", input);
			Console.WriteLine($"   ✅ Отправлено: {input}");
		}
		
        private async Task ReloadConfig()
		{
			Console.WriteLine("\n🔄 Перезагрузка конфигурации...");
			try
			{
				var newConfig = ConfigLoader.Load(suppressExit: true);
				if (newConfig == null)
				{
					Console.WriteLine("   ❌ Ошибка загрузки конфига. Старый конфиг сохранён.");
					Console.Beep(500, 500);
					return;
				}
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
			catch (Exception ex) { Console.WriteLine($"   ❌ Ошибка: {ex.Message}"); Console.Beep(500, 500); }
		}

        private void ShowConfig()
		{
			Console.WriteLine("\n📋 ТЕКУЩАЯ КОНФИГУРАЦИЯ:");
			
			// 1. Общие параметры мониторинга
			Console.WriteLine($"   Интервал проверки: {_config.Monitoring.CheckIntervalSeconds} с");
			Console.WriteLine($"   Режим удаления уведомлений: {_config.NotificationDeletion.DeleteMode}");
			Console.WriteLine($"   Сохранять ID обработанных: {(_config.Monitoring.SaveProcessedIds ? "Да" : "Нет")}");
			Console.WriteLine($"   Максимум хранимых ID: {_config.Monitoring.MaxProcessedIdsCount}");
			
			// 2. Отслеживаемые приложения
			string allowedApps = _config.Monitoring.AllowedApps?.Count > 0
				? string.Join(", ", _config.Monitoring.AllowedApps)
				: "все приложения";
			Console.WriteLine($"   Отслеживаемые приложения: {allowedApps}");
			
			// 3. Фильтрация
			Console.WriteLine($"   Отправители ({_config.Filtering.AllowedSenders.Count}): {string.Join(", ", _config.Filtering.AllowedSenders)}");
			Console.WriteLine($"   Ключевые слова (A): {string.Join(", ", _config.Filtering.ListA)}");
			Console.WriteLine($"   Список Б: {string.Join(", ", _config.Filtering.ListB)}");
			Console.WriteLine($"   Список С (приоритетные): {string.Join(", ", _config.Filtering.ListC)}");
			Console.WriteLine($"   Оператор между A и Б: {_config.Filtering.Operator}");
			Console.WriteLine($"   Регистрозависимость: {(_config.Filtering.CaseSensitive ? "Да" : "Нет")}");
			Console.WriteLine($"   Частичное совпадение отправителей: {(_config.Filtering.PartialMatchSenders ? "Да" : "Нет")}");
			Console.WriteLine($"   Частичное совпадение ключевых слов: {(_config.Filtering.PartialMatchKeywords ? "Да" : "Нет")}");
			
			// 4. Дедупликация
			Console.WriteLine($"   Дедупликация: {(_config.Deduplication.Enabled ? "Вкл" : "Выкл")}");
			if (_config.Deduplication.Enabled)
			{
				Console.WriteLine($"      Окно хранения: {_config.Deduplication.TimeWindowSeconds} с");
				Console.WriteLine($"      Верхний порог: {_config.Deduplication.SimilarityThresholdHigh:P0}");
				Console.WriteLine($"      Нижний порог: {_config.Deduplication.SimilarityThresholdLow:P0}");
				Console.WriteLine($"      Использовать Левенштейна: {(_config.Deduplication.UseLevenshtein ? "Да" : "Нет")}");
				Console.WriteLine($"      Нормализовать время: {(_config.Deduplication.NormalizeTime ? "Да" : "Нет")}");
				Console.WriteLine($"      Логировать дубли: {(_config.Deduplication.LogDuplicates ? "Да" : "Нет")}");
			}
			
			// 5. TTS
			Console.WriteLine($"   TTS: {(_config.VoiceAlert.Enabled ? "Вкл" : "Выкл")}");
			if (_config.VoiceAlert.Enabled)
			{
				Console.WriteLine($"      Громкость: {_stats.CurrentVolume}%");
				Console.WriteLine($"      Текущий голос: {(_tts.GetCurrentVoiceName() ?? "по умолчанию")}");
				Console.WriteLine($"      Голос из конфига: {(_config.VoiceAlert.VoiceName ?? "не задан")}");
				Console.WriteLine($"      Скорость речи: {_config.VoiceAlert.TtsRate}");
				Console.WriteLine($"      Задержка между сообщениями: {_config.VoiceAlert.MinDelayBetweenMessagesMs} мс");
				Console.WriteLine($"      Прерывать предыдущую речь: {(_config.VoiceAlert.AbortPreviousSpeech ? "Да" : "Нет")}");
				Console.WriteLine($"      Озвучивать только при ключевых словах: {(_config.VoiceAlert.SpeakOnlyOnKeywords ? "Да" : "Нет")}");
				Console.WriteLine($"      Произносить имя отправителя: {(_config.VoiceAlert.SpeakSenderName ? "Да" : "Нет")}");
				Console.WriteLine($"      Падение на писк при ошибке: {(_config.VoiceAlert.FallbackBeepOnError ? "Да" : "Нет")}");
			}
			
			// 6. MQTT
			string mqttStatus = _config.Mqtt.Enabled && _config.Additional.MqttEnabled ? "Вкл" : "Выкл";
			Console.WriteLine($"   MQTT: {mqttStatus}");
			if (_config.Mqtt.Enabled && _config.Additional.MqttEnabled)
			{
				Console.WriteLine($"      Брокер: {_config.Mqtt.Broker}:{_config.Mqtt.Port}");
				Console.WriteLine($"      Топик: {_config.Mqtt.Topic}");
				Console.WriteLine($"      ClientId: {_config.Mqtt.ClientId}");
				Console.WriteLine($"      FromNodeId: {_config.Mqtt.FromNodeId}");
				Console.WriteLine($"      ToNodeId: {_config.Mqtt.ToNodeId}");
				Console.WriteLine($"      Канал: {_config.Mqtt.ChannelIndex}");
				Console.WriteLine($"      Макс. байт сообщения: {_config.Mqtt.MaxPayloadBytes}");
				Console.WriteLine($"      Авто-переподключение: {(_config.Mqtt.ReconnectEnabled ? "Вкл" : "Выкл")}");
				Console.WriteLine($"      Тестовый режим (без отправки): {(_config.Mqtt.TestMode ? "Вкл" : "Выкл")}");
				Console.WriteLine($"      Контрольный топик (health): msh/RU/VLK/2/telemetry/");
			}
			
			// 7. Дополнительные параметры
			Console.WriteLine($"   Запрет сна: {(_config.Additional.PreventSleep ? "Вкл" : "Выкл")}");
			Console.WriteLine($"   Запоминать громкость: {(_config.Additional.RememberVolume ? "Да" : "Нет")}");
			Console.WriteLine($"   Горячие клавиши: {(_config.Additional.EnableHotkeys ? "Вкл" : "Выкл")}");
			Console.WriteLine($"   Режим отладки: {(_config.Additional.DebugMode ? "Вкл" : "Выкл")}");
			Console.WriteLine($"   Версия сборки: {System.Reflection.Assembly.GetEntryAssembly()?.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0"}");
		}

		private void RunLearningMode()
		{
			Console.WriteLine("\n🧠 Запуск LearningMode (сбор приложений)...");
			string exePath = "LearningMode.exe";
			if (!File.Exists(exePath))
			{
				Console.WriteLine($"   ❌ Не найден {exePath}. Убедитесь, что утилита находится в той же папке, что и ToastAlert.exe.");
				return;
			}
			try
			{
				var startInfo = new System.Diagnostics.ProcessStartInfo
				{
					FileName = exePath,
					UseShellExecute = true,   // обязательно для нового окна
					CreateNoWindow = false,
					WindowStyle = System.Diagnostics.ProcessWindowStyle.Normal
				};
				System.Diagnostics.Process.Start(startInfo);
			}
			catch (Exception ex)
			{
				Console.WriteLine($"   ⚠️ Ошибка запуска: {ex.Message}");
			}
		}

        private void ShowHelp()
        {
            Console.WriteLine(@"
╔══════════════════════════════════════════════════════════════╗
║         Toast Alert Monitor - Уведомления Windows            ║
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
    F2          - Список голосов TTS / смена голоса
    F3          - Тестовое меню (голос, MQTT)
    F4          - Запустить LearningMode (сбор приложений)
    F12			- Запустить режим ручного ввода сообщения
");
        }

        public void PrintBanner()
		{
			var version = Assembly.GetEntryAssembly()
				?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
				?.InformationalVersion ?? "0.0.0";
			if (version.Contains('+')) version = version.Substring(0, version.IndexOf('+'));

			// Получаем текущий голос TTS (если доступен)
			string currentVoice = _tts.GetCurrentVoiceName() ?? "по умолчанию";

			// Получаем список отслеживаемых приложений
			string allowedApps = _config.Monitoring.AllowedApps?.Count > 0
				? string.Join(", ", _config.Monitoring.AllowedApps)
				: "все приложения";

			Console.WriteLine($@"
		╔══════════════════════════════════════════════════════════════╗
		║        TOAST ALERT MONITOR v{version}                  ║
		║     Мониторинг через центр уведомлений Windows               ║
		╚══════════════════════════════════════════════════════════════╝");
			Console.WriteLine($"   🕐 Запуск: {DateTime.Now:HH:mm:ss}");
			Console.WriteLine($"   📡 Интервал: {_config.Monitoring.CheckIntervalSeconds}с");
			Console.WriteLine($"   🗑️  Удаление: {_config.NotificationDeletion.DeleteMode}");
			Console.WriteLine($"   📱 Каналов (отправителей): {_config.Filtering.AllowedSenders.Count}");
			Console.WriteLine($"   🔑 Ключевых слов (A): {_config.Filtering.ListA.Count}");
			Console.WriteLine($"   🔊 TTS: {(_config.VoiceAlert.Enabled ? "Вкл" : "Выкл")}, голос: {currentVoice}");
			Console.WriteLine($"   🔊 Громкость TTS: {_stats.CurrentVolume}%");
			Console.WriteLine($"   📋 Отслеживаемые приложения: {allowedApps}");
			Console.WriteLine($"   📡 MQTT: {(_config.Mqtt.Enabled && _config.Additional.MqttEnabled ? "Вкл" : "Выкл")}\n");
		}
		
		private void ShowTestMenu()
		{
			Console.WriteLine("\n🧪 МЕНЮ ТЕСТА:");
			Console.WriteLine("   1. Тест голоса (TTS)");
			Console.WriteLine("   2. Тест MQTT связи");
			Console.WriteLine("   3. Тест уведомлений (имитация)");
			Console.WriteLine("   0. Выход");
			Console.Write("Ваш выбор: ");

			string? input = Console.ReadLine();
			if (string.IsNullOrEmpty(input)) return;

			switch (input)
			{
				case "1":
					TestTts();
					break;
				case "2":
					TestMqtt();
					break;
				case "3":
					TestNotification();
					break;
				case "0":
					Console.WriteLine("Отмена.");
					break;
				default:
					Console.WriteLine("❌ Неверный выбор.");
					break;
			}
		}

		private void TestTts()
		{
			Console.WriteLine("\n🔊 Тест голоса...");
			_tts.Speak("Проверка голосового оповещения. Если вы это слышите, всё работает.", false);
			// небольшая задержка, чтобы речь не оборвалась
			Task.Delay(2000).Wait();
			Console.WriteLine("✅ Тест завершён");
		}

		private async void TestMqtt()
		{
			Console.WriteLine("\n📡 Тест MQTT...");
			await _mqtt.PublishAsync("Тест", "Проверка связи через MQTT");
			Console.WriteLine("✅ Тест отправлен (если MQTT включён и подключён)");
		}

		private void TestNotification()
		{
			Console.WriteLine("\n💬 Имитация уведомления...");
			// Вызываем внутреннюю обработку с фейковыми данными (можно через _monitor, но проще просто вывести в консоль)
			Console.WriteLine("   [Тест] Отправитель: Система, Сообщение: Проверка мониторинга");
			// При желании можно сгенерировать фейковое уведомление и пропустить через ProcessMessage, но это сложнее.
		}
    }
}