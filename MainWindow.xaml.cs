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
                BeginRunUi($"מריץ '{p.name}'…");
                _runCts = new CancellationTokenSource();
                var progress = CreateRunProgress();

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
                    EndRunUiError("הריצה בוטלה");
                    return;
                }

                AddSuccessNotification("הרצה הושלמה", $"'{p.name}' הופעל בהצלחה", runResult?.message);
                EndRunUiSuccess("המערכת פועלת תקין");

                if (!string.IsNullOrWhiteSpace(runResult?.TargetPath))
                {
                    try { System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{runResult.TargetPath}\""); }
                    catch (Exception ex) { AddErrorNotification("פתיחת תיקיה נכשלה", runResult.TargetPath, ex.Message); }
                }
            }
            catch (Exception ex)
            {
                AddErrorNotification("שגיאה בהרצת פייפליין", "לא ניתן להריץ את הפייפליין שנבחר", ex.Message);
                EndRunUiError("שגיאה במערכת");
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

                var cfg = _loadedConfig ?? BuildPipelineConfig();
                if (cfg?.Source == null || cfg.Target == null)
                {
                    AddErrorNotification("שגיאת קונפיגורציה", "לא ניתן לבנות קונפיגורציה תקינה");
                    SetPhase(_hasFile ? UiPhase.FileSelected : UiPhase.Idle);
                    return;
                }
                cfg.Source.Path = FilePathTextBox.Text;
                EnsureSafeTargetPath(cfg, FilePathTextBox.Text);

                BeginRunUi("מעבד נתונים…");
                _runCts = new CancellationTokenSource();
                var progress = CreateRunProgress();

                RunPipelineResult result;

                try
                {
                    result = await _api.RunWithProgressAsync(cfg, progress, TimeSpan.FromMilliseconds(500), _runCts.Token);
                }
                catch (OperationCanceledException)
                {
                    AddInfoNotification("בוטל", "המשתמש ביטל את הריצה.");
                    EndRunUiError("הריצה בוטלה");
                    return;
                }
                catch
                {
                    AddInfoNotification("ניסיון חלופי", "מריץ במצב Ad-hoc (העלאת קובץ).");
                    result = await _api.RunAdHocPipelineAsync(
                        filePath: FilePathTextBox.Text,
                        config: cfg,
                        report: new RunReportSettings { generate_html = true, generate_pdf = true, auto_open_html = false },
                        ct: _runCts.Token
                    );
                }

                AddSuccessNotification("Pipeline הושלם!", result.message);

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
                EndRunUiSuccess("המערכת פועלת תקין");

            }
            catch (Exception ex)
            {
                AddErrorNotification("שגיאה בהרצת Pipeline", ex.Message, ex.StackTrace);
                EndRunUiError("שגיאה במערכת");
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

                if (RemoveDuplicatesCheckBox?.IsChecked == true)
                    globalOperations.Add(new Dictionary<string, object> { ["action"] = "remove_duplicates" });

                if (StripWhitespaceCheckBox?.IsChecked == true)
                    globalOperations.Add(new Dictionary<string, object> { ["action"] = "strip_whitespace" });

                var cleaningOps = new List<Dictionary<string, object>>();
                var transformOps = new List<Dictionary<string, object>>();
                var aggregationOps = new List<Dictionary<string, object>>();

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
                            if (settings.NumericRange.Min.HasValue)
                                opDict["min_value"] = settings.NumericRange.Min.Value;
                            if (settings.NumericRange.Max.HasValue)
                                opDict["max_value"] = settings.NumericRange.Max.Value;

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

                            opDict["input_formats"] = DATE_INPUT_FORMATS;

                            var targetType = GetSelectedTargetType();
                            if (!string.IsNullOrWhiteSpace(settings.DateFormatApply?.OutputAs))
                                opDict["output_as"] = settings.DateFormatApply.OutputAs!;
                            else if (string.Equals(targetType, "csv", StringComparison.OrdinalIgnoreCase))
                                opDict["output_as"] = "string";

                            if (!string.IsNullOrWhiteSpace(fmt))
                                opDict["target_format"] = fmt;
                            else
                                opDict["target_format"] = "%Y-%m-%d";

                            opDict["action_on_violation"] = "warn";
                        }

                        if (string.Equals(operation, "remove_invalid_dates", StringComparison.OrdinalIgnoreCase))
                        {
                            var s = settings.InvalidDateRemoval;

                            opDict["field"] = columnName;
                            opDict["action"] = "remove_invalid_dates";
                            opDict["input_formats"] = DATE_INPUT_FORMATS;

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
                                continue;
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
                                continue;
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
                                continue;
                            }
                            else
                            {
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

                                opDict["input_formats"] = DATE_INPUT_FORMATS;
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

                // DEBUG בלבד – אל תציג למשתמשים ב-Release
#if DEBUG
                try
                {
                    var debugJson = JsonConvert.SerializeObject(new { processors }, Formatting.Indented);
                    AddInfoNotification("DEBUG - קונפיגורציה נשלחת", debugJson);
                }
                catch { }
#endif

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
    }
}