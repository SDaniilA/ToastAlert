using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using ToastAlert.Config;
using ToastAlert.Models;

namespace ToastAlert.Services
{
    public class EasterEggService
    {
        private readonly Dictionary<string, IEasterEgg> _eggs = new();
        private IEasterEgg? _activeEgg;
        private readonly Config.Config _config;
        private readonly TtsService _tts;
        private readonly Stats _stats;
        private readonly CancellationToken _cancellationToken;
        private EasterEggContext? _context;
        private readonly MessageRewriteService _rewriteService;

        public EasterEggService(Config.Config config, TtsService tts, Stats stats, MessageRewriteService rewriteService, CancellationToken cancellationToken)
        {
            _config = config;
            _tts = tts;
            _stats = stats;
            _rewriteService = rewriteService;
            _cancellationToken = cancellationToken;
            DiscoverEasterEggs();
        }

        private void DiscoverEasterEggs()
        {
            var eggTypes = Assembly.GetExecutingAssembly().GetTypes()
                .Where(t => typeof(IEasterEgg).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

            foreach (var type in eggTypes)
            {
                try
                {
                    var egg = (IEasterEgg)Activator.CreateInstance(type)!;
                    _eggs[egg.Command] = egg;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Ошибка загрузки пасхалки {type.Name}: {ex.Message}");
                }
            }
        }

        public string GetEggsList()
        {
            if (_eggs.Count == 0) return "   Нет доступных пасхалок.";
            return string.Join("\n", _eggs.Values.Select(e => $"   {e.Command} – {e.Name}: {e.Description}"));
        }

        public async Task<bool> ActivateAsync(string command)
        {
            if (!_eggs.TryGetValue(command, out var egg))
                return false;

            if (_activeEgg != null)
                await _activeEgg.DeactivateAsync();

            if (_context == null)
            {
                _context = new EasterEggContext(_config, _tts, _stats, _rewriteService, _cancellationToken);
                _context.Service = this;   // передаём ссылку на сервис
            }

            await egg.ActivateAsync(_context);
            _activeEgg = egg;
            return true;
        }

        public async Task DeactivateCurrentAsync()
        {
            if (_activeEgg != null)
            {
                await _activeEgg.DeactivateAsync();
                _activeEgg = null;
            }
        }

        public bool IsAnyActive => _activeEgg != null;
        public bool ShouldBlockExit => IsAnyActive;

        public async Task SaveDefaultConfigForActiveEgg()
        {
            if (_activeEgg == null)
            {
                Console.WriteLine("   ❌ Нет активной пасхалки. Сначала активируйте пасхалку (например, !1984).");
                return;
            }
            if (_context == null) return;
            var config = _activeEgg.GetDefaultConfig() as EasterEggConfig;
            if (config != null)
            {
                _context.SaveJsonConfig(_activeEgg.Command + ".json", config);
                Console.WriteLine($"✅ Конфиг для {_activeEgg.Command} сохранён в EasterEggConfigs/{_activeEgg.Command}.json");
            }
            await Task.CompletedTask;
        }

        public async Task RestartCurrentEgg()
        {
            if (_activeEgg == null)
            {
                Console.WriteLine("   ❌ Нет активной пасхалки.");
                return;
            }
            string command = _activeEgg.Command;
            await DeactivateCurrentAsync();
            await ActivateAsync(command);
        }
    }
}