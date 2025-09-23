// PipeWise_Client/MainWindow.xaml.cs

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Win32;
using Newtonsoft.Json;
using OfficeOpenXml;
using System.Threading;

using PipeWiseClient.Helpers;
using PipeWiseClient.Models;
using PipeWiseClient.Services;
using PipeWiseClient.Windows;
using Newtonsoft.Json.Linq;
using System.Xml.Linq;

namespace PipeWiseClient
{
    public partial class MainWindow : Window
    {
        private readonly ApiClient _api = new();
        private List<string> _columnNames = new List<string>();
        private Dictionary<string, ColumnSettings> _columnSettings = new Dictionary<string, ColumnSettings>();
        private PipelineConfig? _loadedConfig;
        private const string OUTPUT_DIR = @"C:\Users\shlom\PipeWise\output";
        private List<NotificationItem> _notifications = new List<NotificationItem>();
        private bool _notificationsCollapsed = false;
        private const int MAX_NOTIFICATIONS = 50;
        private bool _isApplyingConfig = false;
        private CancellationTokenSource? _runCts;
        private bool _hasCompatibleConfig = false;
        private bool _hasLastRunReport = false;
        private bool _hasFile => !string.IsNullOrWhiteSpace(FilePathTextBox?.Text) && File.Exists(FilePathTextBox.Text);
        private const string SETTINGS_FILE = "ui_settings.json";
        private static readonly string[] DATE_INPUT_FORMATS = new[]
        {
            "%d-%m-%Y", "%d/%m/%Y", "%d.%m.%Y",
            "%Y-%m-%d", "%Y/%m/%d", "%m/%d/%Y", "%m-%d-%Y",
            "%d-%m-%Y %H:%M:%S", "%d/%m/%Y %H:%M:%S", "%d.%m.%Y %H:%M:%S",
            "%Y-%m-%d %H:%M:%S", "%Y-%m-%dT%H:%M:%S", "%m/%d/%Y %H:%M:%S",

            "%d-%m-%y", "%d/%m/%y", "%d.%m.%y",
            "%y-%m-%d", "%y/%m/%d", "%m/%d/%y",
            "%d-%m-%y %H:%M:%S", "%d/%m/%y %H:%M:%S", "%y-%m-%d %H:%M:%S",
        };
        public MainWindow()
        {
            try
            {
                InitializeComponent();

                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

                LoadUISettings();

                AddInfoNotification("ברוך הבא ל-PipeWise", "המערכת מוכנה לעיבוד נתונים");

                this.Closing += MainWindow_Closing;

                SetPhase(UiPhase.Idle);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"שגיאה באתחול החלון: {ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #region ===== UI Phase Management =====

        public enum UiPhase
        {
            Idle,                   // אין קובץ/קונפיג
            FileSelected,           // נבחר קובץ
            ConfigLoadedCompatible, // נטען קובץ + קונפיג תואם
            ConfigLoadedMismatch,   // נטען קובץ + קונפיג לא תואם
            Running,                // תהליך בעבודה
            Completed               // ריצה הסתיימה (יש דוח/פלט)
        }

        private void SetPhase(UiPhase next)
        {
            _phase = next;
            UpdateUiByPhase();

            // Progress & Cancel visibility
            if (RunProgressBar != null) RunProgressBar.Visibility = _phase == UiPhase.Running ? Visibility.Visible : Visibility.Collapsed;
            if (RunProgressText != null) RunProgressText.Visibility = _phase == UiPhase.Running ? Visibility.Visible : Visibility.Collapsed;
            Btn("CancelRunBtn").Let(b => b.IsEnabled = _phase == UiPhase.Running);
        }


        private void UpdateUiByPhase()
        {
            // מאתרים כפתורים לפי x:Name (אם לא קיימים — מתעלמים)
            Btn("BrowseFileBtn").Let((Button b) =>
                b.IsEnabled = _phase is UiPhase.Idle or UiPhase.FileSelected or UiPhase.ConfigLoadedCompatible or UiPhase.ConfigLoadedMismatch or UiPhase.Completed);

            Btn("LoadConfigBtn").Let((Button b) =>
                b.IsEnabled = _hasFile && _phase != UiPhase.Running);

            Btn("SaveConfigBtn").Let((Button b) =>
                b.IsEnabled = _hasFile && _phase != UiPhase.Running);

            Btn("RunBtn").Let((Button b) =>
            {
                var canRun =
                    _hasFile &&
                    _phase != UiPhase.Running &&
                    // מרשה ריצה אד-הוק אם אין קונפיג טעון או אם יש קונפיג תואם
                    (_loadedConfig == null || _hasCompatibleConfig);

                b.IsEnabled = canRun;

                b.ToolTip = canRun ? null :
                    (!_hasFile ? "יש לבחור קובץ לפני הרצה"
                     : (_loadedConfig != null && !_hasCompatibleConfig ? "הקונפיגורציה אינה תואמת לקובץ" : "הפעולה אינה זמינה כעת"));
            });

            Btn("RunSavedPipelineBtn").Let((Button b) =>
                b.IsEnabled = _phase != UiPhase.Running);

            Btn("SaveAsServerPipelineBtn").Let((Button b) =>
                b.IsEnabled = (_hasFile || _loadedConfig != null) && _phase != UiPhase.Running);

            Btn("ViewReportsBtn").Let((Button b) =>
                b.IsEnabled = _hasLastRunReport && _phase != UiPhase.Running);

            Btn("ResetSettingsBtn").Let((Button b) =>
                b.IsEnabled = _phase != UiPhase.Running);

            // עכבר בזמן ריצה
            this.Cursor = _phase == UiPhase.Running ? System.Windows.Input.Cursors.AppStarting : null;
        }

        private Button? Btn(string name) => FindName(name) as Button;

        #endregion

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
                    OperationsAreaHeight = GetGridRowHeight(2),
                    NotificationsAreaHeight = GetGridRowHeight(5),
                    NotificationsCollapsed = _notificationsCollapsed,
                    WindowWidth = this.Width,
                    WindowHeight = this.Height
                };

                var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
                File.WriteAllText(SETTINGS_FILE, json, Encoding.UTF8);
            }
            catch (Exception ex)
            {
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
                    SetGridRowHeight(2, settings.OperationsAreaHeight);
                    SetGridRowHeight(5, settings.NotificationsAreaHeight);

                    _notificationsCollapsed = settings.NotificationsCollapsed;
                    if (_notificationsCollapsed && NotificationsScrollViewer != null && CollapseNotificationsBtn != null)
                    {
                        NotificationsScrollViewer.Visibility = Visibility.Collapsed;
                        CollapseNotificationsBtn.Content = "📂";
                    }

                    if (settings.WindowWidth > 0 && settings.WindowHeight > 0)
                    {
                        this.Width = Math.Max(settings.WindowWidth, 600);
                        this.Height = Math.Max(settings.WindowHeight, 500);
                    }
                }
            }
            catch (Exception ex)
            {
                AddWarningNotification("טעינת הגדרות", "לא ניתן לטעון הגדרות ממשק, נטענות ברירות מחדל", ex.Message);
            }
        }

        private double GetGridRowHeight(int rowIndex)
        {
            var grid = FindName("MainGrid") as Grid ??
                      (this.Content as Grid);

            if (grid != null && rowIndex < grid.RowDefinitions.Count)
            {
                var rowDefinition = grid.RowDefinitions[rowIndex];
                return rowDefinition.Height.Value;
            }
            return 1.0;
        }

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

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveUISettings();
        }

        public void ResetUIToDefault()
        {
            try
            {
                SetGridRowHeight(2, 2.0);
                SetGridRowHeight(5, 1.0);

                this.Width = 900;
                this.Height = 700;

                if (NotificationsScrollViewer != null && CollapseNotificationsBtn != null)
                {
                    _notificationsCollapsed = false;
                    NotificationsScrollViewer.Visibility = Visibility.Visible;
                    CollapseNotificationsBtn.Content = "📦";
                }

                AddSuccessNotification("איפוס ממשק", "ממשק המשתמש הוחזר לברירת מחדל");

                // איפוס סטייט כללי
                _loadedConfig = null;
                _hasCompatibleConfig = false;
                _hasLastRunReport = false;
                SetPhase(UiPhase.Idle);
            }
            catch (Exception ex)
            {
                AddErrorNotification("שגיאה באיפוס ממשק", "לא ניתן לאפס את ממשק המשתמש", ex.Message);
            }
        }

        private async Task DetectColumnTypes(string filePath)
        {
            try
            {
                var extension = Path.GetExtension(filePath).ToLower();
                var fileType = extension switch
                {
                    ".csv" => "csv",
                    ".json" => "json",
                    ".xml" => "xml",
                    ".xlsx" or ".xls" => "excel",
                    _ => throw new NotSupportedException($"סוג קובץ {extension} אינו נתמך")
                };
                var payload = new
                {
                    source = new
                    {
                        type = fileType,
                        path = filePath
                    }
                };

                var profileResult = await _api.ProfileColumnsAsync(payload);

                if (profileResult?.Columns != null)
                {
                    foreach (var column in profileResult.Columns)
                    {
                        if (_columnSettings.ContainsKey(column.Name))
                        {
                            _columnSettings[column.Name].InferredType = column.InferredType;
                        }
                        else
                        {
                            _columnSettings[column.Name] = new ColumnSettings
                            {
                                InferredType = column.InferredType
                            };
                        }
                    }
                }
                if (profileResult?.Columns != null)
                {
                    var debugInfo = string.Join("\n", profileResult.Columns.Select(c =>
                        $"{c.Name}: {c.InferredType}"));
                    AddInfoNotification("DEBUG - סוגי עמודות", debugInfo);
                }
            }
            catch (Exception ex)
            {
                AddWarningNotification("זיהוי סוגי עמודות", "לא ניתן לזהות סוגי עמודות", ex.Message);
            }
        }

        #endregion

        #region מערכת התראות

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

        public void AddSuccessNotification(string title, string message, string? details = null)
        {
            AddNotification(NotificationType.Success, title, message, details);
        }

        public void AddErrorNotification(string title, string message, string? details = null)
        {
            AddNotification(NotificationType.Error, title, message, details);
        }

        public void AddWarningNotification(string title, string message, string? details = null)
        {
            AddNotification(NotificationType.Warning, title, message, details);
        }

        public void AddInfoNotification(string title, string message, string? details = null)
        {
            AddNotification(NotificationType.Info, title, message, details);
        }

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

            _notifications.Insert(0, notification);

            if (_notifications.Count > MAX_NOTIFICATIONS)
            {
                _notifications.RemoveAt(_notifications.Count - 1);
            }

            RefreshNotificationsDisplay();
        }

        private void RefreshNotificationsDisplay()
        {
            if (NotificationsPanel == null) return;

            NotificationsPanel.Children.Clear();

            if (DefaultMessageBorder != null)
            {
                DefaultMessageBorder.Visibility = _notifications.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
            }

            foreach (var notification in _notifications)
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
                NotificationType.Success => ("✅", "#D4F4DD", "#28A745", "#155724"),
                NotificationType.Error => ("❌", "#F8D7DA", "#DC3545", "#721C24"),
                NotificationType.Warning => ("⚠️", "#FFF3CD", "#FFC107", "#856404"),
                NotificationType.Info => ("ℹ️", "#CCE7FF", "#007BFF", "#004085"),
                _ => ("📝", "#F8F9FA", "#6C757D", "#495057")
            };
        }

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

        public void UpdateSystemStatus(string status, bool isHealthy = true)
        {
            if (SystemStatusText == null) return;

            var icon = isHealthy ? "🟢" : "🔴";
            SystemStatusText.Text = $"{icon} {status}";
        }

        #endregion

        #region אירועי ממשק

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

                AddSuccessNotification("הצלחה", "כל ההתראות נוקו");
            }
        }

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

        private async void BrowseFile_Click(object sender, RoutedEventArgs e)
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
                    FilePathTextBox!.Text = dialog.FileName;
                    var fileInfo = new FileInfo(dialog.FileName);

                    FileInfoTextBlock!.Text = $"קובץ נבחר: {Path.GetFileName(dialog.FileName)} | גודל: {fileInfo.Length:N0} bytes";

                    AddSuccessNotification(
                        "קובץ נבחר",
                        $"נבחר: {Path.GetFileName(dialog.FileName)}",
                        $"גודל: {fileInfo.Length:N0} bytes\nנתיב: {dialog.FileName}"
                    );

                    // בחירת קובץ חדש מנטרלת קונפיג טעון קודם (אם היה)
                    _loadedConfig = null;
                    _hasCompatibleConfig = false;
                    _hasLastRunReport = false;

                    await LoadFileColumns(dialog.FileName);

                    SetPhase(UiPhase.FileSelected);
                }
            }
            catch (Exception ex)
            {
                AddErrorNotification("שגיאה בבחירת קובץ", "לא ניתן לבחור את הקובץ", ex.Message);
            }
        }

        private async Task LoadFileColumns(string filePath)
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
                    case ".xml":
                        LoadXmlColumns(filePath);
                        break;
                    default:
                        AddWarningNotification("פורמט לא נתמך", $"לא ניתן לטעון עמודות עבור פורמט קובץ {extension}");
                        return;
                }

                if (_columnNames.Count > 0)
                {
                    await DetectColumnTypes(filePath);
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
            try
            {
                var jsonText = File.ReadAllText(filePath);
                var jsonData = JsonConvert.DeserializeObject(jsonText);
                
                _columnNames.Clear();
                
                // טיפול בarray של objects
                if (jsonData is JArray jsonArray && jsonArray.Count > 0)
                {
                    if (jsonArray[0] is JObject firstObj)
                    {
                        _columnNames = firstObj.Properties().Select(p => p.Name).ToList();
                    }
                }
                // טיפול בobject יחיד
                else if (jsonData is JObject jsonObj)
                {
                    // אם זה object שמכיל arrays, נסה למצוא array ראשון
                    var firstArray = jsonObj.Properties()
                        .Select(p => p.Value)
                        .OfType<JArray>()
                        .FirstOrDefault();
                        
                    if (firstArray?.Count > 0 && firstArray[0] is JObject firstRecord)
                    {
                        _columnNames = firstRecord.Properties().Select(p => p.Name).ToList();
                    }
                    else
                    {
                        // אחרת קח את השדות של הobject עצמו
                        _columnNames = jsonObj.Properties().Select(p => p.Name).ToList();
                    }
                }
                
                if (_columnNames.Count == 0)
                {
                    AddWarningNotification("JSON ריק", "לא נמצאו שדות בקובץ JSON");
                }
            }
            catch (Exception ex)
            {
                AddWarningNotification("שגיאה בטעינת JSON", $"לא ניתן לטעון JSON: {ex.Message}");
                _columnNames.Clear();
            }
        }

        private void LoadXmlColumns(string filePath)
        {
            try 
            {
                var doc = XDocument.Load(filePath);
                
                // חפש את הרקורד הראשון שיש לו elements
                var firstRecord = doc.Descendants()
                    .Where(e => e.HasElements)
                    .FirstOrDefault();
                    
                if (firstRecord != null)
                {
                    _columnNames = firstRecord.Elements()
                        .Select(e => e.Name.LocalName)
                        .Distinct()
                        .ToList();
                }
                else
                {
                    // אם לא נמצא רקורד עם elements, נסה לקחת את כל השמות הייחודיים
                    _columnNames = doc.Descendants()
                        .Where(e => !e.HasElements && !string.IsNullOrWhiteSpace(e.Name.LocalName))
                        .Select(e => e.Name.LocalName)
                        .Distinct()
                        .ToList();
                }
            }
            catch (Exception ex)
            {
                AddWarningNotification("שגיאה בטעינת XML", $"לא ניתן לטעון XML: {ex.Message}");
                _columnNames.Clear();
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

        private string GetSelectedTargetType()
        {
            try
            {
                if (TargetTypeComboBox?.SelectedItem is ComboBoxItem item &&
                    item.Tag is string tag && !string.IsNullOrWhiteSpace(tag))
                {
                    return tag.ToLowerInvariant();
                }
            }
            catch { /* ignore */ }
            return "csv"; // ברירת מחדל תואמת להתנהגות הקודמת
        }

        private static string ExtForTarget(string targetType)
        {
            return targetType switch
            {
                "json" => "json",
                "xml" => "xml",
                "excel" or "xlsx" => "xlsx",
                _ => "csv"
            };
        }

        private Border CreateColumnPanel(string columnName)
        {
            var border = new Border
            {
                Style = (Style)FindResource("ColumnPanel"),
                Margin = new Thickness(0, 0, 0, 10)
            };

            var stackPanel = new StackPanel();

            // Panel עליון עם שם העמודה וכפתור לסימון כתאריך
            var headerPanel = new Grid();
            headerPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var headerText = new TextBlock
            {
                Text = $"📊 {columnName}",
                FontWeight = FontWeights.SemiBold,
                FontSize = 14,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2C3E50")),
                VerticalAlignment = VerticalAlignment.Center
            };

            headerPanel.Children.Add(headerText);
            Grid.SetColumn(headerText, 0);

            stackPanel.Children.Add(headerPanel);
            stackPanel.Children.Add(new TextBlock { Height = 10 });

            var operationsPanel = new WrapPanel();

            var cleaningGroup = CreateOperationGroup("🧹 ניקוי", new[]
            {
                ("הסר מזהה לא חוקי",   "remove_invalid_identifier"),
                ("החלף ערכים ריקים", "replace_empty_values"),
                ("החלף ערכי NULL",  "replace_null_values"),
                ("הסר ערכים ריקים", "remove_empty_values"),
                ("הסר ערכי NULL",   "remove_null_values"),
                ("הפוך לאותיות גדולות", "to_uppercase"),
                ("הפוך לאותיות קטנות",  "to_lowercase"),
                ("הסר תווים מיוחדים",   "remove_special_characters"),
                ("אמת טווח מספרי",      "set_numeric_range"),
                ("קבע פורמט תאריך",     "set_date_format"),
                ("הסר תאריך לא חוקי",   "remove_invalid_dates"),

            }, columnName);
            operationsPanel.Children.Add(cleaningGroup);

            var transformGroup = CreateOperationGroup("🔄 טרנספורמציה", new[]
            {
                ("שנה שם עמודה", "rename_field"),
                ("מזג עמודות", "merge_columns"),
                ("פצל שדה", "split_field"),
                ("המר טיפוס", "cast_type"),
                ("נרמל ערכים מספריים (0-1)", "normalize_numeric"),
                ("קידוד קטגוריאלי", "categorical_encoding")
            }, columnName);
            operationsPanel.Children.Add(transformGroup);

            var aggregationGroup = CreateOperationGroup("📊 אגרגציה", new[]
            {
                ("סכום", "sum"),
                ("ממוצע", "average"),
                ("מינימום", "min"),
                ("מקסימום", "max"),
                ("חציון", "median"),
                ("סטיית תקן", "std"),
                ("שונות", "variance"),
                ("טווח", "range"),
                ("ספירת ערכים תקינים", "count_valid"),
                ("ערכים יחודיים", "count_distinct"),
                ("ערך הכי נפוץ", "most_common"),
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
                if (operationName == "validate_date_format")
                {
                    var columnSetting = _columnSettings.ContainsKey(columnName)
                        ? _columnSettings[columnName]
                        : null;

                    var isDateColumn = columnSetting?.InferredType?.ToLower().Contains("date") == true;

                    if (!isDateColumn)
                        continue;
                }

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
            if (_isApplyingConfig) return;

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

                            if (operationName == "replace_empty_values")
                            {
                                var inferredType = _columnSettings[columnName].InferredType ?? "string";
                                var dlg = new ValuePromptDialog(columnName, inferredType, 255) { Owner = this };
                                var ok = dlg.ShowDialog() == true;
                                if (!ok)
                                {
                                    checkBox.IsChecked = false; // המשתמש ביטל
                                    return;
                                }
                                // שמור את ההגדרה לעמודה
                                var s = _columnSettings[columnName];
                                s.ReplaceEmpty ??= new ReplaceEmptySettings();
                                s.ReplaceEmpty.Value = dlg.ReplacementValue;
                                s.ReplaceEmpty.MaxLength = dlg.MaxLength;
                            }

                            else if (operationName == "replace_null_values")
                            {
                                var inferredType = _columnSettings[columnName].InferredType ?? "string";
                                var dlg = new ValuePromptDialog(columnName, inferredType, 255) { Owner = this };
                                var ok = dlg.ShowDialog() == true;
                                if (!ok) { checkBox.IsChecked = false; return; }
                                var s = _columnSettings[columnName];
                                s.ReplaceNull ??= new ReplaceEmptySettings();
                                s.ReplaceNull.Value = dlg.ReplacementValue;
                                s.ReplaceNull.MaxLength = dlg.MaxLength;
                            }

                            else if (operationName == "set_date_format")
                            {
                                static bool IsTypeSupportedForDateFormat(string? inferred)
                                {
                                    if (string.IsNullOrWhiteSpace(inferred)) return true; // fail-open כדי למנוע אזהרות שווא
                                    var t = inferred.ToLowerInvariant();
                                    return t.Contains("date") || t.Contains("time") || t.Contains("timestamp")
                                        || t.Contains("string") || t.Contains("text") || t.Contains("mixed");
                                }

                                var t = _columnSettings[columnName].InferredType;
                                var looksLikeDate = IsTypeSupportedForDateFormat(t);
                                var dlg = new Windows.DateFormatDialog(columnName, looksLikeDate) { Owner = this };

                                // פותחים תמיד את הדיאלוג
                                var ok = dlg.ShowDialog() == true;

                                // אם המשתמש ביטל או לא נבחר פורמט – מבטלים את הסימון
                                if (!ok || string.IsNullOrWhiteSpace(dlg.SelectedPythonFormat))
                                {
                                    checkBox.IsChecked = false;
                                    return;
                                }

                                // שמירת הפורמט שנבחר
                                var s = _columnSettings[columnName];
                                s.DateFormatApply ??= new DateFormatApplySettings();
                                s.DateFormatApply.TargetFormat = dlg.SelectedPythonFormat!;
                            }

                            else if (operationName == "remove_invalid_dates")
                            {
                                var dlg = new RemoveInvalidDatesDialog(columnName) { Owner = this };
                                var ok = dlg.ShowDialog() == true;
                                if (!ok) { checkBox.IsChecked = false; return; }

                                if (!_columnSettings.TryGetValue(columnName, out var s))
                                {
                                    s = new ColumnSettings();
                                    _columnSettings[columnName] = s;
                                }
                                s.InvalidDateRemoval ??= new InvalidDateRemovalSettings();

                                s.InvalidDateRemoval.MinYear = dlg.MinYear;
                                s.InvalidDateRemoval.MaxYear = dlg.MaxYear;
                                s.InvalidDateRemoval.MinDateIso = dlg.MinDateIso;
                                s.InvalidDateRemoval.MaxDateIso = dlg.MaxDateIso;
                                s.InvalidDateRemoval.EmptyAction = dlg.EmptyAction;
                                s.InvalidDateRemoval.EmptyReplacement = dlg.EmptyReplacement;
                            }

                            else if (operationName == "remove_invalid_identifier")
                            {
                                var dlg = new RemoveInvalidIdentifierDialog(columnName) { Owner = this };
                                var ok = dlg.ShowDialog() == true;
                                if (!ok)
                                {
                                    checkBox.IsChecked = false;
                                    return;
                                }

                                var s = _columnSettings[columnName];
                                s.IdentifierValidation ??= new IdentifierValidationSettings();
                                s.IdentifierValidation.IdType = dlg.IdType;
                                s.IdentifierValidation.TreatWhitespaceAsEmpty = dlg.TreatWhitespaceAsEmpty;
                                s.IdentifierValidation.EmptyAction = dlg.EmptyAction;
                                s.IdentifierValidation.EmptyReplacement = dlg.EmptyAction == "replace" ? dlg.EmptyReplacement : null;

                                // אפס את התתי-אובייקטים ואז מלה אותם לפי הסוג שנבחר
                                s.IdentifierValidation.Numeric = null;
                                s.IdentifierValidation.String = null;
                                s.IdentifierValidation.Uuid = null;

                                if (dlg.IdType == "numeric")
                                {
                                    s.IdentifierValidation.Numeric = new NumericIdentifierOptions
                                    {
                                        IntegerOnly = dlg.NumIntegerOnly,
                                        AllowLeadingZeros = dlg.NumAllowLeadingZeros,
                                        AllowNegative = dlg.NumAllowNegative,
                                        AllowThousandSeparators = dlg.NumAllowThousandSeparators,
                                        MaxDigits = dlg.NumMaxDigits
                                    };
                                }
                                else if (dlg.IdType == "string")
                                {
                                    s.IdentifierValidation.String = new StringIdentifierOptions
                                    {
                                        MinLength = dlg.StrMinLength,
                                        MaxLength = dlg.StrMaxLength,
                                        DisallowWhitespace = dlg.StrDisallowWhitespace,
                                        Regex = dlg.StrRegex
                                    };
                                }
                                else if (dlg.IdType == "uuid")
                                {
                                    s.IdentifierValidation.Uuid = new UuidIdentifierOptions
                                    {
                                        AcceptHyphenated = dlg.UuidAcceptHyphenated, // תמיד true
                                        AcceptBraced = dlg.UuidAcceptBraced,
                                        AcceptUrn = dlg.UuidAcceptUrn
                                    };
                                }
                            }

                            else if (operationName == "normalize_numeric")
                            {
                                var settings = _columnSettings[columnName];
                                settings.NormalizeSettings ??= new NormalizeSettings();
                            }

                            else if (operationName == "rename_field")
                            {
                                var allColumns = _columnNames.ToList();
                                var dialog = new RenameColumnDialog(columnName, allColumns)
                                {
                                    Owner = this
                                };

                                var result = dialog.ShowDialog();

                                if (result != true)
                                {
                                    checkBox.IsChecked = false;
                                    return;
                                }

                                var settings = _columnSettings[columnName];
                                settings.RenameSettings ??= new RenameSettings();
                                settings.RenameSettings.NewName = dialog.NewName;

                                AddInfoNotification("שינוי שם עמודה",
                                    $"העמודה '{columnName}' תשונה ל-'{dialog.NewName}' בעיבוד הנתונים");
                            }

                            else if (operationName == "merge_columns")
                            {
                                var allColumns = _columnNames.ToList();
                                var dialog = new MergeColumnsDialog(allColumns, columnName)
                                {
                                    Owner = this
                                };

                                var result = dialog.ShowDialog();

                                if (result != true)
                                {
                                    checkBox.IsChecked = false;
                                    return;
                                }

                                // שמור את ההגדרות - הוסף את העמודה הנוכחית לרשימה
                                var settings = _columnSettings[columnName];
                                settings.MergeColumnsSettings ??= new MergeColumnsSettings();

                                // צור רשימה שמתחילה עם העמודה הנוכחית
                                var allColumnsToMerge = new List<string> { columnName };
                                allColumnsToMerge.AddRange(dialog.SelectedColumns);

                                settings.MergeColumnsSettings.SourceColumns = allColumnsToMerge;
                                settings.MergeColumnsSettings.TargetColumn = dialog.TargetColumn;
                                settings.MergeColumnsSettings.Separator = dialog.Separator;
                                settings.MergeColumnsSettings.RemoveSourceColumns = dialog.RemoveSourceColumns;
                                settings.MergeColumnsSettings.EmptyHandling = dialog.EmptyHandling;
                                settings.MergeColumnsSettings.EmptyReplacement = dialog.EmptyReplacement;

                                var allColumnsText = string.Join(", ", allColumnsToMerge);
                                AddInfoNotification("מיזוג עמודות",
                                    $"העמודות [{allColumnsText}] ימוזגו לעמודה '{dialog.TargetColumn}' עם מפריד '{dialog.Separator}'");
                            }

                            else if (operationName == "split_field")
                            {
                                var allColumns = _columnNames.ToList();
                                var dialog = new SplitFieldWindow(allColumns)
                                {
                                    Owner = this
                                };

                                var result = dialog.ShowDialog();

                                if (result != true || dialog.Result == null)
                                {
                                    checkBox.IsChecked = false;
                                    return;
                                }

                                var splitConfig = dialog.Result;
                                var settings = _columnSettings[columnName];
                                settings.SplitFieldSettings ??= new SplitFieldSettings();

                                // העתק את הקונפיגורציה לSettings
                                settings.SplitFieldSettings.SplitType = splitConfig.SplitType;
                                settings.SplitFieldSettings.Delimiter = splitConfig.Delimiter;
                                settings.SplitFieldSettings.Length = splitConfig.Length;
                                settings.SplitFieldSettings.TargetFields = splitConfig.TargetFields;
                                settings.SplitFieldSettings.RemoveSource = splitConfig.RemoveSource;

                                var fieldsText = string.Join(", ", splitConfig.TargetFields);
                                AddInfoNotification("פיצול שדה",
                                    $"השדה '{columnName}' יפוצל ל-{splitConfig.TargetFields.Count} שדות: {fieldsText}");
                            }

                            else if (operationName == "categorical_encoding")
                            {
                                OpenCategoricalEncodingWindow(columnName);

                                if (_columnSettings[columnName].CategoricalEncoding == null)
                                {
                                    checkBox.IsChecked = false;
                                    return;
                                }
                            }

                            _columnSettings[columnName].Operations.Add(operationName);
                        }
                        else
                        {
                            _columnSettings[columnName].Operations.Remove(operationName);

                            if (operationName == "replace_empty_values")
                            {
                                var settings = _columnSettings[columnName];
                                settings.ReplaceEmpty = null;
                            }

                            if (operationName == "replace_null_values")
                            {
                                var settings = _columnSettings[columnName];
                                settings.ReplaceNull = null;
                            }

                            if (operationName == "set_numeric_range")
                            {
                                var s = _columnSettings[columnName];
                                s.NumericRange = null;
                            }

                            if (operationName == "set_date_format")
                            {
                                var s = _columnSettings[columnName];
                                s.DateFormatApply = null;
                            }

            if (result == MessageBoxResult.Yes)
            {
                _notifications.Clear();
                RefreshNotificationsDisplay();

                AddSuccessNotification("הצלחה", "כל ההתראות נוקו");
            }
        }

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

                _columnSettings.Clear();
                FilePathTextBox!.Text = string.Empty;
                FileInfoTextBlock!.Text = "לא נבחר קובץ";

                NoFileMessageTextBlock.Visibility = Visibility.Visible;
                GlobalOperationsPanel.Visibility = Visibility.Collapsed;
                ColumnsScrollViewer.Visibility = Visibility.Collapsed;

                ResetCheckBoxesInPanel(this);

                _loadedConfig = null;
                _hasCompatibleConfig = false;
                _hasLastRunReport = false;

                if (result == MessageBoxResult.Yes)
                {
                    ResetUIToDefault();
                    AddInfoNotification("איפוס מלא", "כל ההגדרות וממשק המשתמש אופסו לברירת מחדל");
                }
                else
                {
                    AddInfoNotification("איפוס נתונים", "הגדרות הנתונים אופסו, הגדרות הממשק נשמרו");
                    SetPhase(UiPhase.Idle);
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

        private async void LoadConfig_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(FilePathTextBox?.Text) || !File.Exists(FilePathTextBox.Text))
                {
                    var ask = MessageBox.Show(
                        "קודם יש לבחור קובץ מקור לעיבוד. לפתוח דיאלוג בחירת קובץ עכשיו?",
                        "טעינת קובץ נדרשת",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information
                    );

                    if (ask != MessageBoxResult.Yes)
                    {
                        AddInfoNotification("פעולה בוטלה", "לא נטען קובץ מקור, לא ניתן לטעון קונפיגורציה.");
                        return;
                    }

                    var fileDlg = new Microsoft.Win32.OpenFileDialog
                    {
                        Filter = "CSV Files (*.csv)|*.csv|JSON Files (*.json)|*.json|Excel Files (*.xlsx;*.xls)|*.xlsx;*.xls|XML Files (*.xml)|*.xml|All Files (*.*)|*.*",
                        Title = "בחר קובץ נתונים"
                    };

                    if (fileDlg.ShowDialog() != true)
                    {
                        AddInfoNotification("פעולה בוטלה", "לא נבחר קובץ.");
                        return;
                    }
                    else if (!File.Exists(fileDlg.FileName))
                    {
                        AddInfoNotification("שגיאה", "הקובץ שנבחר אינו קיים.");
                        return;
                    }
                    else
                    {
                        FilePathTextBox!.Text = fileDlg.FileName;
                        await LoadFileColumns(fileDlg.FileName);
                        AddInfoNotification("נבחר קובץ", "כעת ניתן לטעון קונפיגורציה. ודא שהיא תואמת למבנה הקובץ.");
                    }

                    SetPhase(UiPhase.FileSelected);
                }
                else
                {
                    AddInfoNotification("תזכורת", "הקונפיגורציה חייבת להיות תואמת למבנה הקובץ שנטען.");
                }

                var cfgDlg = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "JSON Files (*.json)|*.json",
                    Title = "בחר קובץ קונפיגורציה"
                };
                if (cfgDlg.ShowDialog() != true) return;

                if (!TryReadConfigFromJson(cfgDlg.FileName, out var cfg, out var err))
                {
                    AddErrorNotification("שגיאה בטעינת קונפיג", "לא ניתן לטעון את הקובץ", err);
                    _hasCompatibleConfig = false;
                    SetPhase(UiPhase.ConfigLoadedMismatch);
                    return;
                }

                var filePath = FilePathTextBox!.Text;

                var validation = LocalValidateCompatibility(cfg!, filePath, _columnNames);

                if (!validation.IsCompatible)
                {
                    var dlg = new PipeWiseClient.Windows.CompatibilityReportWindow(validation)
                    {
                        Owner = this
                    };
                    dlg.ShowDialog();

                    AddErrorNotification("קונפיגורציה לא תואמת לקובץ",
                        "נמצאו פערים. ראה דוח תאימות ותקן לפני הרצה.");

                    _loadedConfig = cfg!;
                    _hasCompatibleConfig = false;
                    SetPhase(UiPhase.ConfigLoadedMismatch);
                    return;
                }

                _loadedConfig = cfg!;
                _hasCompatibleConfig = true;
                AddSuccessNotification("קונפיגורציה נטענה", $"נטען: {System.IO.Path.GetFileName(cfgDlg.FileName)}");
                await ApplyConfigToUI(_loadedConfig);
                SetPhase(UiPhase.ConfigLoadedCompatible);
            }
            catch (Exception ex)
            {
                AddErrorNotification("שגיאה בטעינת קונפיגורציה", "אירעה תקלה בתהליך", ex.Message);
                _hasCompatibleConfig = false;
                SetPhase(UiPhase.ConfigLoadedMismatch);
            }
        }

        private static CompatibilityIssue Issue(string msg)
        {
            var issue = new CompatibilityIssue();
            var t = typeof(CompatibilityIssue);
            var prop = t.GetProperty("Message", BindingFlags.Public | BindingFlags.Instance)
                      ?? t.GetProperty("Description", BindingFlags.Public | BindingFlags.Instance)
                      ?? t.GetProperty("Text", BindingFlags.Public | BindingFlags.Instance);
            if (prop != null && prop.CanWrite)
                prop.SetValue(issue, msg);
            return issue;
        }