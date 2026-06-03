using System;
using System.Collections.Generic;
using ToastAlert.Config;

namespace ToastAlert.Services
{
    public class QuickConfigService
    {
        private readonly Config.Config _config;
        private readonly TtsService _tts;
        private readonly MqttService _mqtt;
        private readonly Dictionary<char, Action> _quickActions;

        public QuickConfigService(Config.Config config, TtsService tts, MqttService mqtt)
        {
            _config = config;
            _tts = tts;
            _mqtt = mqtt;

            _quickActions = new Dictionary<char, Action>
            {
                ['1'] = () => { _tts.IsMuted = !_tts.IsMuted; Console.WriteLine($"🔇 Звук {(_tts.IsMuted ? "выкл" : "вкл")}"); },
                ['2'] = () => { _config.Mqtt.TestMode = !_config.Mqtt.TestMode; ConfigLoader.Save(_config); Console.WriteLine($"🧪 MQTT TestMode: {(_config.Mqtt.TestMode ? "ВКЛ" : "ВЫКЛ")}"); },
                ['3'] = () => { _tts.ChangeVolume(10); ConfigLoader.Save(_config); },
                ['4'] = () => { _tts.ChangeVolume(-10); ConfigLoader.Save(_config); },
                ['5'] = () => { _config.VoiceAlert.Enabled = !_config.VoiceAlert.Enabled; ConfigLoader.Save(_config); Console.WriteLine($"🔊 TTS: {(_config.VoiceAlert.Enabled ? "ВКЛ" : "ВЫКЛ")}"); },
                ['6'] = () => { _config.Monitoring.ConsoleOutputEnabled = !_config.Monitoring.ConsoleOutputEnabled; ConfigLoader.Save(_config); Console.WriteLine($"📟 Вывод в консоль: {(_config.Monitoring.ConsoleOutputEnabled ? "ВКЛ" : "ВЫКЛ")}"); },
				['7'] = () => { _config.Additional.PreventSleep = !_config.Additional.PreventSleep; ConfigLoader.Save(_config); Console.WriteLine($"💤 Режим сна: {(_config.Additional.PreventSleep ? "ЗАПРЕЩЁН" : "РАЗРЕШЁН")}"); },
                // Добавьте другие цифры по необходимости (7,8,9,0 – 0 лучше оставить для сброса пасхалки)
            };
        }

        public bool Execute(char digit)
        {
            if (_quickActions.TryGetValue(digit, out var action))
            {
                action();
                return true;
            }
            return false;
        }

        public string GetHelp()
        {
            return "   !1 – Mute TTS\n   !2 – Переключить TestMode MQTT\n   !3 – Громкость +10\n   !4 – Громкость -10\n   !5 – Вкл/Выкл TTS\n   !6 – Вкл/Выкл вывод в консоль\n  !7 – Запрет/разрешение сна";
        }
    }
}