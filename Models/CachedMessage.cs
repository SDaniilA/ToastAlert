using System;

namespace ToastAlert.Models
{
    public class CachedMessage
    {
        public string NormalizedText { get; set; } = "";
        public string Hash { get; set; } = "";
        public string OriginalSender { get; set; } = "";
        public string OriginalMessage { get; set; } = "";
        public DateTime ReceivedAt { get; set; }
    }
}
