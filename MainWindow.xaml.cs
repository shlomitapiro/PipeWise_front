using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
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
        private List<NotificationItem> _notifications = new List<NotificationItem>();
        private bool _notificationsCollapsed = false;
        private const int MAX_NOTIFICATIONS = 50;
        private bool _isApplyingConfig = false;
        private CancellationTokenSource? _runCts;
        private UiPhase _phase = UiPhase.Idle;
        private bool _hasCompatibleConfig = false;
        private bool _hasLastRunReport = false;
        private bool _hasFile => !string.IsNullOrWhiteSpace(FilePathTextBox?.Text) && File.Exists(FilePathTextBox.Text);
        private const string SETTINGS_FILE = "ui_settings.json";
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
                    default:
                        AddWarningNotification("פורמט לא נתמך", "לא ניתן לטעון עמודות עבור פורמט קובץ זה");
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

                            if (operationName == "remove_invalid_dates")
                            {
                                var s = _columnSettings[columnName];
                                s.InvalidDateRemoval = null;
                            }

                            if (operationName == "remove_invalid_identifier")
                            {
                                var s = _columnSettings[columnName];
                                s.IdentifierValidation = null;
                            }

                            if (operationName == "normalize_numeric")
                            {
                                var settings = _columnSettings[columnName];
                                settings.NormalizeSettings = null;
                            }

                            if (operationName == "rename_field")
                            {
                                var settings = _columnSettings[columnName];
                                settings.RenameSettings = null;
                            }

                            if (operationName == "merge_columns")
                            {
                                var settings = _columnSettings[columnName];
                                settings.MergeColumnsSettings = null;
                            }

                            if (operationName == "split_field")
                            {
                                var settings = _columnSettings[columnName];
                                settings.SplitFieldSettings = null;
                            }

                            if (operationName == "categorical_encoding")
                            {
                                var settings = _columnSettings[columnName];
                                settings.CategoricalEncoding = null;
                            }
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
                FilePathTextBox!.Text = string.Empty;
                FileInfoTextBlock!.Text = "לא נבחר קובץ";

                // הסתרת ממשק העמודות
                NoFileMessageTextBlock.Visibility = Visibility.Visible;
                GlobalOperationsPanel.Visibility = Visibility.Collapsed;
                ColumnsScrollViewer.Visibility = Visibility.Collapsed;

                // איפוס כל ה-checkboxes
                ResetCheckBoxesInPanel(this);

                // איפוס סטייטים
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
                // 1) ודא שקודם נטען קובץ מקור
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

                // 2) בחירת קובץ קונפיגורציה
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

                // 3) בדיקת תאימות
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

                // 4) תאימות מלאה
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

            // כדי למנוע אזהרת CS1998 במתודה async ללא await
            await Task.CompletedTask;
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

        private PipeWiseClient.Models.CompatResult LocalValidateCompatibility(PipelineConfig cfg, string filePath, List<string> detectedColumns)
        {
            var result = new PipeWiseClient.Models.CompatResult();

            try
            {
                var requiredCols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var p in cfg.Processors ?? Array.Empty<ProcessorConfig>())
                {
                    if (!p.Config.TryGetValue("operations", out var opsObj) || opsObj == null)
                        continue;

                    if (opsObj is Newtonsoft.Json.Linq.JArray jarr)
                    {
                        foreach (var tok in jarr.OfType<Newtonsoft.Json.Linq.JObject>())
                        {
                            string? col = (string?)tok["column"] ?? (string?)tok["field"];
                            if (col == null && tok["fields"] is JArray farr && farr.First is JValue v && v.Type == JTokenType.String)
                                col = (string?)v;

                            if (!string.IsNullOrWhiteSpace(col))
                                requiredCols.Add(col);
                        }
                    }
                    else if (opsObj is System.Text.Json.Nodes.JsonArray sArr)
                    {
                        foreach (var node in sArr)
                        {
                            var col = node?["column"]?.GetValue<string>();
                            if (!string.IsNullOrWhiteSpace(col))
                                requiredCols.Add(col);
                        }
                    }
                    else if (opsObj is IEnumerable<object> plainList)
                    {
                        foreach (var item in plainList)
                        {
                            var dict = item as Dictionary<string, object>;
                            if (dict != null && dict.TryGetValue("column", out var cObj) && cObj is string c && !string.IsNullOrWhiteSpace(c))
                                requiredCols.Add(c);
                        }
                    }
                }

                var colsLower = new HashSet<string>(detectedColumns.Select(s => s.Trim()), StringComparer.OrdinalIgnoreCase);
                var missing = requiredCols.Where(rc => !colsLower.Contains(rc.Trim())).ToList();
                if (missing.Any())
                {
                    result.IsCompatible = false;
                    result.Issues.Add(Issue($"עמודות חסרות בקובץ: {string.Join(", ", missing)}"));
                }

                var numericOps = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "sum", "average", "min", "max", "median", "std", "variance", "range", "count_valid", "count_distinct", "most_common" };
                foreach (var p in cfg.Processors ?? Array.Empty<ProcessorConfig>())
                {
                    if (!p.Config.TryGetValue("operations", out var opsObj) || opsObj == null) continue;

                    IEnumerable<(string action, string? column)> EnumerateOps()
                    {
                        if (opsObj is Newtonsoft.Json.Linq.JArray jarr)
                        {
                            foreach (var tok in jarr.OfType<Newtonsoft.Json.Linq.JObject>())
                                yield return (((string?)tok["action"]) ?? "", (string?)tok["column"]);
                        }
                        else if (opsObj is System.Text.Json.Nodes.JsonArray sArr)
                        {
                            foreach (var node in sArr)
                                yield return ((node?["action"]?.GetValue<string>()) ?? "", node?["column"]?.GetValue<string>());
                        }
                        else if (opsObj is IEnumerable<object> plainList)
                        {
                            foreach (var item in plainList)
                            {
                                var dict = item as Dictionary<string, object>;
                                var action = dict != null && dict.TryGetValue("action", out var aObj) ? aObj?.ToString() ?? "" : "";
                                var col = dict != null && dict.TryGetValue("column", out var cObj) ? cObj as string : null;
                                yield return (action, col);
                            }
                        }
                    }

                    foreach (var (action, col) in EnumerateOps())
                    {
                        if (string.IsNullOrWhiteSpace(action) || string.IsNullOrWhiteSpace(col)) continue;

                        if (numericOps.Contains(action) && colsLower.Contains(col))
                        {
                            var hint = col!.ToLowerInvariant();
                            if (!(hint.Contains("price") || hint.Contains("qty") || hint.Contains("quantity") || hint.Contains("total") || hint.Contains("amount") || hint.Contains("count")))
                            {
                                result.Issues.Add(Issue($"בדיקה: הפעולה '{action}' על '{col}' נראית מספרית — ודא שהעמודה מספרית."));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                result.IsCompatible = false;
                result.Issues.Add(Issue("שגיאה בבדיקת התאימות: " + ex.Message));
            }

            return result;
        }

        private async Task ApplyConfigToUI(PipelineConfig cfg)
        {
            _isApplyingConfig = true;
            try
            {
                var sourcePath = cfg.Source?.Path;
                if (!string.IsNullOrWhiteSpace(sourcePath) && File.Exists(sourcePath))
                {
                    FilePathTextBox!.Text = sourcePath;
                    await LoadFileColumns(sourcePath);
                }

                if (!string.IsNullOrWhiteSpace(cfg.Target?.Type))
                    SelectTargetTypeInUi(cfg.Target.Type);

                var globalActions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var perColumnOps = new List<(string column, string action)>();

                foreach (var p in cfg.Processors ?? Array.Empty<ProcessorConfig>())
                {
                    if (!p.Config.TryGetValue("operations", out var opsObj) || opsObj == null) continue;

                    if (opsObj is JArray jarr)
                    {
                        foreach (var tok in jarr.OfType<JObject>())
                        {
                            var action = (string?)tok["action"];
                            // נסה column, ואם אין – field, ואם אין – הראשון מתוך fields
                            var column = (string?)tok["column"];
                            column ??= (string?)tok["field"];
                            if (column == null && tok["fields"] is JArray farr && farr.First is JValue v && v.Type == JTokenType.String)
                                column = (string?)v;
                            if (string.IsNullOrWhiteSpace(action)) continue;

                            if (string.IsNullOrWhiteSpace(column))
                                globalActions.Add(action);
                            else
                                perColumnOps.Add((column, action));
                        }
                    }
                }

                if (RemoveEmptyRowsCheckBox != null)
                    RemoveEmptyRowsCheckBox.IsChecked = globalActions.Contains("remove_empty_rows");
                if (RemoveDuplicatesCheckBox != null)
                    RemoveDuplicatesCheckBox.IsChecked = globalActions.Contains("remove_duplicates");
                if (StripWhitespaceCheckBox != null)
                    StripWhitespaceCheckBox.IsChecked = globalActions.Contains("strip_whitespace");

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
                    AddWarningNotification("קובץ מקור לא נטען",
                        "זוהו פעולות לפי עמודות, אך הקובץ ב-source.path לא נמצא. בחר קובץ נתונים זהה לזה שבקונפיג כדי לסמן אוטומטית.");
                }
            }
            finally
            {
                _isApplyingConfig = false;
            }
        }

        private void SelectTargetTypeInUi(string type)
        {
            if (TargetTypeComboBox is ComboBox cb)
            {
                foreach (var obj in cb.Items)
                {
                    if (obj is ComboBoxItem it && it.Tag is string tag &&
                        string.Equals(tag, type, StringComparison.OrdinalIgnoreCase))
                    {
                        cb.SelectedItem = it;
                        break;
                    }
                }
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
                var cfg = _loadedConfig ?? BuildPipelineConfig();
                if (cfg == null)
                {
                    AddErrorNotification("שגיאת קונפיג", "אין קונפיגורציה תקינה");
                    return;
                }

                string baseName;
                var fp = FilePathTextBox != null ? FilePathTextBox.Text : null;
                if (!string.IsNullOrWhiteSpace(fp))
                    baseName = System.IO.Path.GetFileNameWithoutExtension(fp);
                else
                    baseName = $"Pipeline {System.DateTime.Now:yyyy-MM-dd HH:mm}";

                var dlg = new PipeWiseClient.Windows.PipelineNameDialog($"{baseName} – שמור");
                dlg.Owner = this;
                var ok = dlg.ShowDialog() == true;
                if (!ok || string.IsNullOrWhiteSpace(dlg.PipelineName))
                    return;

                EnsureSafeTargetPath(cfg, fp ?? string.Empty);

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

        private void CancelRun_Click(object sender, RoutedEventArgs e)
        {
            _runCts?.Cancel();
            AddInfoNotification("ביטול", "הריצה מתבטלת…");
        }

        private async void RunSavedPipeline_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new PipeWiseClient.Windows.PipelinePickerWindow { Owner = this };
                var ok = picker.ShowDialog() == true && picker.SelectedPipeline != null;
                if (!ok)
                {
                    AddInfoNotification("בחירה בוטלה", "לא נבחר פייפליין.");
                    return;
                }

                var p = picker.SelectedPipeline!;
                SetPhase(UiPhase.Running);
                UpdateSystemStatus($"מריץ '{p.name}'…", true);
                AddInfoNotification("הרצה", $"מריץ את '{p.name}'");

                _runCts = new CancellationTokenSource();
                RunProgressBar.Value = 0; RunProgressText.Text = "0%";
                var progress = new Progress<(string Status, int Percent)>(pr =>
                {
                    RunProgressBar.Value = pr.Percent;
                    RunProgressText.Text = $"{pr.Percent}%";
                    SystemStatusText.Text = $"🟢 {pr.Status} ({pr.Percent}%)";
                });

                var full = await _api.GetPipelineAsync(p.id);
                if (full?.pipeline == null) throw new InvalidOperationException("Pipeline definition missing.");

                RunPipelineResult runResult;
                try
                {
                    runResult = await _api.RunWithProgressAsync(full.pipeline!, progress, TimeSpan.FromMilliseconds(500), _runCts.Token);
                }
                catch (OperationCanceledException)
                {
                    AddInfoNotification("בוטל", "המשתמש ביטל את הריצה.");
                    UpdateSystemStatus("הריצה בוטלה", false);
                    return;
                }

                AddSuccessNotification("הרצה הושלמה", $"'{p.name}' הופעל בהצלחה", runResult?.message);
                UpdateSystemStatus("המערכת פועלת תקין", true);

                if (!string.IsNullOrWhiteSpace(runResult?.TargetPath))
                {
                    try { System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{runResult.TargetPath}\""); }
                    catch (Exception ex) { AddErrorNotification("פתיחת תיקיה נכשלה", runResult.TargetPath, ex.Message); }
                }

                _hasLastRunReport = true;
                SetPhase(UiPhase.Completed);
            }
            catch (Exception ex)
            {
                AddErrorNotification("שגיאה בהרצת פייפליין", "לא ניתן להריץ את הפייפליין שנבחר", ex.Message);
                UpdateSystemStatus("שגיאה במערכת", false);

                // חזרה לסטייט הגיוני אחרי כישלון
                SetPhase(_hasCompatibleConfig ? UiPhase.ConfigLoadedCompatible :
                         _hasFile ? UiPhase.FileSelected : UiPhase.Idle);
            }
        }

        private async void RunPipeline_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(FilePathTextBox!.Text) || !File.Exists(FilePathTextBox.Text))
                {
                    AddWarningNotification("קובץ חסר", "יש לבחור קובץ מקור קיים לפני הרצה");
                    return;
                }

                // בונים קונפיג כרגיל
                var cfg = _loadedConfig ?? BuildPipelineConfig();
                if (cfg?.Source == null || cfg.Target == null)
                {
                    AddErrorNotification("שגיאת קונפיגורציה", "לא ניתן לבנות קונפיגורציה תקינה");
                    SetPhase(_hasFile ? UiPhase.FileSelected : UiPhase.Idle);
                    return;
                }
                cfg.Source.Path = FilePathTextBox.Text;
                EnsureSafeTargetPath(cfg, FilePathTextBox.Text);

                // UI → Running
                SetPhase(UiPhase.Running);
                UpdateSystemStatus("מעבד נתונים…", true);
                RunProgressBar.Value = 0;
                RunProgressText.Text = "0%";
                _runCts = new CancellationTokenSource();

                var progress = new Progress<(string Status, int Percent)>(p =>
                {
                    RunProgressBar.Value = p.Percent;
                    RunProgressText.Text = $"{p.Percent}%";
                    SystemStatusText.Text = $"🟢 {p.Status} ({p.Percent}%)";
                });

                RunPipelineResult result;

                try
                {
                    // ניסיון לרוץ במודל Jobs (Start→Progress→Result)
                    result = await _api.RunWithProgressAsync(cfg, progress, TimeSpan.FromMilliseconds(500), _runCts.Token);
                }
                catch (OperationCanceledException)
                {
                    AddInfoNotification("בוטל", "המשתמש ביטל את הריצה.");
                    UpdateSystemStatus("הריצה בוטלה", false);
                    return;
                }
                catch
                {
                    // נפילה חכמה ל-Ad-hoc (מעלה את הקובץ) אם השרת לא נגיש לקובץ בנתיב המקומי
                    AddInfoNotification("ניסיון חלופי", "מריץ במצב Ad-hoc (העלאת קובץ).");
                    result = await _api.RunAdHocPipelineAsync(
                        filePath: FilePathTextBox.Text,
                        config: cfg,
                        report: new RunReportSettings { generate_html = true, generate_pdf = true, auto_open_html = false },
                        ct: _runCts.Token
                    );
                }

                AddSuccessNotification("Pipeline הושלם!", result.message);

                //_hasLastRunReport = !string.IsNullOrWhiteSpace(htmlPath) || !string.IsNullOrWhiteSpace(pdfPath);

                SetPhase(UiPhase.Completed);

                if (!string.IsNullOrWhiteSpace(result.TargetPath))
                {
                    try
                    {
                        System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{result.TargetPath}\"");
                        AddInfoNotification("קובץ נוצר", $"הקובץ נוצר ב:\n{result.TargetPath}");
                    }
                    catch (Exception ex)
                    {
                        AddWarningNotification("קובץ נוצר", $"הקובץ נוצר אך לא הצלחתי לפתוח את התיקיה.\n{result.TargetPath}\n\n{ex.Message}");
                    }
                }

                UpdateSystemStatus("המערכת פועלת תקין", true);
                _hasLastRunReport = true;
                SetPhase(UiPhase.Completed);
            }
            catch (Exception ex)
            {
                AddErrorNotification("שגיאה בהרצת Pipeline", ex.Message, ex.StackTrace);
                UpdateSystemStatus("שגיאה במערכת", false);
                SetPhase(_hasCompatibleConfig ? UiPhase.ConfigLoadedCompatible : _hasFile ? UiPhase.FileSelected : UiPhase.Idle);
            }
            finally
            {
                _runCts?.Dispose();
                _runCts = null;
                if (RunProgressBar != null) RunProgressBar.Value = 0;
                if (RunProgressText != null) RunProgressText.Text = "0%";
            }
        }

        private PipelineConfig? BuildPipelineConfig()
        {
            try
            {
                if (string.IsNullOrEmpty(FilePathTextBox!.Text))
                    return null;

                var processors = new List<ProcessorConfig>();

                var globalOperations = new List<Dictionary<string, object>>();

                if (RemoveEmptyRowsCheckBox?.IsChecked == true)
                    globalOperations.Add(new Dictionary<string, object> { ["action"] = "remove_empty_rows" });

                // Include other global operations if selected
                if (RemoveDuplicatesCheckBox?.IsChecked == true)
                    globalOperations.Add(new Dictionary<string, object> { ["action"] = "remove_duplicates" });

                if (StripWhitespaceCheckBox?.IsChecked == true)
                    globalOperations.Add(new Dictionary<string, object> { ["action"] = "strip_whitespace" });

                var cleaningOps = new List<Dictionary<string, object>>();
                var transformOps = new List<Dictionary<string, object>>();
                var aggregationOps = new List<Dictionary<string, object>>();
                var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
                    "replace_empty_values","replace_null_values",
                    "remove_empty_values","remove_null_values",
                    "to_uppercase","to_lowercase","remove_special_characters",
                    "set_numeric_range","set_date_format","remove_invalid_dates","remove_invalid_identifier",
                    "validate_email_format","validate_positive_number","validate_not_empty",
                    "validate_types","validate_text_length","validate_date_format","validate_date",
                    "validate_numeric_range","required_fields"
                };

                foreach (var kvp in _columnSettings)
                {
                    var columnName = kvp.Key;
                    var settings = kvp.Value;

                    foreach (var operation in settings.Operations)
                    {
                        var opDict = new Dictionary<string, object>
                        {
                            ["action"] = operation
                        };

                        // Prefer backend-compatible keys for Cleaner operations
                        if (string.Equals(operation, "replace_empty_values", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(operation, "replace_null_values", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(operation, "set_numeric_range", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(operation, "set_date_format", StringComparison.OrdinalIgnoreCase))
                        {
                            opDict["field"] = columnName;
                        }

                        if (string.Equals(operation, "set_numeric_range", StringComparison.OrdinalIgnoreCase) &&
                            settings.NumericRange is not null)
                        {
                            // שליחת גבולות רק אם קיימים
                            if (settings.NumericRange.Min.HasValue)
                                opDict["min_value"] = settings.NumericRange.Min.Value;
                            if (settings.NumericRange.Max.HasValue)
                                opDict["max_value"] = settings.NumericRange.Max.Value;

                            // פעולה בחריגה
                            opDict["action_on_violation"] = string.IsNullOrWhiteSpace(settings.NumericRange.ActionOnViolation)
                                ? "remove" : settings.NumericRange.ActionOnViolation;

                            if (string.Equals(settings.NumericRange.ActionOnViolation, "replace", StringComparison.OrdinalIgnoreCase)
                                && settings.NumericRange.ReplacementValue.HasValue)
                            {
                                opDict["replacement_value"] = settings.NumericRange.ReplacementValue.Value;
                            }
                        }

                        if (string.Equals(operation, "set_date_format", StringComparison.OrdinalIgnoreCase))
                        {
                            var fmt = settings.DateFormatApply?.TargetFormat;

                            opDict["input_formats"] = new[]
                            {
                                // 4 ספרות שנה – Day-First בעדיפות
                                "%d-%m-%Y", "%d/%m/%Y", "%d.%m.%Y",
                                "%Y-%m-%d", "%Y/%m/%d", "%m/%d/%Y", "%m-%d-%Y",
                                "%d-%m-%Y %H:%M:%S", "%d/%m/%Y %H:%M:%S", "%d.%m.%Y %H:%M:%S",
                                "%Y-%m-%d %H:%M:%S", "%Y-%m-%dT%H:%M:%S", "%m/%d/%Y %H:%M:%S",
                                // 2 ספרות שנה – Day-First בעדיפות
                                "%d-%m-%y", "%d/%m/%y", "%d.%m.%y",
                                "%y-%m-%d", "%y/%m/%d", "%m/%d/%y",
                                "%d-%m-%y %H:%M:%S", "%d/%m/%y %H:%M:%S", "%y-%m-%d %H:%M:%S",
                            };

                            // העדפה מ-UI אם הוגדרה; אחרת – אם היעד CSV נכפה מחרוזת כדי לשמר את הפורמט
                            var targetType = GetSelectedTargetType();
                            if (!string.IsNullOrWhiteSpace(settings.DateFormatApply?.OutputAs))
                                opDict["output_as"] = settings.DateFormatApply.OutputAs!;
                            else if (string.Equals(targetType, "csv", StringComparison.OrdinalIgnoreCase))
                                opDict["output_as"] = "string";

                            if (!string.IsNullOrWhiteSpace(fmt))
                                opDict["target_format"] = fmt;
                            else
                                opDict["target_format"] = "%Y-%m-%d";

                            // מה לעשות כשלא מצליחים לפרסר תאריך
                            opDict["action_on_violation"] = "warn";
                        }

                        if (string.Equals(operation, "remove_invalid_dates", StringComparison.OrdinalIgnoreCase))
                        {
                            var s = settings.InvalidDateRemoval;

                            opDict["field"] = columnName;
                            opDict["action"] = "remove_invalid_dates";

                            // אותם input_formats כמו ב-set_date_format
                            opDict["input_formats"] = new[]
                            {
                                "%d-%m-%Y", "%d/%m/%Y", "%d.%m.%Y",
                                "%Y-%m-%d", "%Y/%m/%d", "%m/%d/%Y", "%m-%d-%Y",
                                "%d-%m-%Y %H:%M:%S", "%d/%m/%Y %H:%M:%S", "%d.%m.%Y %H:%M:%S",
                                "%Y-%m-%d %H:%M:%S", "%Y-%m-%dT%H:%M:%S", "%m/%d/%Y %H:%M:%S",
                                "%d-%m-%y", "%d/%m/%y", "%d.%m.%y",
                                "%y-%m-%d", "%y/%m/%d", "%m/%d/%y",
                                "%d-%m-%y %H:%M:%S", "%d/%m/%y %H:%M:%S", "%y-%m-%d %H:%M:%S",
                            };

                            if (s != null)
                            {
                                if (s.MinYear.HasValue) opDict["min_year"] = s.MinYear.Value;
                                if (s.MaxYear.HasValue) opDict["max_year"] = s.MaxYear.Value;
                                if (!string.IsNullOrWhiteSpace(s.MinDateIso)) opDict["min_date"] = s.MinDateIso;
                                if (!string.IsNullOrWhiteSpace(s.MaxDateIso)) opDict["max_date"] = s.MaxDateIso;

                                opDict["empty_action"] = string.IsNullOrWhiteSpace(s.EmptyAction) ? "remove" : s.EmptyAction;
                                if (string.Equals(s.EmptyAction, "replace", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(s.EmptyReplacement))
                                    opDict["empty_replacement"] = s.EmptyReplacement;
                            }

                            opDict["treat_whitespace_as_empty"] = true;

                            cleaningOps.Add(opDict);
                            continue;
                        }

                        if (string.Equals(operation, "replace_empty_values", StringComparison.OrdinalIgnoreCase) &&
                            settings.ReplaceEmpty is not null)
                        {
                            if (!string.IsNullOrWhiteSpace(settings.ReplaceEmpty.Value))
                                opDict["replacement_value"] = settings.ReplaceEmpty.Value!;

                            // נשלח תמיד לשרת (עם ברירות מחדל בצד השרת לתאימות)
                            opDict["expected_type"] = string.IsNullOrWhiteSpace(settings.InferredType) ? "string" : settings.InferredType.ToLowerInvariant();
                            opDict["max_length"] = settings.ReplaceEmpty.MaxLength <= 0 ? 255 : settings.ReplaceEmpty.MaxLength;
                        }

                        if (string.Equals(operation, "replace_null_values", StringComparison.OrdinalIgnoreCase) &&
                            settings.ReplaceNull is not null)
                        {
                            if (!string.IsNullOrWhiteSpace(settings.ReplaceNull.Value))
                                opDict["replacement_value"] = settings.ReplaceNull.Value!;

                            opDict["expected_type"] = string.IsNullOrWhiteSpace(settings.InferredType) ? "string" : settings.InferredType.ToLowerInvariant();
                            opDict["max_length"] = settings.ReplaceNull.MaxLength <= 0 ? 255 : settings.ReplaceNull.MaxLength;

                            opDict["null_definitions"] = new[] { "null", "n/a", "none" };
                        }

                        if (string.Equals(operation, "remove_invalid_identifier", StringComparison.OrdinalIgnoreCase))
                        {
                            var s = settings.IdentifierValidation;
                            if (s != null)
                            {
                                opDict["field"] = columnName;
                                opDict["action"] = "remove_invalid_identifier";
                                opDict["id_type"] = string.IsNullOrWhiteSpace(s.IdType) ? "numeric" : s.IdType;
                                opDict["treat_whitespace_as_empty"] = s.TreatWhitespaceAsEmpty;

                                opDict["empty_action"] = string.IsNullOrWhiteSpace(s.EmptyAction) ? "remove" : s.EmptyAction;
                                if (string.Equals(s.EmptyAction, "replace", StringComparison.OrdinalIgnoreCase)
                                    && !string.IsNullOrWhiteSpace(s.EmptyReplacement))
                                {
                                    opDict["empty_replacement"] = s.EmptyReplacement;
                                }

                                // תתי-אובייקטים לפי סוג
                                if (s.IdType == "numeric" && s.Numeric != null)
                                {
                                    opDict["numeric"] = new Dictionary<string, object?>
                                    {
                                        ["integer_only"] = s.Numeric.IntegerOnly,
                                        ["allow_leading_zeros"] = s.Numeric.AllowLeadingZeros,
                                        ["allow_negative"] = s.Numeric.AllowNegative,
                                        ["allow_thousand_separators"] = s.Numeric.AllowThousandSeparators,
                                        ["max_digits"] = s.Numeric.MaxDigits
                                    };
                                }
                                else if (s.IdType == "string" && s.String != null)
                                {
                                    opDict["string"] = new Dictionary<string, object?>
                                    {
                                        ["min_length"] = s.String.MinLength,
                                        ["max_length"] = s.String.MaxLength,
                                        ["disallow_whitespace"] = s.String.DisallowWhitespace,
                                        ["regex"] = string.IsNullOrWhiteSpace(s.String.Regex) ? null : s.String.Regex
                                    };
                                }
                                else if (s.IdType == "uuid" && s.Uuid != null)
                                {
                                    opDict["uuid"] = new Dictionary<string, object?>
                                    {
                                        ["accept_hyphenated"] = s.Uuid.AcceptHyphenated,
                                        ["accept_braced"] = s.Uuid.AcceptBraced,
                                        ["accept_urn"] = s.Uuid.AcceptUrn
                                    };
                                }

                                cleaningOps.Add(opDict);
                                continue; // המשך ללולאה הבאה
                            }
                        }

                        if (string.Equals(operation, "normalize_numeric", StringComparison.OrdinalIgnoreCase))
                        {
                            opDict["field"] = columnName;

                            if (settings.NormalizeSettings?.TargetField != null &&
                                !string.IsNullOrWhiteSpace(settings.NormalizeSettings.TargetField))
                            {
                                opDict["target_field"] = settings.NormalizeSettings.TargetField;
                            }
                        }

                        if (string.Equals(operation, "rename_field", StringComparison.OrdinalIgnoreCase))
                        {
                            var s = settings.RenameSettings;
                            if (s != null && !string.IsNullOrWhiteSpace(s.NewName))
                            {
                                opDict["field"] = columnName;
                                opDict["action"] = "rename_field";
                                opDict["old_name"] = columnName;
                                opDict["new_name"] = s.NewName;

                            }
                            else
                            {
                                continue;
                            }
                        }

                        if (string.Equals(operation, "merge_columns", StringComparison.OrdinalIgnoreCase))
                        {
                            var s = settings.MergeColumnsSettings;
                            if (s != null && s.SourceColumns.Count >= 2 && !string.IsNullOrWhiteSpace(s.TargetColumn))
                            {
                                opDict["action"] = "merge_columns";
                                opDict["source_columns"] = s.SourceColumns.ToArray();
                                opDict["target_column"] = s.TargetColumn;
                                opDict["separator"] = s.Separator;
                                opDict["remove_source"] = s.RemoveSourceColumns;
                                opDict["handle_empty"] = s.EmptyHandling;

                                if (!string.IsNullOrWhiteSpace(s.EmptyReplacement))
                                {
                                    opDict["empty_replacement"] = s.EmptyReplacement;
                                }
                            }
                            else
                            {
                                continue;
                            }
                        }

                        if (string.Equals(operation, "split_field", StringComparison.OrdinalIgnoreCase))
                        {
                            var s = settings.SplitFieldSettings;
                            if (s != null && s.TargetFields.Count > 0)
                            {
                                opDict["action"] = "split_field";
                                opDict["source_field"] = columnName;
                                opDict["split_type"] = s.SplitType;

                                if (s.SplitType == "delimiter")
                                {
                                    opDict["delimiter"] = s.Delimiter;
                                }
                                else if (s.SplitType == "fixed_length")
                                {
                                    opDict["length"] = s.Length;
                                }

                                opDict["target_fields"] = s.TargetFields.ToArray();
                                opDict["remove_source"] = s.RemoveSource;
                            }
                            else
                            {
                                continue; // דלג על הפעולה אם ההגדרות לא תקינות
                            }
                        }

                        if (string.Equals(operation, "categorical_encoding", StringComparison.OrdinalIgnoreCase))
                        {
                            var config = settings.CategoricalEncoding;
                            if (config != null && config.Mapping.Count > 0)
                            {
                                opDict["action"] = "categorical_encoding";
                                opDict["field"] = columnName;
                                opDict["mapping"] = config.Mapping;
                                opDict["replace_original"] = config.ReplaceOriginal;
                                opDict["delete_original"] = config.DeleteOriginal;
                                opDict["default_value"] = config.DefaultValue;

                                if (!config.ReplaceOriginal && !string.IsNullOrEmpty(config.TargetField))
                                {
                                    opDict["target_field"] = config.TargetField;
                                }

                                transformOps.Add(opDict);
                                continue; // המשך ללולאה הבאה
                            }
                            else
                            {
                                // אם אין קונפיגורציה תקינה, דלג על הפעולה
                                continue;
                            }
                        }

                        if (string.Equals(operation, "strip_whitespace", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(operation, "to_uppercase", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(operation, "to_lowercase", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(operation, "remove_special_characters", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(operation, "remove_empty_values", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(operation, "remove_null_values", StringComparison.OrdinalIgnoreCase))
                        {
                            opDict["fields"] = new[] { columnName };
                        }

                        else if (!opDict.ContainsKey("field"))
                        {
                            opDict["column"] = columnName;
                        }

                        if (operation.StartsWith("remove_") || operation.StartsWith("replace_")
                            || operation == "strip_whitespace"
                            || operation == "set_numeric_range"
                            || operation == "set_date_format"
                            || operation == "to_uppercase"
                            || operation == "to_lowercase")
                        {
                            cleaningOps.Add(opDict);
                        }
                        else if (operation == "cast_type" ||
                                operation == "normalize_numeric" || operation == "rename_field" ||
                                operation == "merge_columns" || operation == "split_field")
                        {
                            transformOps.Add(opDict);
                        }

                        else if (operation.StartsWith("validate_") || operation == "required_fields")
                        {

                            if (operation == "validate_date_format" && settings.DateValidationSettings != null)
                            {
                                opDict["action"] = "validate_date";
                                opDict["field"] = columnName;

                                opDict["input_formats"] = new[] {
                                    "%d-%m-%Y",
                                    "%d/%m/%Y",
                                    "%Y-%m-%d",
                                    "%Y/%m/%d",
                                    "%m/%d/%Y",
                                    "%m-%d-%Y"
                                };
                                opDict["target_format"] = settings.DateValidationSettings.DateFormat;

                                if (settings.DateValidationSettings.Action == "replace_with_date")
                                {
                                    opDict["on_fail"] = "replace";
                                    if (settings.DateValidationSettings.ReplacementDate.HasValue)
                                    {
                                        opDict["replace_with"] = settings.DateValidationSettings.ReplacementDate.Value.ToString(settings.DateValidationSettings.DateFormat);
                                    }
                                }
                                else
                                {
                                    opDict["on_fail"] = "drop_row";
                                }

                                opDict.Remove("column");
                            }
                        }
                        else if (operation == "sum" || operation == "average" ||
                                operation == "min" || operation == "max" || operation == "median"
                                || operation == "std" || operation == "variance" || operation == "range"
                                || operation == "count_valid" || operation == "count_distinct" || operation == "most_common")
                        {
                            aggregationOps.Add(opDict);
                        }
                    }
                }

                if (globalOperations.Count > 0 || cleaningOps.Count > 0)
                {
                    // סדר עדיפויות לפעולות ניקוי (קטנות ← קודם)
                    var priority = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["remove_invalid_dates"] = 10,
                        ["remove_empty_values"] = 20,
                        ["remove_null_values"] = 30,
                        ["strip_whitespace"] = 40,
                        ["replace_empty_values"] = 50,
                        ["replace_null_values"] = 60,
                        ["remove_invalid_identifier"] = 65,
                        ["set_numeric_range"] = 70,
                        ["remove_duplicates"] = 80,
                        ["set_date_format"] = 90,
                        // שאר הפעולות יקבלו 100
                    };

                    cleaningOps = cleaningOps
                        .OrderBy(op =>
                        {
                            var act = (op.TryGetValue("action", out var a) ? a?.ToString() : null) ?? "";
                            return priority.TryGetValue(act, out var p) ? p : 100;
                        })
                        .ToList();

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

                if (aggregationOps.Count > 0)
                {
                    processors.Add(new ProcessorConfig
                    {
                        Type = "aggregator",
                        Config = new Dictionary<string, object> { ["operations"] = aggregationOps }
                    });
                }

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

                // לפי בחירת המשתמש
                var selectedTargetType = GetSelectedTargetType();
                var targetExt = ExtForTarget(selectedTargetType);

                var outputFileName = $"{baseName}_processed.{targetExt}";
                try { Directory.CreateDirectory(OUTPUT_DIR); } catch { }
                var absoluteTargetPath = Path.Combine(OUTPUT_DIR, outputFileName);

                // הוסף בסוף המתודה BuildPipelineConfig, לפני ה-return:
                try
                {
                    var debugJson = JsonConvert.SerializeObject(new { processors }, Formatting.Indented);
                    AddInfoNotification("DEBUG - קונפיגורציה נשלחת", debugJson);
                }
                catch { }

                var built = new PipelineConfig
                {
                    Source = new SourceConfig
                    {
                        Type = sourceType,
                        Path = FilePathTextBox.Text
                    },
                    Processors = processors.ToArray(),
                    Target = new TargetConfig
                    {
                        Type = selectedTargetType,
                        Path = absoluteTargetPath
                    }
                };

                return built;

            }
            catch (Exception ex)
            {
                AddErrorNotification("שגיאה בבניית קונפיגורציה", ex.Message);
                return null;
            }
        }

        private void EnsureSafeTargetPath(PipelineConfig cfg, string dataFilePath)
        {
            Directory.CreateDirectory(OUTPUT_DIR);

            var baseName = string.IsNullOrWhiteSpace(dataFilePath)
                ? "output"
                : Path.GetFileNameWithoutExtension(dataFilePath);

            // לפי בחירת המשתמש
            var selectedTargetType = GetSelectedTargetType();
            var targetExt = ExtForTarget(selectedTargetType);

            var defaultType = selectedTargetType;
            var defaultPath = Path.Combine(OUTPUT_DIR, $"{baseName}_processed.{targetExt}");

            if (cfg.Target == null)
            {
                cfg.Target = new TargetConfig
                {
                    Type = defaultType,
                    Path = defaultPath
                };
                return;
            }

            if (string.IsNullOrWhiteSpace(cfg.Target.Type))
                cfg.Target.Type = defaultType;

            if (string.IsNullOrWhiteSpace(cfg.Target.Path))
                cfg.Target.Path = defaultPath;

            var effectiveType = string.IsNullOrWhiteSpace(cfg.Target.Type) ? selectedTargetType : cfg.Target.Type;
            var desiredExt = "." + ExtForTarget(effectiveType.ToLowerInvariant());

            if (!string.IsNullOrWhiteSpace(cfg.Target.Path))
            {
                var currentExt = Path.GetExtension(cfg.Target.Path);
                if (!string.Equals(currentExt, desiredExt, StringComparison.OrdinalIgnoreCase))
                    cfg.Target.Path = Path.ChangeExtension(cfg.Target.Path, desiredExt);
            }
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
        
    }
}
