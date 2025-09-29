using System;
using System.Collections.Generic;
using PipeWiseClient.Models;

namespace PipeWiseClient.Interfaces
{
    public interface INotificationService
    {
        void Success(string title, string message, string? details = null);
        void Error(string title, string message, string? details = null);
        void Warning(string title, string message, string? details = null);
        void Info(string title, string message, string? details = null);
        void Clear();
        IReadOnlyList<NotificationItem> Notifications { get; }
        event EventHandler<NotificationItem>? NotificationAdded;
    }
}

