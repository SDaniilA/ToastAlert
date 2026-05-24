using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using ToastAlert.Config;

namespace ToastAlert.Utilities
{
    public static class StringUtilities
    {
        public static string CleanMessage(string text, Config.Config cfg)
        {
            if (cfg.TextCleaning.RemoveAds)
            {
                foreach (var ad in cfg.TextCleaning.CustomAdsToRemove)
                    text = text.Replace(ad, "");
            }

            if (cfg.TextCleaning.RemoveHtmlTags)
                text = Regex.Replace(text, "<.*?>", "");

            if (cfg.TextCleaning.RemoveUrls)
                text = Regex.Replace(text, @"https?:\/\/[^\s]+", "");

            if (cfg.TextCleaning.RemoveEmojis)
                text = Regex.Replace(text, @"[\uD800-\uDBFF][\uDC00-\uDFFF]|[\u2600-\u26FF]", "");

            if (cfg.TextCleaning.RemoveExtraSpaces)
                text = Regex.Replace(text, @"\s+", " ");

            if (cfg.TextCleaning.TrimMessage)
                text = text.Trim();

            if (text.Length > cfg.TextCleaning.MaxMessageLength)
                text = text.Substring(0, cfg.TextCleaning.MaxMessageLength);

            return text;
        }

        public static string NormalizeForDeduplication(string text, Config.Config cfg)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            text = Regex.Replace(text, @"\p{Cs}|\p{So}", "");
            text = text.ToLower();
            text = Regex.Replace(text, @"""""?\s*[-–]\s*""""?$", "");
            text = Regex.Replace(text, @"\s+", " ").Trim();
            if (cfg.Deduplication.NormalizeTime)
                text = Regex.Replace(text, @"\b\d{1,2}[:.-]\d{2}\b", "<TIME>");
            return text;
        }

        public static string ComputeHash(string text)
        {
            return Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(text)));
        }

        public static int LevenshteinDistance(string s, string t)
        {
            if (string.IsNullOrEmpty(s)) return t?.Length ?? 0;
            if (string.IsNullOrEmpty(t)) return s.Length;
            var d = new int[s.Length + 1, t.Length + 1];
            for (int i = 0; i <= s.Length; i++) d[i, 0] = i;
            for (int j = 0; j <= t.Length; j++) d[0, j] = j;
            for (int i = 1; i <= s.Length; i++)
                for (int j = 1; j <= t.Length; j++)
                {
                    int cost = s[i - 1] == t[j - 1] ? 0 : 1;
                    d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
                }
            return d[s.Length, t.Length];
        }

        public static double LevenshteinSimilarity(string s1, string s2)
        {
            if (string.IsNullOrEmpty(s1) && string.IsNullOrEmpty(s2)) return 1.0;
            int maxLen = Math.Max(s1.Length, s2.Length);
            if (maxLen == 0) return 1.0;
            return 1.0 - (double)LevenshteinDistance(s1, s2) / maxLen;
        }

        public static string TruncateToBytes(string text, int maxBytes)
        {
            if (string.IsNullOrEmpty(text)) return text;
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            if (bytes.Length <= maxBytes) return text;
            Array.Resize(ref bytes, maxBytes);
            while (maxBytes > 0 && (bytes[maxBytes - 1] & 0xC0) == 0x80)
            {
                maxBytes--;
                Array.Resize(ref bytes, maxBytes);
            }
            string result = Encoding.UTF8.GetString(bytes);
            if (result.Length < text.Length) result += "...";
            return result;
        }

        public static string SanitizeForMqtt(string sender, string message, int maxBytes)
        {
            string text = $"[Telegram] {message}".Replace("[Telegram]", "TG:");
            text = Regex.Replace(text, @"\p{Cs}", "");
            text = Regex.Replace(text, @"\s+", " ").Trim();
            return TruncateToBytes(text, maxBytes);
        }

        public static string NormalizeTextForSpeech(string text, Config.Config cfg)
        {
            if (!cfg.Abbreviations.Enabled) return text;
            foreach (var kv in cfg.Abbreviations.CustomAbbreviations)
                text = Regex.Replace(text, $@"\b{Regex.Escape(kv.Key)}\b", kv.Value, RegexOptions.IgnoreCase);
            return text;
        }
    }
}
