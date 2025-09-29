// PipeWise_Client/MainWindow/Parts/MainWindow.Notifications.cs

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using PipeWiseClient.Models;

namespace PipeWiseClient
{
    public partial class MainWindow
    {
        private void RefreshNotificationsDisplay()
        {
            if (NotificationsPanel == null) return;

            var notifications = _notifications.Notifications;

            NotificationsPanel.Children.Clear();

            if (DefaultMessageBorder != null)
            {
                DefaultMessageBorder.Visibility = notifications.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
            }

            foreach (var notification in notifications)
            {
                var notificationElement = CreateNotificationElement(notification);
                NotificationsPanel.Children.Add(notificationElement);
            }

            UpdateNotificationCount();

            if (LastNotificationTimeText != null)
            {
                LastNotificationTimeText.Text = DateTime.Now.ToString("HH:mm:ss");
            }

            if (NotificationsScrollViewer != null)
            {
                NotificationsScrollViewer.ScrollToTop();
            }
        }

        private Border CreateNotificationElement(NotificationItem notification)
        {
            var (icon, backgroundColor, borderColor, textColor) = GetNotificationStyle(notification.Type);

            var border = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(backgroundColor)),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(borderColor)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(15, 12, 15, 12),
                Margin = new Thickness(1, 0, 1, 0)
            };

            var mainPanel = new StackPanel();

            var headerPanel = new Grid();
            headerPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var leftPanel = new StackPanel { Orientation = Orientation.Horizontal };

            var iconText = new TextBlock
            {
                Text = icon,
                FontSize = 14,
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            var titleText = new TextBlock
            {
                Text = notification.Title,
                FontWeight = FontWeights.SemiBold,
                FontSize = 12,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(textColor)),
                VerticalAlignment = VerticalAlignment.Center
            };

            leftPanel.Children.Add(iconText);
            leftPanel.Children.Add(titleText);

            var timeText = new TextBlock
            {
                Text = notification.Timestamp.ToString("HH:mm:ss"),
                FontSize = 10,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6C757D")),
                VerticalAlignment = VerticalAlignment.Center
            };

            headerPanel.Children.Add(leftPanel);
            headerPanel.Children.Add(timeText);
            Grid.SetColumn(leftPanel, 0);
            Grid.SetColumn(timeText, 1);

            mainPanel.Children.Add(headerPanel);

            var messageText = new TextBlock
            {
                Text = notification.Message,
                FontSize = 11,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(textColor)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(22, 4, 0, 0)
            };

            mainPanel.Children.Add(messageText);

            if (notification.IsDetailed && !string.IsNullOrEmpty(notification.Details))
            {
                var detailsBorder = new Border
                {
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8F9FA")),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(10, 10, 10, 10),
                    Margin = new Thickness(22, 6, 0, 0)
                };

                var detailsText = new TextBlock
                {
                    Text = notification.Details,
                    FontSize = 10,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#495057")),
                    TextWrapping = TextWrapping.Wrap,
                    FontFamily = new FontFamily("Consolas")
                };

                detailsBorder.Child = detailsText;
                mainPanel.Children.Add(detailsBorder);
            }

            border.Child = mainPanel;

            border.Opacity = 0;
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));
            border.BeginAnimation(UIElement.OpacityProperty, fadeIn);

            return border;
        }

        private (string icon, string backgroundColor, string borderColor, string textColor) GetNotificationStyle(NotificationType type)
        {
            return type switch
            {
                NotificationType.Success => ("?", "#D4F4DD", "#28A745", "#155724"),
                NotificationType.Error => ("?", "#F8D7DA", "#DC3545", "#721C24"),
                NotificationType.Warning => ("??", "#FFF3CD", "#FFC107", "#856404"),
                NotificationType.Info => ("??", "#CCE7FF", "#007BFF", "#004085"),
                _ => ("??", "#F8F9FA", "#6C757D", "#495057")
            };
        }

        private void UpdateNotificationCount()
        {
            if (NotificationCountBadge == null || NotificationCountText == null) return;

            var count = _notifications.Notifications.Count;

            if (count > 0)
            {
                NotificationCountBadge.Visibility = Visibility.Visible;
                NotificationCountText.Text = count > 99 ? "99+" : count.ToString();
            }
            else
            {
                NotificationCountBadge.Visibility = Visibility.Collapsed;
            }
        }

        public void UpdateSystemStatus(string status, bool isHealthy = true)
        {
            if (SystemStatusText == null) return;

            var icon = isHealthy ? "??" : "??";
            SystemStatusText.Text = $"{icon} {status}";
        }
    }
}

