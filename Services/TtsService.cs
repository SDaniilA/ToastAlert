using System;
using System.Collections.Generic;
using System.Linq;
using System.Speech.Synthesis;
using ToastAlert.Config;
using ToastAlert.Models;
using ToastAlert.Utilities;
using System.Threading.Tasks;

namespace ToastAlert.Services
{
    public class TtsService : IDisposable
    {
        private readonly Config.Config _config;
        private SpeechSynthesizer? _synthesizer;
        private readonly Stats _stats;
        private DateTime _lastSpeechTime = DateTime.MinValue;
        private List<InstalledVoice>? _installedVoices;
        private bool _disposed = false;

        public bool IsMuted { get; set; }

        public TtsService(Config.Config config, Stats stats)
        {
            _config = config;
            _stats = stats;
        }

        public string? GetCurrentVoiceName()
        {
            return _synthesizer?.Voice?.Name;
        }

        public void Initialize()
        {
            if (!_config.VoiceAlert.Enabled)
            {
                Console.WriteLine("🔇 TTS отключен в настройках\n");
                SafeDisposeSynthesizer();
                _synthesizer = null;
                return;
            }

            try
            {
                var newSynth = new SpeechSynthesizer();
                newSynth.SetOutputToDefaultAudioDevice();
                newSynth.Rate = _config.VoiceAlert.TtsRate;
                newSynth.Volume = _config.VoiceAlert.TtsVolume;
                _stats.CurrentVolume = _config.VoiceAlert.TtsVolume;

                bool voiceSet = false;
                if (!string.IsNullOrEmpty(_config.VoiceAlert.VoiceName))
                {
                    try
                    {
                        newSynth.SelectVoice(_config.VoiceAlert.VoiceName);
                        Console.WriteLine($"🎤 Голос из конфига: {_config.VoiceAlert.VoiceName}\n");
                        voiceSet = true;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️ Голос '{_config.VoiceAlert.VoiceName}' не найден: {ex.Message}");
                    }
                }

                if (!voiceSet)
                {
                    foreach (var voice in newSynth.GetInstalledVoices())
                    {
                        if (voice.VoiceInfo.Culture.Name.StartsWith("ru"))
                        {
                            newSynth.SelectVoice(voice.VoiceInfo.Name);
                            Console.WriteLine($"🎤 Голос: {voice.VoiceInfo.Name}\n");
                            voiceSet = true;
                            break;
                        }
                    }
                }

                if (!voiceSet)
                    Console.WriteLine("🎤 Используется голос по умолчанию\n");

                SafeDisposeSynthesizer();
                _synthesizer = newSynth;
                _installedVoices = null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ TTS ошибка: {ex.Message}");
                Console.WriteLine("   Будет использован звуковой сигнал\n");
                SafeDisposeSynthesizer();
                _synthesizer = null;
            }
        }

        private void SafeDisposeSynthesizer()
        {
            if (_synthesizer != null)
            {
                try
                {
                    _synthesizer.Dispose();
                }
                catch { }
                _synthesizer = null;
            }
        }

        public void Speak(string text, bool isPriority, bool allowInterrupt = true)
        {
            if (!_config.VoiceAlert.Enabled)
            {
                PlayBeep(_config.Sounds.OnNewMessageSound);
                return;
            }
            if (_synthesizer == null)
            {
                if (_config.VoiceAlert.FallbackBeepOnError)
                    Console.Beep(_config.VoiceAlert.BeepFrequency, _config.VoiceAlert.BeepDurationMs);
                return;
            }

            try
            {
                if (isPriority && _config.Priorities.InterruptCurrentSpeech)
                {
                    _synthesizer.SpeakAsyncCancelAll();
                    if (_config.Priorities.HighPriorityBeep)
                        Console.Beep(2000, 300);
                }
                if (allowInterrupt && _config.VoiceAlert.AbortPreviousSpeech && _synthesizer.State == SynthesizerState.Speaking)
                {
                    _synthesizer.SpeakAsyncCancelAll();
                }

                _synthesizer.SpeakAsync(text);
                PlayBeep(isPriority ? _config.Sounds.OnKeywordDetectedSound : _config.Sounds.OnNewMessageSound);
            }
            catch (Exception ex)
            {
                if (_config.Additional.DebugMode)
                    Console.WriteLine($"   ⚠️ TTS ошибка: {ex.Message}");
                if (_config.VoiceAlert.FallbackBeepOnError)
                    Console.Beep(_config.VoiceAlert.BeepFrequency, _config.VoiceAlert.BeepDurationMs);
            }
        }

        private void PlayBeep(string soundType)
        {
            Task.Run(() =>
            {
                switch (soundType.ToLower())
                {
                    case "beep": Console.Beep(1000, 150); break;
                    case "beep_high": Console.Beep(2000, 200); break;
                    case "beep_low": Console.Beep(500, 300); break;
                }
            });
        }

        public void ChangeVolume(int delta)
        {
            int newVol = Math.Clamp(_stats.CurrentVolume + delta, 0, 100);
            if (_synthesizer != null) _synthesizer.Volume = newVol;
            _stats.CurrentVolume = newVol;
            Console.Beep(delta > 0 ? 1500 : 1000, 100);
            Console.WriteLine($"\n🔊 Громкость: {newVol}%");
            if (_config.Additional.RememberVolume)
            {
                _config.VoiceAlert.TtsVolume = newVol;
                ConfigLoader.Save(_config);
            }
        }

        public bool CanSpeakNow()
        {
            int minDelay = _config.VoiceAlert.MinDelayBetweenMessagesMs;
            return (DateTime.Now - _lastSpeechTime).TotalMilliseconds >= minDelay;
        }

        public void UpdateLastSpeechTime() => _lastSpeechTime = DateTime.Now;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            SafeDisposeSynthesizer();
        }

        public List<(string Name, string Culture, bool IsDefault)> GetVoicesList()
        {
            if (_synthesizer == null) return new List<(string, string, bool)>();
            if (_installedVoices == null)
                _installedVoices = _synthesizer.GetInstalledVoices().ToList();

            var currentVoice = _synthesizer.Voice;
            return _installedVoices.Select(v => (
                Name: v.VoiceInfo.Name,
                Culture: v.VoiceInfo.Culture.DisplayName,
                IsDefault: currentVoice != null && v.VoiceInfo.Name == currentVoice.Name
            )).ToList();
        }

        public bool SelectVoiceByName(string name)
        {
            if (_synthesizer == null) return false;
            try
            {
                _synthesizer.SelectVoice(name);
                _synthesizer.SetOutputToDefaultAudioDevice(); // ВАЖНО: восстановить вывод звука
                _config.VoiceAlert.VoiceName = name;
                ConfigLoader.Save(_config);
                Console.WriteLine($"🎤 Голос изменён на: {name}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Не удалось выбрать голос: {ex.Message}");
                return false;
            }
        }

        public bool SelectVoiceByIndex(int index)
        {
            var voices = GetVoicesList();
            if (index < 0 || index >= voices.Count) return false;
            return SelectVoiceByName(voices[index].Name);
        }
    }
}