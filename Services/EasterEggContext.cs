using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ToastAlert.Config;
using ToastAlert.Models;

namespace ToastAlert.Services
{
    public class EasterEggContext
    {
        public Config.Config Config { get; }
        public TtsService Tts { get; }
        public Stats Stats { get; }
        public MessageRewriteService RewriteService { get; }
        public EasterEggService? Service { get; set; }   // ссылка на сервис для корректной деактивации
        public Action<string> ConsoleWriteLine { get; set; }
        public Func<string, bool, Task> SpeakAsync { get; set; }
        public CancellationToken CancellationToken { get; }

        public event Func<string, string, Task>? OnNewMessage;

        public EasterEggContext(Config.Config config, TtsService tts, Stats stats, MessageRewriteService rewriteService, CancellationToken cancellationToken)
        {
            Config = config;
            Tts = tts;
            Stats = stats;
            RewriteService = rewriteService;
            CancellationToken = cancellationToken;
            ConsoleWriteLine = Console.WriteLine;
            // Учитываем Mute в пасхалке
            SpeakAsync = async (text, interruptible) =>
            {
                if (Tts.IsMuted) return;
                await Task.Run(() => Tts.Speak(text, false, !interruptible));
            };
        }

        public void RaiseNewMessage(string sender, string message)
        {
            OnNewMessage?.Invoke(sender, message);
        }

        public void SetMessageHandler(Func<string, string, Task> handler)
        {
            OnNewMessage += handler;
        }

        public void RemoveMessageHandler(Func<string, string, Task> handler)
        {
            OnNewMessage -= handler;
        }

        public void RegisterReplacements(IEasterEgg owner, Dictionary<string, string> replacements)
        {
            RewriteService.RegisterReplacements(owner, replacements);
        }

        public void UnregisterReplacements(IEasterEgg owner)
        {
            RewriteService.Unregister(owner);
        }

        public T? LoadJsonConfig<T>(string fileName) where T : new()
        {
            string configDir = Path.Combine(AppContext.BaseDirectory, "EasterEggConfigs");
            string path = Path.Combine(configDir, fileName);
            if (!File.Exists(path)) return new T();
            try
            {
                string json = File.ReadAllText(path);
                return JsonConvert.DeserializeObject<T>(json);
            }
            catch
            {
                return new T();
            }
        }

        public void SaveJsonConfig<T>(string fileName, T config)
        {
            string configDir = Path.Combine(AppContext.BaseDirectory, "EasterEggConfigs");
            if (!Directory.Exists(configDir))
                Directory.CreateDirectory(configDir);
            string path = Path.Combine(configDir, fileName);
            string json = JsonConvert.SerializeObject(config, Formatting.Indented);
            File.WriteAllText(path, json);
        }
    }
}