using System;
using System.IO;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace ToastAlert.Config
{
    public static class ConfigLoader
    {
        private static readonly object _lock = new object();

        public static Config Load(string configPath = "config.json")
        {
            string backupPath = configPath + ".bak";

            if (!File.Exists(configPath))
            {
                var defaultCfg = GetDefault();
                Save(defaultCfg, configPath);
                return defaultCfg;
            }

            try
            {
                var json = File.ReadAllText(configPath);
                var cfg = JsonConvert.DeserializeObject<Config>(json);
                if (cfg != null)
                {
                    SetDefaults(cfg);
                    return cfg;
                }
                else
                {
                    Console.WriteLine("⚠️ Не удалось десериализовать config.json. Пытаемся восстановить из .bak...");
                    if (File.Exists(backupPath))
                    {
                        json = File.ReadAllText(backupPath);
                        cfg = JsonConvert.DeserializeObject<Config>(json);
                        if (cfg != null)
                        {
                            Console.WriteLine("✅ Восстановлено из config.json.bak");
                            SetDefaults(cfg);
                            Save(cfg, configPath);
                            return cfg;
                        }
                    }
                    throw new InvalidOperationException("Не удалось загрузить конфигурацию ни из основного файла, ни из бэкапа.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Критическая ошибка загрузки конфига: {ex.Message}");
                Console.WriteLine("Нажмите любую клавишу для выхода...");
                Console.ReadKey();
                Environment.Exit(1);
                return null!;
            }
        }

        public static void Save(Config config, string configPath = "config.json")
        {
            lock (_lock)
            {
                string tempPath = configPath + ".tmp";
                string backupPath = configPath + ".bak";
                try
                {
                    var json = JsonConvert.SerializeObject(config, Formatting.Indented);
                    File.WriteAllText(tempPath, json);
                    if (File.Exists(backupPath)) File.Delete(backupPath);
                    if (File.Exists(configPath)) File.Move(configPath, backupPath);
                    File.Move(tempPath, configPath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Ошибка сохранения конфига: {ex.Message}");
                }
            }
        }

        private static void SetDefaults(Config cfg)
        {
            cfg.TextCleaning ??= new CleanTextConfig();
            cfg.Abbreviations ??= new AbbrevConfig();
            cfg.Priorities ??= new PriorityConfig();
            cfg.Abbreviations.CustomAbbreviations ??= new Dictionary<string, string>();
            cfg.Filtering.ListB ??= new List<string>();
            cfg.Filtering.ListC ??= new List<string>();
            if (string.IsNullOrEmpty(cfg.Filtering.Operator)) cfg.Filtering.Operator = "И";
            cfg.Filtering.BlacklistedSenders ??= new List<string>();
            cfg.Filtering.BlacklistedKeywords ??= new List<string>();
            cfg.TextCleaning.CustomAdsToRemove ??= new List<string>();
            cfg.Mqtt ??= new MqttConfig();
            if (cfg.Mqtt.MaxPayloadBytes == 0) cfg.Mqtt.MaxPayloadBytes = 100;
            cfg.Deduplication ??= new DedupConfig();
            if (string.IsNullOrEmpty(cfg.Mqtt.ClientId)) cfg.Mqtt.ClientId = $"telegram_alert_{Environment.MachineName}";
        }

        private static Config GetDefault()
        {
            return new Config
            {
                Monitoring = new MonitorConfig(),
                Filtering = new FilterConfig
                {
                    AllowedSenders = new List<string>(),
                    ListA = new List<string>(),
                    ListB = new List<string>(),
                    ListC = new List<string>()
                },
                VoiceAlert = new TtsConfig
                {
                    Enabled = true,
                    TtsVolume = 30,
                    SpeakOnlyOnKeywords = true,
                    SuffixText = "От канала"
                },
                NotificationDeletion = new DeleteConfig { DeleteMode = "on_exit" },
                TextCleaning = new CleanTextConfig
                {
                    RemoveAds = true,
                    CustomAdsToRemove = new List<string> { "Резервный канал", "подпишись", "Telegram" }
                },
                Abbreviations = new AbbrevConfig
                {
                    Enabled = true,
                    CustomAbbreviations = new Dictionary<string, string>
                    {
                        { "ФПВ", "Фи Пи Ви" },
                        { "ЧС", "Чэ Эс" },
                        { "МЧС", "Эм Чэ Эс" },
                        { "МО", "Эм О" },
                        { "ЧП", "Чэ Пэ" },
                    }
                },
                Logging = new LogConfig(),
                Sounds = new SoundConfig(),
                Additional = new ExtraConfig { MqttEnabled = true },
                Priorities = new PriorityConfig { PriorityKeywords = new List<string> {} },
                HealthCheck = new HealthConfig(),
                Mqtt = new MqttConfig { Enabled = false },
                Deduplication = new DedupConfig()
            };
        }
    }
}
