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
using PipeWiseClient.Models;
using OfficeOpenXml;
using PipeWiseClient.Helpers;
using PipeWiseClient.Windows;

namespace PipeWiseClient
{
    public partial class MainWindow : Window
    {
        private readonly HttpClient _httpClient = new HttpClient();
        private List<string> _columnNames = new List<string>();
        private Dictionary<string, ColumnSettings> _columnSettings = new Dictionary<string, ColumnSettings>();

        // מערכת התראות - הגדרות בסיסיות
        private List<NotificationItem> _notifications = new List<NotificationItem>();
        private bool _notificationsCollapsed = false;
        private const int MAX_NOTIFICATIONS = 50;

        public MainWindow()
        {
            try
            {
                InitializeComponent();
                
                // אתחול EPPlus
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
                
                // הוספת הודעת ברכה
                AddInfoNotification("ברוך הבא ל-PipeWise", "המערכת מוכנה לעיבוד נתונים");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"שגיאה באתחול החלון: {ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

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
            }
            else
            {
                NotificationsScrollViewer.Visibility = Visibility.Visible;
                CollapseNotificationsBtn.Content = "📦";
            }
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
                // איפוס הגדרות
                _columnSettings.Clear();
                FilePathTextBox.Text = string.Empty;
                FileInfoTextBlock.Text = "לא נבחר קובץ";
                
                // הסתרת ממשק העמודות
                NoFileMessageTextBlock.Visibility = Visibility.Visible;
                GlobalOperationsPanel.Visibility = Visibility.Collapsed;
                ColumnsScrollViewer.Visibility = Visibility.Collapsed;
                
                // איפוס כל ה-checkboxes
                ResetCheckBoxesInPanel(this);
                
                AddInfoNotification("איפוס הגדרות", "כל ההגדרות אופסו והממשק חזר למצב התחלתי");
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

        private async void RunPipeline_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // בדיקות ראשוניות
                if (string.IsNullOrWhiteSpace(FilePathTextBox.Text))
                {
                    AddWarningNotification("קובץ חסר", "יש לבחור קובץ מקור לפני הרצת Pipeline");
                    return;
                }

                if (!File.Exists(FilePathTextBox.Text))
                {
                    AddErrorNotification("קובץ לא נמצא", "הקובץ הנבחר לא קיים במערכת");
                    return;
                }

                AddInfoNotification("התחלת עיבוד", "מריץ Pipeline...", "מכין קונפיגורציה ושולח בקשה לשרת");
                UpdateSystemStatus("מעבד נתונים...", true);

                var config = BuildPipelineConfig();
                if (config?.Source == null)
                {
                    AddErrorNotification("שגיאת קונפיגורציה", "לא ניתן לבנות קונפיגורציה תקינה");
                    return;
                }

                var json = JsonConvert.SerializeObject(config, Formatting.Indented);

                var content = new MultipartFormDataContent();
                content.Add(new ByteArrayContent(File.ReadAllBytes(FilePathTextBox.Text)), "file", Path.GetFileName(FilePathTextBox.Text));
                content.Add(new StringContent(json, Encoding.UTF8, "application/json"), "config");
                
                var response = await _httpClient.PostAsync("http://127.0.0.1:8000/run-pipeline", content);
                var result = await response.Content.ReadAsStringAsync();
                
                if (response.IsSuccessStatusCode)
                {
                    AddSuccessNotification("Pipeline הושלם!", "העיבוד הסתיים בהצלחה", $"תגובת שרת:\n{result}");
                    UpdateSystemStatus("המערכת פועלת תקין", true);
                }
                else
                {
                    AddErrorNotification("שגיאת שרת", $"השרת החזיר שגיאה ({response.StatusCode})", result);
                    UpdateSystemStatus("שגיאה בעיבוד", false);
                }
            }
            catch (Exception ex)
            {
                AddErrorNotification("שגיאה בהרצת Pipeline", ex.Message, ex.StackTrace);
                UpdateSystemStatus("שגיאה במערכת", false);
            }
        }

        #endregion

        #region פונקציות עזר

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

                // קבע קובץ פלט
                var outputFileName = Path.GetFileNameWithoutExtension(FilePathTextBox.Text) + "_processed.csv";

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
                        Path = outputFileName
                    }
                };
            }
            catch (Exception ex)
            {
                AddErrorNotification("שגיאה בבניית קונפיגורציה", ex.Message);
                return null;
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