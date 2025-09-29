using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using PipeWiseClient.Interfaces;
using PipeWiseClient.Models;

namespace PipeWiseClient.Services
{
    public class NotificationService : INotificationService
    {
        private const int MAX_NOTIFICATIONS = 50;
        private readonly ObservableCollection<NotificationItem> _notifications;

        public NotificationService()
        {
            _notifications = new ObservableCollection<NotificationItem>();
        }

        public IReadOnlyList<NotificationItem> Notifications => _notifications.ToList();

        public event EventHandler<NotificationItem>? NotificationAdded;

        public void Success(string title, string message, string? details = null)
            => AddNotification(NotificationType.Success, title, message, details);

        public void Error(string title, string message, string? details = null)
            => AddNotification(NotificationType.Error, title, message, details);

        public void Warning(string title, string message, string? details = null)
            => AddNotification(NotificationType.Warning, title, message, details);

        public void Info(string title, string message, string? details = null)
            => AddNotification(NotificationType.Info, title, message, details);

        public void Clear()
        {
            _notifications.Clear();
        }

        private void AddNotification(NotificationType type, string title, string message, string? details)
        {
            var item = new NotificationItem
            {
                Type = type,
                Title = title,
                Message = message,
                Details = details,
                IsDetailed = !string.IsNullOrWhiteSpace(details),
                Timestamp = DateTime.Now
            };

            _notifications.Insert(0, item);
            if (_notifications.Count > MAX_NOTIFICATIONS)
            {
                _notifications.RemoveAt(_notifications.Count - 1);
            }

            NotificationAdded?.Invoke(this, item);
        }
    }
}

