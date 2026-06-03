using System;
using System.Collections.Generic;

namespace ToastAlert.Services
{
    public class EasterEggConfig
    {
        public List<SloganEntry> Slogans { get; set; } = new();
        public Dictionary<string, string> Replacements { get; set; } = new();
        public int MinDurationMinutes { get; set; } = 5;
        public int MaxDurationMinutes { get; set; } = 15;
        public int PropagandaIntervalSeconds { get; set; } = 45;
        public ConsoleColor? ForegroundColor { get; set; }
        public ConsoleColor? BackgroundColor { get; set; }
        public string? Title { get; set; }
    }

    public class SloganEntry
    {
        public string Text { get; set; } = "";
        public int Weight { get; set; } = 1;
    }
}