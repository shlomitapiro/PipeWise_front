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
using System.Net;
using System.Net.Http;

using PipeWiseClient.Helpers;
using PipeWiseClient.Models;
using PipeWiseClient.Services;
using PipeWiseClient.Windows;
using Newtonsoft.Json.Linq;

namespace PipeWiseClient
{
    public partial class MainWindow : Window
    {
        private readonly PipeWiseClient.Interfaces.IApiClient _api = null!;
        private readonly PipeWiseClient.Interfaces.INotificationService _notifications = null!;
        private readonly PipeWiseClient.Interfaces.IFileService _fileService = null!;
        private readonly PipeWiseClient.Interfaces.IPipelineService _pipelineService = null!;
        private readonly PipeWiseClient.Services.ColumnOperationRegistry _operationRegistry = null!;
        private List<string> _columnNames = new List<string>();
        public IReadOnlyList<string> ColumnNames => _columnNames.AsReadOnly();
        private Dictionary<string, ColumnSettings> _columnSettings = new Dictionary<string, ColumnSettings>();
        private PipelineConfig? _loadedConfig;
        private const string OUTPUT_DIR = @"C:\Users\shlom\PipeWise\output";
        private bool _notificationsCollapsed = false;
        private bool _isApplyingConfig = false;
        
        private readonly System.Collections.Generic.List<System.Threading.CancellationTokenSource> _activeRuns = new();
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

        private void OnNotificationAdded(object? sender, NotificationItem notification)
        {
            Dispatcher.Invoke(() => RefreshNotificationsDisplay());
        }

        public MainWindow(PipeWiseClient.Interfaces.IApiClient apiClient, PipeWiseClient.Interfaces.INotificationService notificationService, PipeWiseClient.Interfaces.IFileService fileService, PipeWiseClient.Interfaces.IPipelineService pipelineService, PipeWiseClient.Services.ColumnOperationRegistry operationRegistry)
        {
            try
            {
                _api = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
                _notifications = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
                _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
                _pipelineService = pipelineService ?? throw new ArgumentNullException(nameof(pipelineService));
                _operationRegistry = operationRegistry ?? throw new ArgumentNullException(nameof(operationRegistry));
                InitializeComponent();

                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

                LoadUISettings();

                _notifications.NotificationAdded += OnNotificationAdded;
                _notifications.Info("ברוך הבא ל-PipeWise", "המערכת מוכנה לעיבוד נתונים");

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
            if (_notifications.Notifications.Count == 0)
            {
                _notifications.Info("מידע", "אין התראות למחיקה");
                return;
            }

            var result = MessageBox.Show(
                $"האם אתה בטוח שברצונך למחוק {_notifications.Notifications.Count} התראות?",
                "מחיקת התראות",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _notifications.Clear();
                RefreshNotificationsDisplay();
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
            }
            else
            {
                NotificationsScrollViewer.Visibility = Visibility.Visible;
                CollapseNotificationsBtn.Content = "📦";
            }

            SaveUISettings();
        }

        private void ViewReports_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var reportsWindow = new ReportsWindow();
                reportsWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                _notifications.Error("שגיאת חלון דוחות", "שגיאה בפתיחת חלון הדוחות", ex.Message);
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
                _pendingOperationsToApply.Clear();

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
                    _notifications.Info("איפוס מלא", "כל ההגדרות וממשק המשתמש אופסו לברירת מחדל");
                }
                else
                {
                    _notifications.Info("איפוס נתונים", "הגדרות הנתונים אופסו, הגדרות הממשק נשמרו");
                    SetPhase(UiPhase.Idle);
                }
            }
            catch (Exception ex)
            {
                _notifications.Error("שגיאה באיפוס", "לא ניתן לאפס את ההגדרות", ex.Message);
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

        private async void SaveConfig_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var config = _pipelineService.BuildConfig(FilePathTextBox!.Text, _columnSettings, GetSelectedTargetType(), OUTPUT_DIR, RemoveEmptyRowsCheckBox?.IsChecked == true, RemoveDuplicatesCheckBox?.IsChecked == true, StripWhitespaceCheckBox?.IsChecked == true, _columnNames);
                if (!_pipelineService.ValidateConfig(config, out var errors))
                {
                    _notifications.Error("שגיאה", string.Join("\n", errors));
                    return;
                }

                var saveDialog = new SaveFileDialog
                {
                    Filter = "JSON Files (*.json)|*.json",
                    Title = "שמור קונפיגורציה",
                    FileName = "pipeline_config.json"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    await _pipelineService.SaveConfigAsync(config, saveDialog.FileName);
                    _notifications.Success(
                        "קונפיגורציה נשמרה",
                        "הקובץ נשמר בהצלחה למיקום הנבחר",
                        $"נתיב: {saveDialog.FileName}\nגודל: {new FileInfo(saveDialog.FileName).Length} bytes"
                    );
                }
            }
            catch (Exception ex)
            {
                _notifications.Error("שגיאה בשמירת קונפיגורציה", "לא ניתן לשמור את הקונפיגורציה", ex.Message);
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
                        _notifications.Info("פעולה בוטלה", "לא נטען קובץ מקור, לא ניתן לטעון קונפיגורציה.");
                        return;
                    }

                    var fileDlg = new Microsoft.Win32.OpenFileDialog
                    {
                        Filter = "CSV Files (*.csv)|*.csv|JSON Files (*.json)|*.json|Excel Files (*.xlsx;*.xls)|*.xlsx;*.xls|XML Files (*.xml)|*.xml|All Files (*.*)|*.*",
                        Title = "בחר קובץ נתונים"
                    };

                    if (fileDlg.ShowDialog() != true)
                    {
                        _notifications.Info("פעולה בוטלה", "לא נבחר קובץ.");
                        return;
                    }
                    else if (!File.Exists(fileDlg.FileName))
                    {
                        _notifications.Info("שגיאה", "הקובץ שנבחר אינו קיים.");
                        return;
                    }
                    else
                    {
                        FilePathTextBox!.Text = fileDlg.FileName;
                        await LoadFileColumns(fileDlg.FileName);
                        _notifications.Info("נבחר קובץ", "כעת ניתן לטעון קונפיגורציה. ודא שהיא תואמת למבנה הקובץ.");
                    }

                    SetPhase(UiPhase.FileSelected);
                }
                else
                {
                    _notifications.Info("תזכורת", "הקונפיגורציה חייבת להיות תואמת למבנה הקובץ שנטען.");
                }

                var cfgDlg = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "JSON Files (*.json)|*.json",
                    Title = "בחר קובץ קונפיגורציה"
                };
                
                if (cfgDlg.ShowDialog() != true) 
                {
                    return;
                }
                
                var cfg = await _pipelineService.LoadConfigAsync(cfgDlg.FileName);
                if (cfg == null)
                {
                    _notifications.Error("שגיאה בטעינת קונפיג", "לא ניתן לקרוא את הקובץ");
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

                    _notifications.Error("קונפיגורציה לא תואמת לקובץ",
                        "נמצאו פערים. ראה דוח תאימות ותקן לפני הרצה.");

                    _loadedConfig = cfg!;
                    _hasCompatibleConfig = false;
                    SetPhase(UiPhase.ConfigLoadedMismatch);
                    return;
                }

                _loadedConfig = cfg!;
                NormalizeProcessorConfigs(_loadedConfig);
                _hasCompatibleConfig = true;
                
                _notifications.Success("קונפיגורציה נטענה", $"נטען: {System.IO.Path.GetFileName(cfgDlg.FileName)}");
                
                DebugConfigContent(_loadedConfig);
                
                await ApplyConfigToUI(_loadedConfig);
                
                SetPhase(UiPhase.ConfigLoadedCompatible);                
            }
            catch (Exception ex)
            {
                _notifications.Error("שגיאה בטעינת קונפיגורציה", "אירעה תקלה בתהליך", ex.Message);
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

        private CheckBox? FindCheckBoxByTag(DependencyObject? root, string tag)
        {
            if (root is null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);

                if (child is CheckBox cb && cb.Tag is string s &&
                    string.Equals(s, tag, StringComparison.OrdinalIgnoreCase))
                {
                    return cb;
                }

                var inner = FindCheckBoxByTag(child, tag);
                if (inner != null) return inner;
            }

            return null;
        }

        private async void SaveAsServerPipeline_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var cfg = _loadedConfig ?? _pipelineService.BuildConfig(FilePathTextBox!.Text, _columnSettings, GetSelectedTargetType(), OUTPUT_DIR, RemoveEmptyRowsCheckBox?.IsChecked == true, RemoveDuplicatesCheckBox?.IsChecked == true, StripWhitespaceCheckBox?.IsChecked == true, _columnNames);
                if (cfg == null)
                {
                    _notifications.Error("שגיאת קונפיג", "אין קונפיגורציה תקינה");
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
                NormalizeProcessorConfigs(cfg);

                var resp = await _api.CreatePipelineAsync(cfg, name: dlg.PipelineName);

                _notifications.Success("Pipeline נשמר בשרת",
                    $"'{dlg.PipelineName}' (ID: {resp?.id})",
                    resp?.message ?? "נשמר בהצלחה. ניתן לחפש לפי השם בעמוד הפייפליינים.");
            }
            catch (Exception ex)
            {
                _notifications.Error("שגיאה בשמירה לשרת", "לא ניתן לשמור את הפייפליין", ex.Message);
            }
        }

        private void CancelRun_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                foreach (var cts in _activeRuns.ToArray())
                {
                    try { cts.Cancel(); } catch { }
                }
                _notifications.Info("ביטול", "כל הריצות הפעילות בוטלו.");
            }
            catch (Exception ex)
            {
                _notifications.Error("שגיאה בביטול", ex.Message);
            }
        }

        private async void RunSavedPipeline_Click(object sender, RoutedEventArgs e)
        {
            System.Threading.CancellationTokenSource? cts = null;
            try
            {
                var picker = new PipeWiseClient.Windows.PipelinePickerWindow { Owner = this };
                var ok = picker.ShowDialog() == true && picker.SelectedPipeline != null;
                if (!ok)
                {
                    _notifications.Info("בחירה בוטלה", "לא נבחר פייפליין.");
                    return;
                }

                var p = picker.SelectedPipeline!;
                BeginRunUi("הרצה..."); cts = new System.Threading.CancellationTokenSource(); _activeRuns.Add(cts);
                var baseProgress = CreateRunProgress();
                var runStarted = false;
                var progress = new Progress<(string Status, int Percent)>(p =>
                {
                    runStarted = true;
                    baseProgress.Report(p);
                });

                RunPipelineResult runResult;
                try
                {
                    var full = await _api.GetPipelineAsync(p.id);
                    if (full?.pipeline == null) throw new InvalidOperationException("Pipeline definition missing.");

                    runResult = await _pipelineService.ExecuteAsync(full.pipeline!, progress, cts.Token) ?? throw new InvalidOperationException("Run failed: no result returned from server");
                }
                catch (OperationCanceledException)
                {
                    _notifications.Info("בוטל", "המשתמש ביטל את הריצה.");
                    EndRunUiError("הריצה בוטלה");
                    return;
                }
                catch (HttpRequestException httpEx) when (httpEx.StatusCode == HttpStatusCode.NotFound || httpEx.StatusCode == HttpStatusCode.MethodNotAllowed)
                {
                    _notifications.Info("ניסיון חלופי", "Jobs API לא החזיר run_id – מריץ במצב Ad-hoc (ללא מעקב התקדמות).");
                    var full = await _api.GetPipelineAsync(p.id);
                    if (full?.pipeline == null) throw new InvalidOperationException("Pipeline definition missing.");
                    var adHocPath = full.pipeline.Source?.Path ?? FilePathTextBox?.Text;
                    if (string.IsNullOrWhiteSpace(adHocPath))
                        throw new InvalidOperationException("No source file path available for Ad-hoc run.");

                    runResult = await _pipelineService.ExecuteAsync(full.pipeline!, progress, cts.Token);
                }
                catch (Exception ex)
                {
                    if (runStarted)
                    {
                        _notifications.Error("שגיאת ריצה", "נכשלה ריצה עם מעקב; לא מריץ Ad-hoc כדי למנוע כפילויות.", ex.Message);
                        EndRunUiError("שגיאה במערכת");
                        return;
                    }
                    _notifications.Error("שגיאת ריצה", "השרת החזיר שגיאה; לא מבצע Fallback אוטומטי.", ex.Message);
                    EndRunUiError("שגיאה במערכת");
                    return;
                }

                _notifications.Success("הרצה הושלמה", $"'{p.name}' הופעל בהצלחה", runResult?.message);
                EndRunUiSuccess("המערכת פועלת תקין");

                if (!string.IsNullOrWhiteSpace(runResult?.TargetPath))
                {
                    try { System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{runResult.TargetPath}\""); }
                    catch (Exception ex) { _notifications.Error("פתיחת תיקיה נכשלה", runResult.TargetPath, ex.Message); }
                }
            }
            catch (Exception ex)
            {
                _notifications.Error("שגיאה בהרצת פייפליין", "לא ניתן להריץ את הפייפליין שנבחר", ex.Message);
                EndRunUiError("שגיאה במערכת");
            }
            finally
            {
                
                try { if (cts != null) { _activeRuns.Remove(cts); cts.Dispose(); } } catch { }
                if (RunProgressBar != null) RunProgressBar.Value = 0;
                if (RunProgressText != null) RunProgressText.Text = "0%";
            }
        }

        private async void RunPipeline_Click(object sender, RoutedEventArgs e)
        {
            System.Threading.CancellationTokenSource? cts = null;
            try
            {
                if (string.IsNullOrWhiteSpace(FilePathTextBox!.Text) || !File.Exists(FilePathTextBox.Text))
                {
                    _notifications.Warning("קובץ חסר", "יש לבחור קובץ מקור קיים לפני הרצה");
                    return;
                }

                var cfg = _loadedConfig ?? _pipelineService.BuildConfig(FilePathTextBox!.Text, _columnSettings, GetSelectedTargetType(), OUTPUT_DIR, RemoveEmptyRowsCheckBox?.IsChecked == true, RemoveDuplicatesCheckBox?.IsChecked == true, StripWhitespaceCheckBox?.IsChecked == true, _columnNames);
                if (cfg?.Source == null || cfg.Target == null)
                {
                    _notifications.Error("שגיאת קונפיגורציה", "לא ניתן לבנות קונפיגורציה תקינה");
                    SetPhase(_hasFile ? UiPhase.FileSelected : UiPhase.Idle);
                    return;
                }
                cfg.Source.Path = FilePathTextBox.Text;
                EnsureSafeTargetPath(cfg, FilePathTextBox.Text);
                NormalizeProcessorConfigs(cfg);

                BeginRunUi("הרצה..."); cts = new System.Threading.CancellationTokenSource(); _activeRuns.Add(cts);
                var baseProgress = CreateRunProgress();
                var runStarted = false;
                var progress = new Progress<(string Status, int Percent)>(p =>
                {
                    runStarted = true;
                    baseProgress.Report(p);
                });

                RunPipelineResult result;

                try
                {
                    result = await _pipelineService.ExecuteAsync(cfg, progress, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    _notifications.Info("בוטל", "המשתמש ביטל את הריצה.");
                    EndRunUiError("הריצה בוטלה");
                    return;
                }
                /* catch (RunAlreadyStartedException ex)
                {
                    // ריצה כבר החלה – לא לבצע fallback נוסף
                    _notifications.Error("שגיאת ריצה", $"הריצה כבר הושקה (RunId={ex.RunId}); לא מריץ Ad-hoc כפול.", ex.InnerException?.Message ?? "");
                    EndRunUiError("שגיאה במערכת");
                    return;
                } */
                catch (HttpRequestException httpEx) when (httpEx.StatusCode == HttpStatusCode.NotFound || httpEx.StatusCode == HttpStatusCode.MethodNotAllowed)
                {
                    _notifications.Info("ניסיון חלופי", "Jobs API לא החזיר run_id – מריץ במצב Ad-hoc (העלאת קובץ).");
                    result = await _pipelineService.ExecuteAsync(cfg, progress, cts.Token);
                }
                catch (Exception ex)
                {
                    if (runStarted)
                    {
                        _notifications.Error("שגיאת ריצה", "נכשלה ריצה עם מעקב; לא מריץ Ad-hoc כדי למנוע כפילויות.", ex.Message);
                        EndRunUiError("שגיאה במערכת");
                        return;
                    }
                    // בשגיאות אחרות שאינן 404/405 – אל תריץ Ad-hoc (מונע כפילויות)
                    _notifications.Error("שגיאת ריצה", "השרת החזיר שגיאה; לא מבצע Fallback אוטומטי.", ex.Message);
                    EndRunUiError("שגיאה במערכת");
                    return;
                }

                _notifications.Success("Pipeline הושלם!", result.message);

                if (!string.IsNullOrWhiteSpace(result.TargetPath))
                {
                    try
                    {
                        System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{result.TargetPath}\"");
                        _notifications.Info("קובץ נוצר", $"הקובץ נוצר ב:\n{result.TargetPath}");
                    }
                    catch (Exception ex)
                    {
                        _notifications.Warning("קובץ נוצר", $"הקובץ נוצר אך לא הצלחתי לפתוח את התיקיה.\n{result.TargetPath}\n\n{ex.Message}");
                    }
                }
                EndRunUiSuccess("המערכת פועלת תקין");

            }
            catch (Exception ex)
            {
                _notifications.Error("שגיאה בהרצת Pipeline", ex.Message, ex.StackTrace);
                EndRunUiError("שגיאה במערכת");
            }
            finally
            {
                
                try { if (cts != null) { _activeRuns.Remove(cts); cts.Dispose(); } } catch { }
                if (RunProgressBar != null) RunProgressBar.Value = 0;
                if (RunProgressText != null) RunProgressText.Text = "0%";
            }
        }
        
        private static object NormalizeJToken(object? o)
        {
            switch (o)
            {
                case null:
                    return null!;
                case Newtonsoft.Json.Linq.JValue v:
                    return v.Value!;
                case Newtonsoft.Json.Linq.JObject jo:
                    return jo.Properties()
                            .ToDictionary(p => p.Name, p => NormalizeJToken(p.Value));
                case Newtonsoft.Json.Linq.JArray ja:
                    return ja.Select(NormalizeJToken).ToList();
                default:
                    return o;
            }
        }

        private static object? NormalizeAnyJsonNode(object? o)
        {
            if (o is Newtonsoft.Json.Linq.JValue jv) return jv.Value;
            if (o is Newtonsoft.Json.Linq.JObject jo)
                return jo.Properties().ToDictionary(p => p.Name, p => NormalizeAnyJsonNode(p.Value));
            if (o is Newtonsoft.Json.Linq.JArray ja)
                return ja.Select(NormalizeAnyJsonNode).ToList();

            if (o is System.Text.Json.Nodes.JsonValue jsv)
            {
                if (jsv.TryGetValue(out string? s)) return s;
                if (jsv.TryGetValue(out int i)) return i;
                if (jsv.TryGetValue(out double d)) return d;
                if (jsv.TryGetValue(out bool b)) return b;
                return jsv.ToJsonString(); // fallback
            }
            if (o is System.Text.Json.Nodes.JsonArray sa)
                return sa.Select(NormalizeAnyJsonNode).ToList();
            if (o is System.Text.Json.Nodes.JsonObject so)
                return so.ToDictionary(kv => kv.Key, kv => NormalizeAnyJsonNode(kv.Value));

            if (o is IEnumerable<object> e) return e.Select(NormalizeAnyJsonNode).ToList();
            if (o is Dictionary<string, object?> dct)
                return dct.ToDictionary(k => k.Key, v => NormalizeAnyJsonNode(v.Value));

            return o;
        }

        private static void NormalizeProcessorConfigs(PipelineConfig cfg)
        {
            if (cfg?.Processors == null) return;

            foreach (var p in cfg.Processors)
            {
                if (p?.Config == null) continue;

                var normalized = NormalizeAnyJsonNode(p.Config);

                Dictionary<string, object?> dict;
                if (normalized is Dictionary<string, object?> cfgDict)
                {
                    dict = new Dictionary<string, object?>(cfgDict, StringComparer.OrdinalIgnoreCase);
                }
                else if (normalized is Dictionary<string, object> cfgDictObj)
                {
                    dict = cfgDictObj.ToDictionary(kv => kv.Key, kv => (object?)kv.Value, StringComparer.OrdinalIgnoreCase);
                }
                else
                {
                    continue;
                }

                if (dict.TryGetValue("operations", out var opsValue))
                {
                    if (opsValue is Dictionary<string, object> opsDict && opsDict.ContainsKey("ValueKind"))
                    {
                        var processorType = p.Type?.ToLowerInvariant() ?? "";
                        if (processorType == "cleaner")
                        {
                            dict["operations"] = new List<Dictionary<string, object>>
                            {
                                new() { ["action"] = "remove_empty_rows" },
                                new() { ["action"] = "strip_whitespace" }
                            };
                        }
                        else
                        {
                            dict["operations"] = new List<Dictionary<string, object>>();
                        }                       
                    }
                    else if (opsValue is List<object> opsList)
                    {
                        var cleanedOps = new List<Dictionary<string, object>>();

                        foreach (var op in opsList)
                        {
                            if (op is Dictionary<string, object> opDict)
                            {
                                if (!opDict.ContainsKey("ValueKind"))
                                {
                                    cleanedOps.Add(opDict);
                                }
                            }
                            else if (op is string actionString)
                            {
                                cleanedOps.Add(new Dictionary<string, object> { ["action"] = actionString });
                            }
                        }

                        dict["operations"] = cleanedOps;
                    }
                }

                if (dict.ContainsKey("Operations") && !dict.ContainsKey("operations"))
                {
                    dict["operations"] = dict["Operations"];
                    dict.Remove("Operations");
                }

                if (!dict.TryGetValue("operations", out var opsObj) || opsObj == null)
                    continue;

                if (opsObj is not IEnumerable<object?> list)
                    continue;

                var fixedList = new List<Dictionary<string, object?>>();

                foreach (var it in list)
                {
                    var item = NormalizeAnyJsonNode(it) as Dictionary<string, object?>;
                    if (item == null) continue;
                    if (item.ContainsKey("ValueKind")) continue;

                    var nd = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    foreach (var kv in item)
                        nd[(kv.Key ?? string.Empty).ToLowerInvariant()] = kv.Value;

                    if (!nd.ContainsKey("field"))
                    {
                        if (nd.TryGetValue("column", out var col) && col is string cs && !string.IsNullOrWhiteSpace(cs))
                            nd["field"] = cs;
                        else if (nd.TryGetValue("source_field", out var sf) && sf is string sfs && !string.IsNullOrWhiteSpace(sfs))
                            nd["field"] = sfs;
                    }

                    if (!nd.TryGetValue("action", out var act) || string.IsNullOrWhiteSpace(act?.ToString()))
                        continue;

                    if (nd.TryGetValue("fields", out var fields) && fields is IEnumerable<object?> ef)
                        nd["fields"] = ef.Select(x => x?.ToString()).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();

                    fixedList.Add(nd);
                }

                dict["operations"] = fixedList;
                p.Config = dict.ToDictionary(kv => kv.Key, kv => (object)kv.Value!, StringComparer.OrdinalIgnoreCase);

            }
        }
    }
}


