using System;

namespace PipeWiseClient.Models
{
    public enum NotificationType
    {
        Success,
        Error,
        Warning,
        Info
    }

    public class NotificationItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public NotificationType Type { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public bool IsDetailed { get; set; } = false;
        public string? Details { get; set; }
    }
}

