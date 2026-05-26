using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace ToastAlert.Config
{
    [JsonObject(MemberSerialization.OptIn)]
    public class Config
    {
        [JsonProperty("Мониторинг")] public MonitorConfig Monitoring { get; set; } = new();
        [JsonProperty("Фильтрация")] public FilterConfig Filtering { get; set; } = new();
        [JsonProperty("ГолосовоеОповещение")] public TtsConfig VoiceAlert { get; set; } = new();
        [JsonProperty("УдалениеУведомлений")] public DeleteConfig NotificationDeletion { get; set; } = new();
        [JsonProperty("ОчисткаТекста")] public CleanTextConfig TextCleaning { get; set; } = new();
        [JsonProperty("Аббревиатуры")] public AbbrevConfig Abbreviations { get; set; } = new();
        [JsonProperty("Логирование")] public LogConfig Logging { get; set; } = new();
        [JsonProperty("Звуки")] public SoundConfig Sounds { get; set; } = new();
        [JsonProperty("Дополнительно")] public ExtraConfig Additional { get; set; } = new();
        [JsonProperty("Приоритеты")] public PriorityConfig Priorities { get; set; } = new();
        [JsonProperty("MQTT")] public MqttConfig Mqtt { get; set; } = new();
        [JsonProperty("ПроверкаСвязи")] public HealthConfig HealthCheck { get; set; } = new();
        [JsonProperty("Дедупликация")] public DedupConfig Deduplication { get; set; } = new();
    }

    public class MonitorConfig
    {
        [JsonProperty("CheckIntervalSeconds")] public int CheckIntervalSeconds { get; set; } = 2;
        [JsonProperty("MaxNotificationsPerCheck")] public int MaxNotificationsPerCheck { get; set; } = 50;
        [JsonProperty("StartMinimized")] public bool StartMinimized { get; set; } = true;
        [JsonProperty("ShowPopupNotifications")] public bool ShowPopupNotifications { get; set; } = true;
        [JsonProperty("PopupTimeoutSeconds")] public int PopupTimeoutSeconds { get; set; } = 5;
        [JsonProperty("LogToFile")] public bool LogToFile { get; set; } = true;
        [JsonProperty("LogFilePath")] public string LogFilePath { get; set; } = "telegram_alerts.log";
        [JsonProperty("SaveProcessedIds")] public bool SaveProcessedIds { get; set; } = true;
        [JsonProperty("ProcessedIdsFile")] public string ProcessedIdsFile { get; set; } = "processed.json";
        [JsonProperty("MaxProcessedIdsCount")] public int MaxProcessedIdsCount { get; set; } = 1000;
        [JsonProperty("ConsoleOutputEnabled")] public bool ConsoleOutputEnabled { get; set; } = true;
        [JsonProperty("ConsoleMaxMessageLength")] public int ConsoleMaxMessageLength { get; set; } = 0;
        [JsonProperty("ConsoleBeepOnMessage")] public bool ConsoleBeepOnMessage { get; set; } = true;
		[JsonProperty("AllowedApps")] public List<string>? AllowedApps { get; set; }
    }

    public class FilterConfig
    {
        [JsonProperty("AllowedSenders")] public List<string> AllowedSenders { get; set; } = new();
        [JsonProperty("RequireKeywords")] public bool RequireKeywords { get; set; } = false;
        [JsonProperty("Keywords")] public List<string> ListA { get; set; } = new();   // переименовано из Keywords
        [JsonProperty("СписокБ")] public List<string> ListB { get; set; } = new();
        [JsonProperty("СписокС")] public List<string> ListC { get; set; } = new();
        [JsonProperty("Оператор")] public string Operator { get; set; } = "И";
        [JsonProperty("BlacklistedSenders")] public List<string> BlacklistedSenders { get; set; } = new();
        [JsonProperty("BlacklistedKeywords")] public List<string> BlacklistedKeywords { get; set; } = new();
        [JsonProperty("CaseSensitive")] public bool CaseSensitive { get; set; } = false;
        [JsonProperty("PartialMatchSenders")] public bool PartialMatchSenders { get; set; } = true;
        [JsonProperty("PartialMatchKeywords")] public bool PartialMatchKeywords { get; set; } = true;
    }

    public class TtsConfig
    {
        [JsonProperty("Enabled")] public bool Enabled { get; set; } = true;
        [JsonProperty("TtsEngine")] public string TtsEngine { get; set; } = "windows";
        [JsonProperty("TtsRate")] public int TtsRate { get; set; } = 1;
        [JsonProperty("TtsVolume")] public int TtsVolume { get; set; } = 100;
        [JsonProperty("MaxMessageLength")] public int MaxMessageLength { get; set; } = 300;
        [JsonProperty("SpeakOnlyTitle")] public bool SpeakOnlyTitle { get; set; } = false;
        [JsonProperty("SpeakSenderName")] public bool SpeakSenderName { get; set; } = true;
        [JsonProperty("SpeakFullMessage")] public bool SpeakFullMessage { get; set; } = true;
        [JsonProperty("SpeakOnlyOnKeywords")] public bool SpeakOnlyOnKeywords { get; set; } = false;
        [JsonProperty("SpeakTime")] public bool SpeakTime { get; set; } = false;
        [JsonProperty("TimeFormat")] public string TimeFormat { get; set; } = "hours_minutes";
        [JsonProperty("PrefixText")] public string PrefixText { get; set; } = "";
        [JsonProperty("SuffixText")] public string SuffixText { get; set; } = "От канала";
        [JsonProperty("FallbackBeepOnError")] public bool FallbackBeepOnError { get; set; } = true;
        [JsonProperty("BeepFrequency")] public int BeepFrequency { get; set; } = 1500;
        [JsonProperty("BeepDurationMs")] public int BeepDurationMs { get; set; } = 200;
        [JsonProperty("AbortPreviousSpeech")] public bool AbortPreviousSpeech { get; set; } = true;
        [JsonProperty("MinDelayBetweenMessagesMs")] public int MinDelayBetweenMessagesMs { get; set; } = 500;
		[JsonProperty("VoiceName")] public string? VoiceName { get; set; } = null; // null = голос по умолчанию
    }

    public class DeleteConfig
    {
        [JsonProperty("DeleteMode")] public string DeleteMode { get; set; } = "on_exit";
        [JsonProperty("DeleteOnlyProcessed")] public bool DeleteOnlyProcessed { get; set; } = true;
        [JsonProperty("DeleteAfterSeconds")] public int DeleteAfterSeconds { get; set; } = 0;
        [JsonProperty("KeepUnread")] public bool KeepUnread { get; set; } = false;
    }

    public class CleanTextConfig
    {
        [JsonProperty("RemoveAds")] public bool RemoveAds { get; set; } = true;
        [JsonProperty("CustomAdsToRemove")] public List<string> CustomAdsToRemove { get; set; } = new();
        [JsonProperty("RemoveHtmlTags")] public bool RemoveHtmlTags { get; set; } = true;
        [JsonProperty("RemoveUrls")] public bool RemoveUrls { get; set; } = true;
        [JsonProperty("RemoveEmojis")] public bool RemoveEmojis { get; set; } = false;
        [JsonProperty("RemoveExtraSpaces")] public bool RemoveExtraSpaces { get; set; } = true;
        [JsonProperty("TrimMessage")] public bool TrimMessage { get; set; } = true;
        [JsonProperty("MaxMessageLength")] public int MaxMessageLength { get; set; } = 300;
    }

    public class AbbrevConfig
    {
        [JsonProperty("Enabled")] public bool Enabled { get; set; } = true;
        [JsonProperty("CustomAbbreviations")] public Dictionary<string, string> CustomAbbreviations { get; set; } = new();
    }

    public class LogConfig
    {
        [JsonProperty("Enabled")] public bool Enabled { get; set; } = true;
        [JsonProperty("Separator")] public string Separator { get; set; } = "--------------------------------------------------";
        [JsonProperty("IncludeTimestamp")] public bool IncludeTimestamp { get; set; } = true;
        [JsonProperty("TimestampFormat")] public string TimestampFormat { get; set; } = "yyyy-MM-dd HH:mm:ss";
        [JsonProperty("IncludeSender")] public bool IncludeSender { get; set; } = true;
        [JsonProperty("IncludeMessage")] public bool IncludeMessage { get; set; } = true;
        [JsonProperty("IncludeStats")] public bool IncludeStats { get; set; } = true;
        [JsonProperty("StatsLogIntervalMinutes")] public int StatsLogIntervalMinutes { get; set; } = 10;
        [JsonProperty("MaxLogFileSizeMB")] public int MaxLogFileSizeMB { get; set; } = 10;
        [JsonProperty("RotateLogs")] public bool RotateLogs { get; set; } = true;
        [JsonProperty("LogToConsole")] public bool LogToConsole { get; set; } = true;
        [JsonProperty("LogToFile")] public bool LogToFile { get; set; } = true;
    }

    public class SoundConfig
    {
        [JsonProperty("EnableSounds")] public bool EnableSounds { get; set; } = true;
        [JsonProperty("OnNewMessageSound")] public string OnNewMessageSound { get; set; } = "beep";
        [JsonProperty("OnKeywordDetectedSound")] public string OnKeywordDetectedSound { get; set; } = "beep_high";
        [JsonProperty("OnErrorSound")] public string OnErrorSound { get; set; } = "beep_low";
        [JsonProperty("SoundVolume")] public int SoundVolume { get; set; } = 100;
        [JsonProperty("CustomSoundWavPath")] public string CustomSoundWavPath { get; set; } = "";
        [JsonProperty("CustomSoundKeywordWavPath")] public string CustomSoundKeywordWavPath { get; set; } = "";
    }

    public class ExtraConfig
    {
        [JsonProperty("EnableHotkeys")] public bool EnableHotkeys { get; set; } = true;
        [JsonProperty("HotkeyStats")] public string HotkeyStats { get; set; } = "S";
        [JsonProperty("HotkeyConfig")] public string HotkeyConfig { get; set; } = "C";
        [JsonProperty("HotkeyMute")] public string HotkeyMute { get; set; } = "M";
        [JsonProperty("HotkeyExit")] public string HotkeyExit { get; set; } = "Escape";
        [JsonProperty("AutoStartWithWindows")] public bool AutoStartWithWindows { get; set; } = false;
        [JsonProperty("RunInBackground")] public bool RunInBackground { get; set; } = true;
        [JsonProperty("ShowTrayIcon")] public bool ShowTrayIcon { get; set; } = false;
        [JsonProperty("Language")] public string Language { get; set; } = "ru";
        [JsonProperty("DebugMode")] public bool DebugMode { get; set; } = false;
        [JsonProperty("DeveloperMode")] public bool DeveloperMode { get; set; } = false;
        [JsonProperty("RememberVolume")] public bool RememberVolume { get; set; } = true;
        [JsonProperty("MqttEnabled")] public bool MqttEnabled { get; set; } = true;
        [JsonProperty("PreventSleep")] public bool PreventSleep { get; set; } = false;
    }

    public class PriorityConfig
    {
        [JsonProperty("PriorityKeywords")] public List<string> PriorityKeywords { get; set; } = new();
        [JsonProperty("PriorityThreshold")] public int PriorityThreshold { get; set; } = 100;
        [JsonProperty("InterruptCurrentSpeech")] public bool InterruptCurrentSpeech { get; set; } = true;
        [JsonProperty("HighPriorityBeep")] public bool HighPriorityBeep { get; set; } = true;
    }

    public class MqttConfig
    {
        [JsonProperty("Enabled")] public bool Enabled { get; set; } = false;
        [JsonProperty("TestMode")] public bool TestMode { get; set; } = false;
        [JsonProperty("Broker")] public string Broker { get; set; } = "127.0.0.1";
        [JsonProperty("Port")] public int Port { get; set; } = 1883;
        [JsonProperty("Topic")] public string Topic { get; set; } = "telegram/alerts";
        [JsonProperty("ClientId")] public string ClientId { get; set; } = "";
        [JsonProperty("FromNodeId")] public uint FromNodeId { get; set; } = 1234567890;
        [JsonProperty("ToNodeId")] public uint ToNodeId { get; set; } = 4294967295;
        [JsonProperty("ChannelIndex")] public int ChannelIndex { get; set; } = 0;
        [JsonProperty("IncludeTimestamp")] public bool IncludeTimestamp { get; set; } = true;
        [JsonProperty("TimestampFormat")] public string TimestampFormat { get; set; } = "HH:mm:ss";
        [JsonProperty("MaxPayloadBytes")] public int MaxPayloadBytes { get; set; } = 100;
        [JsonProperty("ReconnectEnabled")] public bool ReconnectEnabled { get; set; } = true;
    }

    public class HealthConfig
    {
        [JsonProperty("ИнтервалСек")] public int IntervalSeconds { get; set; } = 30;
        [JsonProperty("МаксПопыток")] public int MaxAttempts { get; set; } = 0;
        [JsonProperty("ТаймаутМс")] public int TimeoutMs { get; set; } = 3000;
        [JsonProperty("МаксВремяПопытокСек")] public int MaxRetryTimeSeconds { get; set; } = 0;
    }

    public class DedupConfig
    {
        [JsonProperty("Enabled")] public bool Enabled { get; set; } = true;
        [JsonProperty("TimeWindowSeconds")] public int TimeWindowSeconds { get; set; } = 120;
        [JsonProperty("SimilarityThresholdHigh")] public double SimilarityThresholdHigh { get; set; } = 0.95;
        [JsonProperty("SimilarityThresholdLow")] public double SimilarityThresholdLow { get; set; } = 0.85;
        [JsonProperty("BufferMaxSize")] public int BufferMaxSize { get; set; } = 50;
        [JsonProperty("NormalizeTime")] public bool NormalizeTime { get; set; } = true;
        [JsonProperty("UseLevenshtein")] public bool UseLevenshtein { get; set; } = true;
        [JsonProperty("LogDuplicates")] public bool LogDuplicates { get; set; } = true;
    }
}
