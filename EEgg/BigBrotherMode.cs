using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ToastAlert.Services;

namespace ToastAlert.EEgg
{
    public class BigBrotherMode : IEasterEgg
    {
        public string Command => "!1984";
        public string Name => "Режим Большого брата";
        public string Description => "Антиутопический режим с лозунгами, помехами и перехватом выхода";
        public bool IsActive { get; private set; }

        private EasterEggContext? _context;
        private Timer? _propagandaTimer;
        private (ConsoleColor Foreground, ConsoleColor Background) _originalColors;
        private string _originalTitle = string.Empty;

        private List<(string Text, int Weight)> _slogansWithWeights = new();
        private string? _lastSlogan;
        private readonly Random _random = new();

        // Встроенные лозунги по умолчанию
        private readonly List<(string Text, int Weight)> _defaultSlogans = new()
        {
            ("Большой брат следит за тобой", 7),
            ("Свобода — это рабство", 5),
            ("Незнание — сила", 5),
            ("Война — это мир", 5),
            ("2 + 2 = 5", 4),
            ("Кто управляет прошлым, тот управляет будущим", 3),
            ("Министерство мира занимается войной", 3),
            ("Любовь к Большому Брату — это конец индивидуальности", 2),
            ("Если вы хотите сохранить тайну, вы должны скрыть её от себя", 2),
            ("Власть — это цель, а не средство", 2),
            ("Партия — это народ", 2),
            ("Двоемыслие — это способность удерживать в уме два противоположных убеждения", 2),
            ("Мы встретимся в месте, где нет тьмы", 2),
            ("Признание — не предательство. Что ты сказал или не сказал — не важно", 1),
            ("Если падаешь с высоты, схватиться за веревку — не трусость", 1),
            ("Массы никогда не восстают сами по себе", 1),
            ("Невозможно построить цивилизацию на страхе, ненависти и жестокости", 1),
            ("Если мысль извращает язык, то и язык может повредить нашим мыслям", 1),
            ("Если ты в меньшинстве — это не значит, что ты безумен", 1),
            ("О жестокие, ненужные размолвки!", 1),
            ("Они никогда не взбунтуются, пока не станут сознательными, а сознательными не станут, пока не взбунтуются.", 1),
            ("В нашем мире не будет иных чувств, кроме страха, гнева, торжества", 1),
            ("То, что истинно сейчас, истинно от века и на веки вечные", 1),
            ("Партия сказала тебе отвергнуть свидетельства собственных глаз и ушей. Это был их последний, самый главный приказ", 1),
            ("Свобода — это возможность говорить, что дважды два — четыре. Если это дозволено, все остальное отсюда следует само собой", 1),
            ("Сила — в разрывании человеческих умов на части и в складывании их заново в новые формы по собственному выбору", 1),
            ("Партию нельзя было свергнуть изнутри. ... Но пролы, если бы они только могли как-то осознать собственную силу, не нуждались бы в заговоре. Им нужно только подняться и стряхнуть с себя все, как лошадь, отгоняющая мух", 1),
            ("Ересь из ересей — здравый смысл", 1),
            ("Если надежда и есть, то она — в пролах", 1),
        };

        // Словарь замен по умолчанию (новояз)
        private readonly Dictionary<string, string> _defaultReplacements = new()
        {
            { "фпв", "птицы Остазии" },
            { "БПЛА", "крылья Океании" },
            { "ракетная", "Гольдштейн атакует" }
        };

        public object GetDefaultConfig()
        {
            return new EasterEggConfig
            {
                Slogans = _defaultSlogans.Select(s => new SloganEntry { Text = s.Text, Weight = s.Weight }).ToList(),
                Replacements = _defaultReplacements,
                MinDurationMinutes = 5,
                MaxDurationMinutes = 15,
                PropagandaIntervalSeconds = 45,
                ForegroundColor = ConsoleColor.Green,
                BackgroundColor = ConsoleColor.Black,
                Title = "ВОЙНА — ЭТО МИР"
            };
        }

        public async Task ActivateAsync(EasterEggContext context)
        {
            if (IsActive) return;
            _context = context;

            // Загрузка конфигурации из JSON
            var config = _context.LoadJsonConfig<EasterEggConfig>("!1984.json");
            if (config != null && config.Slogans.Count > 0)
            {
                _slogansWithWeights = config.Slogans.Select(s => (s.Text, s.Weight)).ToList();
                if (config.Replacements.Count > 0)
                    _context.RegisterReplacements(this, config.Replacements);
                else
                    _context.RegisterReplacements(this, _defaultReplacements);
            }
            else
            {
                _slogansWithWeights = _defaultSlogans.ToList();
                _context.RegisterReplacements(this, _defaultReplacements);
            }

            int propagandaIntervalSec = config?.PropagandaIntervalSeconds ?? 45;

            _originalColors = (Console.ForegroundColor, Console.BackgroundColor);
            _originalTitle = Console.Title;

            Console.Clear();
            DrawBanner();

            Console.ForegroundColor = config?.ForegroundColor ?? ConsoleColor.Green;
            Console.BackgroundColor = config?.BackgroundColor ?? ConsoleColor.Black;
            Console.Title = config?.Title ?? "ВОЙНА — ЭТО МИР";

            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.WriteLine($"\n⏱️ Партия вечна, режим вечен.");
			Console.WriteLine($"\n⏱️ Выход только по директие партии !0.");
            Console.ForegroundColor = ConsoleColor.Green;

            _propagandaTimer = new Timer(async _ => await PropagandaAsync(), null, 5000, propagandaIntervalSec * 1000);

            _context.SetMessageHandler(OnNewMessageAsync);

            IsActive = true;
            await Task.CompletedTask;
        }

		private void DrawBanner()
		{
			var oldFore = Console.ForegroundColor;
			var oldBack = Console.BackgroundColor;

			Console.BackgroundColor = ConsoleColor.DarkGray;
			Console.ForegroundColor = ConsoleColor.Black;

			string banner = @"
		╔═══════════════════════════════════════════════════╗
		║  ██████  ██  █████       ██████  █████    █████   ║
		║  ██   ██ ██ ██           ██   ██ ██  ██  ██   ██  ║
		║  ██████  ██ ██  ███      ██████  █████   ██   ██  ║
		║  ██   ██ ██ ██   ██      ██   ██ ██  ██  ██   ██  ║
		║  ██████  ██  ████        ██████  ██   ██  █████   ║
		║                                                   ║
		║                  2 + 2 = 5                        ║
		║             МИНИСТЕРСТВО ПРАВДЫ                   ║
		║             СВОБОДА ЭТО РАБСТВО                   ║
		║                МИР ЭТО ВОЙНА                      ║
		║               НЕЗНАНИЕ - СИЛА                     ║
		╚═══════════════════════════════════════════════════╝
		";
			Console.WriteLine(banner);

			Console.ForegroundColor = oldFore;
			Console.BackgroundColor = oldBack;
		}


        private async Task PropagandaAsync()
        {
            if (!IsActive || _context == null) return;

            var candidates = _slogansWithWeights
                .Where(s => s.Text != _lastSlogan)
                .ToList();
            if (candidates.Count == 0) candidates = _slogansWithWeights;

            int totalWeight = candidates.Sum(w => w.Weight);
            int roll = _random.Next(totalWeight);
            int cumulative = 0;
            string selected = candidates[0].Text;
            foreach (var (text, weight) in candidates)
            {
                cumulative += weight;
                if (roll < cumulative)
                {
                    selected = text;
                    break;
                }
            }
            _lastSlogan = selected;

            var oldColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.WriteLine($"\n📢 {selected}");
            Console.ForegroundColor = oldColor;

            await _context.SpeakAsync(selected, false);
        }

        private async Task OnNewMessageAsync(string sender, string message)
        {
            if (!IsActive || _context == null) return;

            string prefix = "[шшш-щелчок] ";
            _context.ConsoleWriteLine($"{prefix}{sender}: {message}");

            await PlayInterferenceAsync();
            await _context.SpeakAsync(message, true);
            await _context.SpeakAsync("Сообщение отправлено в Министерство правды", true);
        }

        private async Task PlayInterferenceAsync()
        {
            await Task.Run(() =>
            {
                Console.Beep(1200, 50);
                Thread.Sleep(30);
                Console.Beep(800, 50);
            });
        }

        public async Task DeactivateAsync()
        {
            if (!IsActive) return;

            _propagandaTimer?.Dispose();
            _propagandaTimer = null;

            Console.ForegroundColor = _originalColors.Foreground;
            Console.BackgroundColor = _originalColors.Background;
            Console.Title = _originalTitle;

            if (_context != null)
            {
                _context.RemoveMessageHandler(OnNewMessageAsync);
                _context.UnregisterReplacements(this);
            }

            Console.WriteLine("\nРЕЖИМ НОРМАЛИЗОВАН. МЫ ЛЮБИМ БОЛЬШОГО БРАТА");

            IsActive = false;
            await Task.CompletedTask;
        }
    }
}