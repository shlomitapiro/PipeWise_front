using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Win32;
using Newtonsoft.Json;
using OfficeOpenXml;

using PipeWiseClient.Helpers;
using PipeWiseClient.Models;
using PipeWiseClient.Services;
using PipeWiseClient.Windows;
using System.Text.Json;
using Newtonsoft.Json.Linq;



namespace PipeWiseClient
{
    public partial class MainWindow : Window
    {
        private readonly ApiClient _api = new();
        private List<string> _columnNames = new List<string>();
        private Dictionary<string, ColumnSettings> _columnSettings = new Dictionary<string, ColumnSettings>();

        private PipelineConfig? _loadedConfig;
        private const string OUTPUT_DIR = @"C:\Users\shlom\PipeWise\output";

        // מערכת התראות - הגדרות בסיסיות
        private List<NotificationItem> _notifications = new List<NotificationItem>();
        private bool _notificationsCollapsed = false;
        private const int MAX_NOTIFICATIONS = 50;

        private bool _isApplyingConfig = false;

        // הגדרות לשמירת גדלי אזורים
        private const string SETTINGS_FILE = "ui_settings.json";
        
        // מבנה הגריד:
        // Row 0: אזור בחירת קובץ (Auto)
        // Row 1: ריווח (Auto) 
        // Row 2: אזור עמודות ופעולות (2* - ניתן לשינוי)
        // Row 3: כפתורי פעולה (Auto - גודל קבוע)
        // Row 4: GridSplitter
        // Row 5: אזור התראות (1* - ניתן לשינוי)
        
        public MainWindow()
        {
            try
            {
                InitializeComponent();
                
                // אתחול EPPlus
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
                
                // טען הגדרות גדלי אזורים
                LoadUISettings();
                
                // הוספת הודעת ברכה
                AddInfoNotification("ברוך הבא ל-PipeWise", "המערכת מוכנה לעיבוד נתונים");
                
                // הוסף מאזיני אירועים לשמירת הגדרות
                this.Closing += MainWindow_Closing;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"שגיאה באתחול החלון: {ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #region ניהול הגדרות ממשק

        /// <summary>
        /// מודל להגדרות ממשק המשתמש
        /// </summary>
        public class UISettings
        {
            public double OperationsAreaHeight { get; set; } = 2; // יחס גודל התחלתי
            public double NotificationsAreaHeight { get; set; } = 1; // יחס גודל התחלתי
            public bool NotificationsCollapsed { get; set; } = false;
            public double WindowWidth { get; set; } = 900;
            public double WindowHeight { get; set; } = 700;
        }

        /// <summary>
        /// שמירת הגדרות ממשק המשתמש
        /// </summary>
        private void SaveUISettings()
        {
            try
            {
                var settings = new UISettings
                {
                    // שמור את היחס בין האזורים - עכשיו עם המיקומים הנכונים
                    OperationsAreaHeight = GetGridRowHeight(2), // אזור עמודות ופעולות
                    NotificationsAreaHeight = GetGridRowHeight(5), // אזור התראות
                    NotificationsCollapsed = _notificationsCollapsed,
                    WindowWidth = this.Width,
                    WindowHeight = this.Height
                };

                var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
                File.WriteAllText(SETTINGS_FILE, json, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                // שגיאה בשמירת הגדרות - לא קריטית
                AddWarningNotification("שמירת הגדרות", "לא ניתן לשמור הגדרות ממשק", ex.Message);
            }
        }

        /// <summary>
        /// טעינת הגדרות ממשק המשתמש
        /// </summary>
        private void LoadUISettings()
        {
            try
            {
                if (!File.Exists(SETTINGS_FILE))
                    return;

                var json = File.ReadAllText(SETTINGS_FILE, Encoding.UTF8);
                var settings = JsonConvert.DeserializeObject<UISettings>(json);

                if (settings != null)
                {
                    // החזר גדלי אזורים - עכשיו עם המיקומים הנכונים
                    SetGridRowHeight(2, settings.OperationsAreaHeight); // אזור עמודות ופעולות
                    SetGridRowHeight(5, settings.NotificationsAreaHeight); // אזור התראות
                    
                    // החזר מצב כיווץ התראות
                    _notificationsCollapsed = settings.NotificationsCollapsed;
                    if (_notificationsCollapsed && NotificationsScrollViewer != null && CollapseNotificationsBtn != null)
                    {
                        NotificationsScrollViewer.Visibility = Visibility.Collapsed;
                        CollapseNotificationsBtn.Content = "📂";
                    }

                    // החזר גודל חלון
                    if (settings.WindowWidth > 0 && settings.WindowHeight > 0)
                    {
                        this.Width = Math.Max(settings.WindowWidth, 600); // מינימום רוחב
                        this.Height = Math.Max(settings.WindowHeight, 500); // מינימום גובה
                    }
                }
            }
            catch (Exception ex)
            {
                // שגיאה בטעינת הגדרות - לא קריטית, השתמש בברירות מחדל
                AddWarningNotification("טעינת הגדרות", "לא ניתן לטעון הגדרות ממשק, נטענות ברירות מחדל", ex.Message);
            }
        }

        /// <summary>
        /// קבלת גובה שורה בגריד
        /// </summary>
        private double GetGridRowHeight(int rowIndex)
        {
            var grid = FindName("MainGrid") as Grid ?? 
                      (this.Content as Grid);
            
            if (grid != null && rowIndex < grid.RowDefinitions.Count)
            {
                var rowDefinition = grid.RowDefinitions[rowIndex];
                return rowDefinition.Height.Value;
            }
            return 1.0; // ברירת מחדל
        }

        /// <summary>
        /// הגדרת גובה שורה בגריד
        /// </summary>
        private void SetGridRowHeight(int rowIndex, double height)
        {
            var grid = FindName("MainGrid") as Grid ?? 
                      (this.Content as Grid);
            
            if (grid != null && rowIndex < grid.RowDefinitions.Count)
            {
                var rowDefinition = grid.RowDefinitions[rowIndex];
                rowDefinition.Height = new GridLength(Math.Max(height, 0.5), GridUnitType.Star);
            }
        }

        /// <summary>
        /// אירוע סגירת חלון - שמור הגדרות
        /// </summary>
        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveUISettings();
        }

        /// <summary>
        /// איפוס הגדרות ממשק לברירת מחדל
        /// </summary>
        public void ResetUIToDefault()
        {
            try
            {
                // החזר גדלי אזורים לברירת מחדל - עכשיו עם המיקומים הנכונים
                SetGridRowHeight(2, 2.0); // אזור עמודות - יחס 2
                SetGridRowHeight(5, 1.0); // אזור התראות - יחס 1
                
                // החזר גודל חלון
                this.Width = 900;
                this.Height = 700;
                
                // החזר מצב התראות
                if (NotificationsScrollViewer != null && CollapseNotificationsBtn != null)
                {
                    _notificationsCollapsed = false;
                    NotificationsScrollViewer.Visibility = Visibility.Visible;
                    CollapseNotificationsBtn.Content = "📦";
                }
                
                AddSuccessNotification("איפוס ממשק", "ממשק המשתמש הוחזר לברירת מחדל");
            }
            catch (Exception ex)
            {
                AddErrorNotification("שגיאה באיפוס ממשק", "לא ניתן לאפס את ממשק המשתמש", ex.Message);
            }
        }

        #endregion

        #region מערכת התראות

        /// <summary>
        /// סוגי התראות זמינים
        /// </summary>
        public enum NotificationType
        {
            Success,
            Error,
            Warning,
            Info
        }

        /// <summary>
        /// מודל התראה
        /// </summary>
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

        /// <summary>
        /// הוספת התראת הצלחה
        /// </summary>
        public void AddSuccessNotification(string title, string message, string? details = null)
        {
            AddNotification(NotificationType.Success, title, message, details);
        }

        /// <summary>
        /// הוספת התראת שגיאה
        /// </summary>
        public void AddErrorNotification(string title, string message, string? details = null)
        {
            AddNotification(NotificationType.Error, title, message, details);
        }

        /// <summary>
        /// הוספת התראת אזהרה
        /// </summary>
        public void AddWarningNotification(string title, string message, string? details = null)
        {
            AddNotification(NotificationType.Warning, title, message, details);
        }

        /// <summary>
        /// הוספת התראת מידע
        /// </summary>
        public void AddInfoNotification(string title, string message, string? details = null)
        {
            AddNotification(NotificationType.Info, title, message, details);
        }

        /// <summary>
        /// הוספת התראה כללית
        /// </summary>
        private void AddNotification(NotificationType type, string title, string message, string? details = null)
        {
            var notification = new NotificationItem
            {
                Type = type,
                Title = title,
                Message = message,
                Details = details,
                IsDetailed = !string.IsNullOrEmpty(details)
            };

            _notifications.Insert(0, notification); // הוסף בראש הרשימה

            // הגבל מספר התראות
            if (_notifications.Count > MAX_NOTIFICATIONS)
            {
                _notifications.RemoveAt(_notifications.Count - 1);
            }

            RefreshNotificationsDisplay();
        }

        /// <summary>
        /// רענון תצוגת ההתראות
        /// </summary>
        private void RefreshNotificationsDisplay()
        {
            if (NotificationsPanel == null) return;

            // נקה את התצוגה הקיימת
            NotificationsPanel.Children.Clear();

            // הסתר הודעת ברירת מחדל אם יש התראות
            if (DefaultMessageBorder != null)
            {
                DefaultMessageBorder.Visibility = _notifications.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
            }

            // הוסף כל התראה
            foreach (var notification in _notifications)
            {
                var notificationElement = CreateNotificationElement(notification);
                NotificationsPanel.Children.Add(notificationElement);
            }

            // עדכן מונה ההתראות
            UpdateNotificationCount();
            
            // עדכן זמן עדכון אחרון
            if (LastNotificationTimeText != null)
            {
                LastNotificationTimeText.Text = DateTime.Now.ToString("HH:mm:ss");
            }

            // גלול למעלה להתראה החדשה
            if (NotificationsScrollViewer != null)
            {
                NotificationsScrollViewer.ScrollToTop();
            }
        }

        /// <summary>
        /// יצירת אלמנט התראה בודד
        /// </summary>
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

            // שורה עליונה - אייקון, כותרת וזמן
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

            // הודעה
            var messageText = new TextBlock
            {
                Text = notification.Message,
                FontSize = 11,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(textColor)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(22, 4, 0, 0)
            };

            mainPanel.Children.Add(messageText);

            // פרטים נוספים (אם יש)
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

            // אנימציה של הופעה
            border.Opacity = 0;
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));
            border.BeginAnimation(UIElement.OpacityProperty, fadeIn);

            return border;
        }

        /// <summary>
        /// קבלת סגנון התראה לפי סוג
        /// </summary>
        private (string icon, string backgroundColor, string borderColor, string textColor) GetNotificationStyle(NotificationType type)
        {
            return type switch
            {
                NotificationType.Success => ("✅", "#D4F4DD", "#28A745", "#155724"),
                NotificationType.Error => ("❌", "#F8D7DA", "#DC3545", "#721C24"),
                NotificationType.Warning => ("⚠️", "#FFF3CD", "#FFC107", "#856404"),
                NotificationType.Info => ("ℹ️", "#CCE7FF", "#007BFF", "#004085"),
                _ => ("📝", "#F8F9FA", "#6C757D", "#495057")
            };
        }

        /// <summary>
        /// עדכון מונה ההתראות
        /// </summary>
        private void UpdateNotificationCount()
        {
            if (NotificationCountBadge == null || NotificationCountText == null) return;

            var count = _notifications.Count;
            
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

        /// <summary>
        /// עדכון הודעת סטטוס המערכת
        /// </summary>
        public void UpdateSystemStatus(string status, bool isHealthy = true)
        {
            if (SystemStatusText == null) return;

            var icon = isHealthy ? "🟢" : "🔴";
            SystemStatusText.Text = $"{icon} {status}";
        }

        #endregion

        #region אירועי ממשק

        /// <summary>
        /// ניקוי כל ההתראות
        /// </summary>
        private void ClearNotifications_Click(object sender, RoutedEventArgs e)
        {
            if (_notifications.Count == 0)
            {
                AddInfoNotification("מידע", "אין התראות למחיקה");
                return;
            }

            var result = MessageBox.Show(
                $"האם אתה בטוח שברצונך למחוק {_notifications.Count} התראות?",
                "מחיקת התראות",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _notifications.Clear();
                RefreshNotificationsDisplay();
                
                // הוסף הודעת אישור
                AddSuccessNotification("הצלחה", "כל ההתראות נוקו");
            }
        }

        /// <summary>
        /// כיווץ/הרחבה של אזור ההתראות
        /// </summary>
        private void ToggleNotifications_Click(object sender, RoutedEventArgs e)
        {
            if (NotificationsScrollViewer == null || CollapseNotificationsBtn == null) return;

            _notificationsCollapsed = !_notificationsCollapsed;

            if (_notificationsCollapsed)
            {
                NotificationsScrollViewer.Visibility = Visibility.Collapsed;
                CollapseNotificationsBtn.Content = "📂";
                AddInfoNotification("ממשק", "אזור ההתראות כווץ");
            }
            else
            {
                NotificationsScrollViewer.Visibility = Visibility.Visible;
                CollapseNotificationsBtn.Content = "📦";
                AddInfoNotification("ממשק", "אזור ההתראות הורחב");
            }

            // שמור הגדרה זו מיידית
            SaveUISettings();
        }

        private void ViewReports_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AddInfoNotification("פתיחת דוחות", "פותח חלון הדוחות...");
                var reportsWindow = new ReportsWindow();
                reportsWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                AddErrorNotification("שגיאת חלון דוחות", "שגיאה בפתיחת חלון הדוחות", ex.Message);
            }
        }

        private void BrowseFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new OpenFileDialog
                {
                    Filter = "CSV Files (*.csv)|*.csv|JSON Files (*.json)|*.json|Excel Files (*.xlsx;*.xls)|*.xlsx;*.xls|XML Files (*.xml)|*.xml|All Files (*.*)|*.*",
                    Title = "בחר קובץ נתונים"
                };

                if (dialog.ShowDialog() == true)
                {
                    FilePathTextBox.Text = dialog.FileName;
                    var fileInfo = new FileInfo(dialog.FileName);
                    
                    FileInfoTextBlock.Text = $"קובץ נבחר: {Path.GetFileName(dialog.FileName)} | גודל: {fileInfo.Length:N0} bytes";
                    
                    AddSuccessNotification(
                        "קובץ נבחר", 
                        $"נבחר: {Path.GetFileName(dialog.FileName)}", 
                        $"גודל: {fileInfo.Length:N0} bytes\nנתיב: {dialog.FileName}"
                    );

                    // טען עמודות אם זה אפשרי
                    LoadFileColumns(dialog.FileName);
                }
            }
            catch (Exception ex)
            {
                AddErrorNotification("שגיאה בבחירת קובץ", "לא ניתן לבחור את הקובץ", ex.Message);
            }
        }

        private void LoadFileColumns(string filePath)
        {
            try
            {
                _columnNames.Clear();
                _columnSettings.Clear();

                var extension = Path.GetExtension(filePath).ToLower();
                
                switch (extension)
                {
                    case ".csv":
                        LoadCsvColumns(filePath);
                        break;
                    case ".xlsx":
                    case ".xls":
                        LoadExcelColumns(filePath);
                        break;
                    case ".json":
                        LoadJsonColumns(filePath);
                        break;
                    default:
                        AddWarningNotification("פורמט לא נתמך", "לא ניתן לטעון עמודות עבור פורמט קובץ זה");
                        return;
                }

                if (_columnNames.Count > 0)
                {
                    ShowColumnsInterface();
                    AddInfoNotification("עמודות נטענו", $"נטענו {_columnNames.Count} עמודות מהקובץ");
                }
                else
                {
                    AddWarningNotification("אין עמודות", "לא נמצאו עמודות בקובץ");
                }
            }
            catch (Exception ex)
            {
                AddErrorNotification("שגיאה בטעינת עמודות", "לא ניתן לטעון את עמודות הקובץ", ex.Message);
            }
        }

        private void LoadCsvColumns(string filePath)
        {
            using var reader = new StreamReader(filePath, System.Text.Encoding.UTF8);
            var headerLine = reader.ReadLine();
            if (!string.IsNullOrEmpty(headerLine))
            {
                _columnNames = headerLine.Split(',').Select(col => col.Trim()).ToList();
            }
        }

        private void LoadExcelColumns(string filePath)
        {
            using var package = new ExcelPackage(new FileInfo(filePath));
            var worksheet = package.Workbook.Worksheets.First();
            
            for (int col = 1; col <= worksheet.Dimension.End.Column; col++)
            {
                var cellValue = worksheet.Cells[1, col].Value?.ToString();
                if (!string.IsNullOrEmpty(cellValue))
                {
                    _columnNames.Add(cellValue);
                }
            }
        }

        private void LoadJsonColumns(string filePath)
        {
            var jsonText = File.ReadAllText(filePath);
            var jsonArray = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(jsonText);
            
            if (jsonArray?.Count > 0)
            {
                _columnNames = jsonArray[0].Keys.ToList();
            }
        }

        private void ShowColumnsInterface()
        {
            NoFileMessageTextBlock.Visibility = Visibility.Collapsed;
            GlobalOperationsPanel.Visibility = Visibility.Visible;
            ColumnsScrollViewer.Visibility = Visibility.Visible;

            ColumnsPanel.Children.Clear();

            foreach (var columnName in _columnNames)
            {
                var columnPanel = CreateColumnPanel(columnName);
                ColumnsPanel.Children.Add(columnPanel);
                _columnSettings[columnName] = new ColumnSettings();
            }
        }

        private Border CreateColumnPanel(string columnName)
        {
            var border = new Border
            {
                Style = (Style)FindResource("ColumnPanel"),
                Margin = new Thickness(0, 0, 0, 10)
            };

            var stackPanel = new StackPanel();

            // כותרת העמודה
            var headerText = new TextBlock
            {
                Text = $"📊 {columnName}",
                FontWeight = FontWeights.SemiBold,
                FontSize = 14,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2C3E50")),
                Margin = new Thickness(0, 0, 0, 10)
            };
            stackPanel.Children.Add(headerText);

            // פעולות זמינות
            var operationsPanel = new WrapPanel();

            // פעולות ניקוי
            var cleaningGroup = CreateOperationGroup("🧹 ניקוי", new[]
            {
                ("הסר אם ריק", "remove_if_missing"),
                ("החלף ערכים ריקים", "replace_nulls"),
                ("נקה רווחים", "strip_whitespace")
            }, columnName);
            operationsPanel.Children.Add(cleaningGroup);

            // פעולות טרנספורמציה
            var transformGroup = CreateOperationGroup("🔄 טרנספורמציה", new[]
            {
                ("הפוך לאותיות גדולות", "to_uppercase"),
                ("הפוך לאותיות קטנות", "to_lowercase"),
                ("המר טיפוס", "cast_type")
            }, columnName);
            operationsPanel.Children.Add(transformGroup);

            // פעולות אימות
            var validationGroup = CreateOperationGroup("✅ אימות", new[]
            {
                ("שדה חובה", "required_field"),
                ("אמת טווח מספרי", "validate_numeric_range"),
                ("אמת אורך טקסט", "validate_text_length")
            }, columnName);
            operationsPanel.Children.Add(validationGroup);

            // פעולות אגרגציה
            var aggregationGroup = CreateOperationGroup("📊 אגרגציה", new[]
            {
                ("סכום", "sum"),
                ("ממוצע", "average"),
                ("ספירה", "count"),
                ("מינימום", "min"),
                ("מקסימום", "max"),
                ("קיבוץ לפי", "group_by")
            }, columnName);
            operationsPanel.Children.Add(aggregationGroup);

            stackPanel.Children.Add(operationsPanel);
            border.Child = stackPanel;

            return border;
        }

        private Border CreateOperationGroup(string title, (string displayName, string operationName)[] operations, string columnName)
        {
            var groupBorder = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8F9FA")),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E9ECEF")),
                BorderThickness = new Thickness(1, 1, 1, 1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(10, 10, 10, 10),
                Margin = new Thickness(0, 0, 10, 10),
                MinWidth = 200
            };

            var stackPanel = new StackPanel();

            var titleText = new TextBlock
            {
                Text = title,
                FontWeight = FontWeights.SemiBold,
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 8)
            };
            stackPanel.Children.Add(titleText);

            foreach (var (displayName, operationName) in operations)
            {
                var checkBox = new CheckBox
                {
                    Content = displayName,
                    Tag = $"{columnName}:{operationName}",
                    Margin = new Thickness(0, 0, 0, 4)
                };
                checkBox.Checked += OperationCheckBox_Changed;
                checkBox.Unchecked += OperationCheckBox_Changed;
                stackPanel.Children.Add(checkBox);
            }

            groupBorder.Child = stackPanel;
            return groupBorder;
        }

        private void OperationCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isApplyingConfig) return; // prevent loops while applying config

            if (sender is CheckBox checkBox && checkBox.Tag is string tag)
            {
                var parts = tag.Split(':');
                if (parts.Length == 2)
                {
                    var columnName = parts[0];
                    var operationName = parts[1];

                    if (_columnSettings.ContainsKey(columnName))
                    {
                        if (checkBox.IsChecked == true)
                        {
                            _columnSettings[columnName].Operations.Add(operationName);
                        }
                        else
                        {
                            _columnSettings[columnName].Operations.Remove(operationName);
                        }
                    }
                }
            }
        }

        private void ResetSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = MessageBox.Show(
                    "האם ברצונך לאפס גם את הגדרות הממשק (גדלי אזורים) לברירת מחדל?",
                    "איפוס הגדרות",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Cancel)
                    return;

                // איפוס הגדרות נתונים
                _columnSettings.Clear();
                FilePathTextBox.Text = string.Empty;
                FileInfoTextBlock.Text = "לא נבחר קובץ";
                
                // הסתרת ממשק העמודות
                NoFileMessageTextBlock.Visibility = Visibility.Visible;
                GlobalOperationsPanel.Visibility = Visibility.Collapsed;
                ColumnsScrollViewer.Visibility = Visibility.Collapsed;
                
                // איפוס כל ה-checkboxes
                ResetCheckBoxesInPanel(this);

                // איפוס הגדרות ממשק אם המשתמש רצה
                if (result == MessageBoxResult.Yes)
                {
                    ResetUIToDefault();
                    AddInfoNotification("איפוס מלא", "כל ההגדרות וממשק המשתמש אופסו לברירת מחדל");
                }
                else
                {
                    AddInfoNotification("איפוס נתונים", "הגדרות הנתונים אופסו, הגדרות הממשק נשמרו");
                }
            }
            catch (Exception ex)
            {
                AddErrorNotification("שגיאה באיפוס", "לא ניתן לאפס את ההגדרות", ex.Message);
            }
        }

        private void ResetCheckBoxesInPanel(DependencyObject parent)
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);

                if (child is CheckBox checkBox)
                {
                    checkBox.IsChecked = false;
                }
                else
                {
                    ResetCheckBoxesInPanel(child);
                }
            }
        }

        private void SaveConfig_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var config = BuildPipelineConfig();
                if (config == null)
                {
                    AddWarningNotification("בעיה בקונפיגורציה", "לא ניתן לבנות קונפיגורציה - וודא שנבחר קובץ");
                    return;
                }

                var json = JsonConvert.SerializeObject(config, Formatting.Indented);

                var saveDialog = new SaveFileDialog
                {
                    Filter = "JSON Files (*.json)|*.json",
                    Title = "שמור קונפיגורציה",
                    FileName = "pipeline_config.json"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    File.WriteAllText(saveDialog.FileName, json, System.Text.Encoding.UTF8);
                    AddSuccessNotification(
                        "קונפיגורציה נשמרה", 
                        "הקובץ נשמר בהצלחה למיקום הנבחר",
                        $"נתיב: {saveDialog.FileName}\nגודל: {new FileInfo(saveDialog.FileName).Length} bytes"
                    );
                }
            }
            catch (Exception ex)
            {
                AddErrorNotification("שגיאה בשמירת קונפיגורציה", "לא ניתן לשמור את הקונפיגורציה", ex.Message);
            }
        }

        private void LoadConfig_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "JSON Files (*.json)|*.json",
                Title = "בחר קובץ קונפיגורציה"
            };
            if (dlg.ShowDialog() != true) return;

            if (!TryReadConfigFromJson(dlg.FileName, out var cfg, out var err))
            {
                AddErrorNotification("שגיאה בטעינת קונפיג", "לא ניתן לטעון את הקובץ", err);
                return;
            }

            _loadedConfig = cfg!;
            AddSuccessNotification("קונפיגורציה נטענה", $"נטען: {System.IO.Path.GetFileName(dlg.FileName)}");
            ApplyConfigToUI(_loadedConfig);
        }

        private void ApplyConfigToUI(PipelineConfig cfg)
        {
            _isApplyingConfig = true;
            try
            {
                // 1) אם יש נתיב קובץ ב-source ונותן לטעון עמודות – נטען כדי ליצור את הצ׳קבוקסים הדינמיים
                var sourcePath = cfg.Source?.Path;
                if (!string.IsNullOrWhiteSpace(sourcePath) && File.Exists(sourcePath))
                {
                    // מציג את הנתיב בתיבה ויטעין את העמודות (כמו BrowseFile_Click)
                    FilePathTextBox.Text = sourcePath;
                    LoadFileColumns(sourcePath);
                }

                // 2) אפליקציה של פעולות גלובליות (cleaner ללא column)
                var globalActions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var perColumnOps = new List<(string column, string action)>();

                foreach (var p in cfg.Processors ?? Array.Empty<ProcessorConfig>())
                {
                    if (!p.Config.TryGetValue("operations", out var opsObj) || opsObj == null) continue;

                    // ה־Dictionary<string,object> מגיע מ-Newtonsoft ולכן value לרוב יהיה JArray/JObject
                    if (opsObj is JArray jarr)
                    {
                        foreach (var tok in jarr.OfType<JObject>())
                        {
                            var action = (string?)tok["action"];
                            var column = (string?)tok["column"];
                            if (string.IsNullOrWhiteSpace(action)) continue;

                            if (string.IsNullOrWhiteSpace(column))
                                globalActions.Add(action);
                            else
                                perColumnOps.Add((column, action));
                        }
                    }
                }

                // 3) סנכרון שלושת הצ׳קבוקסים הגלובליים הקיימים במסך
                RemoveEmptyRowsCheckBox.IsChecked = globalActions.Contains("remove_empty_rows");
                RemoveDuplicatesCheckBox.IsChecked = globalActions.Contains("remove_duplicates");
                StripWhitespaceCheckBox.IsChecked  = globalActions.Contains("strip_whitespace");

                // 4) סימון פעולות לפי עמודות (אם כבר נטענו עמודות ונוצרו הצ׳קבוקסים הדינמיים)
                if (ColumnsPanel != null && ColumnsPanel.Children.Count > 0 && perColumnOps.Count > 0)
                {
                    foreach (var (column, action) in perColumnOps)
                    {
                        var tag = $"{column}:{action}";
                        var cb = FindCheckBoxByTag(ColumnsPanel, tag);
                        if (cb != null) cb.IsChecked = true;
                    }
                }
                else if (perColumnOps.Count > 0 && !string.IsNullOrWhiteSpace(sourcePath) && !File.Exists(sourcePath))
                {
                    // יש פעולות לפי עמודות, אבל לא ניתן היה לטעון עמודות כי הקובץ לא קיים/לא נגיש
                    AddWarningNotification("קובץ מקור לא נטען",
                        "זיהיתי פעולות לפי עמודות בקונפיגורציה, אך לא נטענו עמודות (הקובץ ב-source.path לא נמצא). " +
                        "בחר קובץ נתונים זהה לזה שבקונפיג כדי לסמן אוטומטית את הצ׳קבוקסים של העמודות.");
                }
            }
            finally
            {
                _isApplyingConfig = false;
            }
        }

        private CheckBox? FindCheckBoxByTag(DependencyObject root, string tag)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is CheckBox cb && cb.Tag is string s && string.Equals(s, tag, StringComparison.OrdinalIgnoreCase))
                    return cb;

                var inner = FindCheckBoxByTag(child, tag);
                if (inner != null) return inner;
            }
            return null;
        }

        private async void SaveAsServerPipeline_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // קח קונפיג קיים או בנה מה־UI
                var cfg = _loadedConfig ?? BuildPipelineConfig();
                if (cfg == null)
                {
                    AddErrorNotification("שגיאת קונפיג", "אין קונפיגורציה תקינה");
                    return;
                }

                // הצע שם ברירת מחדל לפי קובץ הנתונים אם יש
                string baseName;
                var fp = FilePathTextBox != null ? FilePathTextBox.Text : null;
                if (!string.IsNullOrWhiteSpace(fp))
                    baseName = System.IO.Path.GetFileNameWithoutExtension(fp);
                else
                    baseName = $"Pipeline {System.DateTime.Now:yyyy-MM-dd HH:mm}";

                // בקשת שם מהמשתמש
                var dlg = new PipeWiseClient.Windows.PipelineNameDialog($"{baseName} – שמור");
                dlg.Owner = this;
                var ok = dlg.ShowDialog() == true;
                if (!ok || string.IsNullOrWhiteSpace(dlg.PipelineName))
                    return;

                // ודא יעד בטוח תחת output
                EnsureSafeTargetPath(cfg, fp ?? string.Empty);

                // שמירה לשרת עם שם
                var resp = await _api.CreatePipelineAsync(cfg, name: dlg.PipelineName);

                AddSuccessNotification("Pipeline נשמר בשרת",
                    $"'{dlg.PipelineName}' (ID: {resp?.id})",
                    resp?.message ?? "נשמר בהצלחה. ניתן לחפש לפי השם בעמוד הפייפליינים.");
            }
            catch (Exception ex)
            {
                AddErrorNotification("שגיאה בשמירה לשרת", "לא ניתן לשמור את הפייפליין", ex.Message);
            }
        }

        private async void RunSavedPipeline_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // חלון בחירת פייפליין
                var picker = new PipeWiseClient.Windows.PipelinePickerWindow
                {
                    Owner = this
                };

                var ok = picker.ShowDialog() == true && picker.SelectedPipeline != null;
                if (!ok)
                {
                    // המשתמש סגר או ביטל
                    AddInfoNotification("בחירה בוטלה", "לא נבחר פייפליין.");
                    return;
                }

                var p = picker.SelectedPipeline!;

                // הודעת אישור – בלי פתיחת דיאלוג קובץ
                var confirm = MessageBox.Show(
                    $"פייפליין \"{p.name}\" נבחר.\n\n" +
                    $"בלחיצה על 'אישור' תתבצע הרצה של הפייפליין.\n" +
                    $"בלחיצה על 'ביטול' הבחירה תבוטל ולא תתבצע הרצה.",
                    "אישור הרצת פייפליין",
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Question,
                    MessageBoxResult.OK);

                if (confirm != MessageBoxResult.OK)
                {
                    AddInfoNotification("בחירה בוטלה", $"הפייפליין '{p.name}' לא הורץ.");
                    return;
                }

                // הרצה (ללא קובץ קלט – השרת תומך באופציונלי)
                UpdateSystemStatus("מריץ פייפליין שמור…", true);
                AddInfoNotification("הרצה", $"מריץ את '{p.name}'");

                var runResult = await _api.RunPipelineByIdAsync(p.id, filePath: null);

                AddSuccessNotification("הרצה הושלמה", $"'{p.name}' הופעל בהצלחה", runResult?.message);
                UpdateSystemStatus("המערכת פועלת תקין", true);
            }
            catch (Exception ex)
            {
                AddErrorNotification("שגיאה בהרצת פייפליין", "לא ניתן להריץ את הפייפליין שנבחר", ex.Message);
                UpdateSystemStatus("שגיאה במערכת", false);
            }
        }

        private async void RunPipeline_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(FilePathTextBox.Text) || !File.Exists(FilePathTextBox.Text))
                {
                    AddWarningNotification("קובץ חסר", "יש לבחור קובץ מקור קיים לפני הרצת Pipeline");
                    return;
                }

                UpdateSystemStatus("מעבד נתונים...", true);
                AddInfoNotification("התחלת עיבוד", "מריץ Pipeline...", "מכין קונפיגורציה ושולח בקשה לשרת");

                // ✦ אם נטען קובץ קונפיג – נשתמש בו; אחרת נבנה מה־UI (הקוד הקיים שלך)
                var cfg = _loadedConfig ?? BuildPipelineConfig();
                if (cfg?.Source == null || cfg.Target == null)
                {
                    AddErrorNotification("שגיאת קונפיגורציה", "לא ניתן לבנות קונפיגורציה תקינה");
                    return;
                }

                // לעקביות, נעדכן את מקור הנתונים לקובץ שבחרת עכשיו
                cfg.Source.Path = FilePathTextBox.Text;

                // ✦ חובה: יעד תחת תיקיית output שהשרת אוכף
                EnsureSafeTargetPath(cfg, FilePathTextBox.Text);

                // ✦ שליחה באמצעות מחלקת ה-API שלנו (ולא HttpClient ידני)
                var text = await _api.RunAdHocPipelineAsync(
                    filePath: FilePathTextBox.Text,
                    config: cfg,
                    report: new RunReportSettings { generate_html = true, generate_pdf = true, auto_open_html = false }
                );

                AddSuccessNotification("Pipeline הושלם!", "העיבוד הסתיים בהצלחה", $"תגובת שרת:\n{text}");
                UpdateSystemStatus("המערכת פועלת תקין", true);
            }
            catch (Exception ex)
            {
                AddErrorNotification("שגיאה בהרצת Pipeline", ex.Message, ex.StackTrace);
                UpdateSystemStatus("שגיאה במערכת", false);
            }
        }

        private PipelineConfig? BuildPipelineConfig()
        {
            try
            {
                if (string.IsNullOrEmpty(FilePathTextBox.Text))
                    return null;

                var processors = new List<ProcessorConfig>();

                // הוסף פעולות גלובליות
                var globalOperations = new List<Dictionary<string, object>>();
                
                if (RemoveEmptyRowsCheckBox?.IsChecked == true)
                    globalOperations.Add(new Dictionary<string, object> { ["action"] = "remove_empty_rows" });
                
                if (RemoveDuplicatesCheckBox?.IsChecked == true)
                    globalOperations.Add(new Dictionary<string, object> { ["action"] = "remove_duplicates" });
                
                if (StripWhitespaceCheckBox?.IsChecked == true)
                    globalOperations.Add(new Dictionary<string, object> { ["action"] = "strip_whitespace" });

                // הוסף פעולות ספציפיות לעמודות
                var cleaningOps = new List<Dictionary<string, object>>();
                var transformOps = new List<Dictionary<string, object>>();
                var validationOps = new List<Dictionary<string, object>>();
                var aggregationOps = new List<Dictionary<string, object>>();

                foreach (var kvp in _columnSettings)
                {
                    var columnName = kvp.Key;
                    var settings = kvp.Value;

                    foreach (var operation in settings.Operations)
                    {
                        var opDict = new Dictionary<string, object>
                        {
                            ["action"] = operation,
                            ["column"] = columnName
                        };

                        if (operation.StartsWith("remove_") || operation.StartsWith("replace_") || operation == "strip_whitespace")
                        {
                            cleaningOps.Add(opDict);
                        }
                        else if (operation.StartsWith("to_") || operation == "cast_type")
                        {
                            transformOps.Add(opDict);
                        }
                        else if (operation.StartsWith("validate_") || operation == "required_field")
                        {
                            validationOps.Add(opDict);
                        }
                        else if (operation == "sum" || operation == "average" || operation == "count" || 
                                operation == "min" || operation == "max" || operation == "group_by")
                        {
                            aggregationOps.Add(opDict);
                        }
                    }
                }

                // צור processors
                if (globalOperations.Count > 0 || cleaningOps.Count > 0)
                {
                    var allCleaningOps = globalOperations.Concat(cleaningOps).ToList();
                    processors.Add(new ProcessorConfig
                    {
                        Type = "cleaner",
                        Config = new Dictionary<string, object> { ["operations"] = allCleaningOps }
                    });
                }

                if (transformOps.Count > 0)
                {
                    processors.Add(new ProcessorConfig
                    {
                        Type = "transformer",
                        Config = new Dictionary<string, object> { ["operations"] = transformOps }
                    });
                }

                if (validationOps.Count > 0)
                {
                    processors.Add(new ProcessorConfig
                    {
                        Type = "validator",
                        Config = new Dictionary<string, object> { ["operations"] = validationOps }
                    });
                }

                if (aggregationOps.Count > 0)
                {
                    processors.Add(new ProcessorConfig
                    {
                        Type = "aggregator",
                        Config = new Dictionary<string, object> { ["operations"] = aggregationOps }
                    });
                }

                // אם אין processors, הוסף cleaner בסיסי
                if (processors.Count == 0)
                {
                    processors.Add(new ProcessorConfig
                    {
                        Type = "cleaner",
                        Config = new Dictionary<string, object>
                        {
                            ["operations"] = new[]
                            {
                                new Dictionary<string, object> { ["action"] = "remove_empty_rows" },
                                new Dictionary<string, object> { ["action"] = "strip_whitespace" }
                            }
                        }
                    });
                }

                // קבע סוג מקור
                var fileExtension = Path.GetExtension(FilePathTextBox.Text).ToLower();
                var sourceType = fileExtension switch
                {
                    ".csv" => "csv",
                    ".json" => "json",
                    ".xlsx" or ".xls" => "excel",
                    ".xml" => "xml",
                    _ => "csv"
                };

                var baseName = Path.GetFileNameWithoutExtension(FilePathTextBox.Text);
                var outputFileName = $"{baseName}_processed.csv";
                try { Directory.CreateDirectory(OUTPUT_DIR); } catch { /* לא קריטי ללקוח */ }
                var absoluteTargetPath = Path.Combine(OUTPUT_DIR, outputFileName);

                return new PipelineConfig
                {
                    Source = new SourceConfig
                    {
                        Type = sourceType,
                        Path = FilePathTextBox.Text
                    },
                    Processors = processors.ToArray(),
                    Target = new TargetConfig
                    {
                        Type = "csv",
                        Path = absoluteTargetPath   // ← נתיב מלא תחת OUTPUT_DIR
                    }
                };
            }
            catch (Exception ex)
            {
                AddErrorNotification("שגיאה בבניית קונפיגורציה", ex.Message);
                return null;
            }
        }

        private void EnsureSafeTargetPath(PipelineConfig cfg, string dataFilePath)
        {
            // השרת דורש שה־Target.Path יהיה תחת OUTPUT_DIR
            Directory.CreateDirectory(OUTPUT_DIR);

            var baseName = string.IsNullOrWhiteSpace(dataFilePath)
                ? "output"
                : Path.GetFileNameWithoutExtension(dataFilePath);

            var defaultType = "csv";
            var defaultPath = Path.Combine(OUTPUT_DIR, $"{baseName}_processed.csv");

            // אם Target לא מאותחל – אתחול עם required members כבר באובייקט-אינישיאלייזר
            if (cfg.Target == null)
            {
                cfg.Target = new TargetConfig
                {
                    Type = defaultType,
                    Path = defaultPath
                };
                return;
            }

            // אם יש Target אבל חסרים ערכים – מלא ערכי ברירת מחדל
            if (string.IsNullOrWhiteSpace(cfg.Target.Type))
                cfg.Target.Type = defaultType;

            if (string.IsNullOrWhiteSpace(cfg.Target.Path))
                cfg.Target.Path = defaultPath;
        }

        private bool TryReadConfigFromJson(string filePath, out PipelineConfig? cfg, out string? error)
        {
            try
            {
                var json = File.ReadAllText(filePath, Encoding.UTF8);
                cfg = Newtonsoft.Json.JsonConvert.DeserializeObject<PipelineConfig>(json);
                if (cfg == null) { error = "קובץ קונפיג לא תקין."; return false; }
                if (cfg.Source == null || cfg.Target == null) { error = "חסרים source/target בקונפיג."; return false; }
                error = null;
                return true;
            }
            catch (Exception ex)
            {
                cfg = null;
                error = ex.Message;
                return false;
            }
        }

        #endregion
    }

    // מחלקת עזר להגדרות עמודה
    public class ColumnSettings
    {
        public HashSet<string> Operations { get; set; } = new HashSet<string>();
    }
}