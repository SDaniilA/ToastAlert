using System;
using System.Collections.Generic;
using System.Linq;
using ToastAlert.Config;
using ToastAlert.Models;
using ToastAlert.Utilities;

namespace ToastAlert.Services
{
    public class DeduplicationService
    {
        private readonly Config.Config _config;
        private readonly List<CachedMessage> _buffer = new();
        private readonly object _lock = new();

        public DeduplicationService(Config.Config config)
        {
            _config = config;
        }

        public bool IsDuplicate(string text, string sender, out double similarity, out string dupSender, out string? dupOriginalMessage)
		//public bool IsDuplicate(string text, string sender, out double similarity, out string dupSender, out string? dupOriginalMessage)
        {
            similarity = 0;
            dupSender = null!;
            dupOriginalMessage = null!;
            if (!_config.Deduplication.Enabled) return false;

            string normalized = StringUtilities.NormalizeForDeduplication(text, _config);
            string hash = StringUtilities.ComputeHash(normalized);

            lock (_lock)
            {
                CleanOldMessages();

                // точное совпадение хэша
                var exact = _buffer.Find(m => m.Hash == hash);
                if (exact != null)
                {
                    similarity = 1.0;
                    dupSender = exact.OriginalSender;
                    dupOriginalMessage = exact.OriginalMessage;
                    return true;
                }

                // Левенштейн
                if (_config.Deduplication.UseLevenshtein)
                {
                    double best = 0;
                    CachedMessage? bestMsg = null;
                    foreach (var m in _buffer)
                    {
                        double sim = StringUtilities.LevenshteinSimilarity(normalized, m.NormalizedText);
                        if (sim > best) { best = sim; bestMsg = m; }
                    }
                    if (best >= _config.Deduplication.SimilarityThresholdHigh)
                    {
                        similarity = best;
                        dupSender = bestMsg?.OriginalSender ?? string.Empty;
						dupOriginalMessage = bestMsg?.OriginalMessage ?? string.Empty;
                        return true;
                    }
                    similarity = best;
                }
                return false;
            }
        }

        public void AddToBuffer(string text, string sender, string originalMessage)
        {
            if (!_config.Deduplication.Enabled) return;
            string normalized = StringUtilities.NormalizeForDeduplication(text, _config);
            string hash = StringUtilities.ComputeHash(normalized);
            lock (_lock)
            {
                _buffer.Add(new CachedMessage
                {
                    NormalizedText = normalized,
                    Hash = hash,
                    OriginalSender = sender,
                    OriginalMessage = originalMessage,
                    ReceivedAt = DateTime.Now
                });
                while (_buffer.Count > _config.Deduplication.BufferMaxSize)
                    _buffer.RemoveAt(0);
            }
        }

        public void ClearBuffer()
        {
            lock (_lock) _buffer.Clear();
        }

        private void CleanOldMessages()
        {
            DateTime cutoff = DateTime.Now.AddSeconds(-_config.Deduplication.TimeWindowSeconds);
            _buffer.RemoveAll(m => m.ReceivedAt < cutoff);
        }
    }
}
