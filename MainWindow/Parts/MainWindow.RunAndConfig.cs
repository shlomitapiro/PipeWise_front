using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PipeWiseClient.Models;

namespace PipeWiseClient
{
    public partial class MainWindow
    {
        private void ResetSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = MessageBox.Show(
                    "??? ?????? ???? ?? ?? ?????? ????? (???? ??????) ?????? ?????",
                    "????? ??????",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Cancel)
                    return;

                _columnSettings.Clear();
                FilePathTextBox!.Text = string.Empty;
                FileInfoTextBlock!.Text = "?? ???? ????";

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
                    AddInfoNotification("????? ???", "?? ??????? ????? ?????? ????? ?????? ????.");
                }
                else
                {
                    AddInfoNotification("????? ??????", "?????? ??????? ?????, ?????? ????? ?????");
                    SetPhase(UiPhase.Idle);
                }
            }
            catch (Exception ex)
            {
                AddErrorNotification("????? ??????", "?? ???? ???? ?? ???????", ex.Message);
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
                    AddWarningNotification("???? ????????????", "?? ???? ????? ??????????? - ???? ????? ????.");
                    return;
                }

                var json = JsonConvert.SerializeObject(config, Formatting.Indented);

                var saveDialog = new SaveFileDialog
                {
                    Filter = "JSON Files (*.json)|*.json",
                    Title = "???? ???????????",
                    FileName = "pipeline_config.json"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    File.WriteAllText(saveDialog.FileName, json, System.Text.Encoding.UTF8);
                    AddSuccessNotification(
                        "??????????? ?????",
                        "????? ???? ?????? ?????? ?????",
                        $"????: {saveDialog.FileName}\n????: {new FileInfo(saveDialog.FileName).Length} bytes"
                    );
                }
            }
            catch (Exception ex)
            {
                AddErrorNotification("????? ?????? ???????????", "?? ???? ????? ?? ????????????", ex.Message);
            }
        }

        private async void LoadConfig_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(FilePathTextBox?.Text) || !File.Exists(FilePathTextBox.Text))
                {
                    var ask = MessageBox.Show(
                        "???? ?? ????? ???? ???? ??????. ????? ?????? ????? ???? ??????",
                        "????? ???? ?????",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information
                    );

                    if (ask != MessageBoxResult.Yes)
                    {
                        AddInfoNotification("????? ?????", "?? ???? ???? ????, ?? ???? ????? ???????????.");
                        return;
                    }

                    var fileDlg = new Microsoft.Win32.OpenFileDialog
                    {
                        Filter = "CSV Files (*.csv)|*.csv|JSON Files (*.json)|*.json|Excel Files (*.xlsx;*.xls)|*.xlsx;*.xls|XML Files (*.xml)|*.xml|All Files (*.*)|*.*",
                        Title = "??? ???? ??????"
                    };

                    if (fileDlg.ShowDialog() != true)
                    {
                        AddInfoNotification("????? ?????", "?? ???? ????.");
                        return;
                    }
                    else if (!File.Exists(fileDlg.FileName))
                    {
                        AddInfoNotification("?????", "????? ????? ???? ????.");
                        return;
                    }
                    else
                    {
                        FilePathTextBox!.Text = fileDlg.FileName;
                        await LoadFileColumns(fileDlg.FileName);
                        AddInfoNotification("???? ????", "??? ???? ????? ???????????. ??? ???? ????? ????? ?????.");
                    }

                    SetPhase(UiPhase.FileSelected);
                }
                else
                {
                    AddInfoNotification("??????", "???????????? ????? ????? ????? ????? ????? ?????.");
                }

                var cfgDlg = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "JSON Files (*.json)|*.json",
                    Title = "??? ???? ???????????"
                };
                if (cfgDlg.ShowDialog() != true) return;

                if (!TryReadConfigFromJson(cfgDlg.FileName, out var cfg, out var err))
                {
                    AddErrorNotification("????? ?????? ??????", "?? ???? ????? ?? ?????", err);
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

                    AddErrorNotification("??????????? ?? ????? ?????",
                        "????? ?????. ??? ??? ?????? ???? ???? ????.");

                    _loadedConfig = cfg!;
                    _hasCompatibleConfig = false;
                    SetPhase(UiPhase.ConfigLoadedMismatch);
                    return;
                }

                _loadedConfig = cfg!;
                _hasCompatibleConfig = true;
                AddSuccessNotification("??????????? ?????", $"????: {System.IO.Path.GetFileName(cfgDlg.FileName)}");
                await ApplyConfigToUI(_loadedConfig);
                SetPhase(UiPhase.ConfigLoadedCompatible);
            }
            catch (Exception ex)
            {
                AddErrorNotification("????? ?????? ???????????", "????? ???? ??????", ex.Message);
                _hasCompatibleConfig = false;
                SetPhase(UiPhase.ConfigLoadedMismatch);
            }

            await Task.CompletedTask;
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
                    AddWarningNotification("???? ???? ?? ????",
                        "???? ?????? ??? ??????, ?? ????? ?-source.path ?? ????. ??? ???? ?????? ??? ??? ???????? ??? ???? ????????.");
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
                    AddErrorNotification("????? ??????", "??? ??????????? ?????");
                    return;
                }

                string baseName;
                var fp = FilePathTextBox != null ? FilePathTextBox.Text : null;
                if (!string.IsNullOrWhiteSpace(fp))
                    baseName = System.IO.Path.GetFileNameWithoutExtension(fp);
                else
                    baseName = $"Pipeline {System.DateTime.Now:yyyy-MM-dd HH:mm}";

                var dlg = new PipeWiseClient.Windows.PipelineNameDialog($"{baseName} - ????");
                dlg.Owner = this;
                var ok = dlg.ShowDialog() == true;
                if (!ok || string.IsNullOrWhiteSpace(dlg.PipelineName))
                    return;

                EnsureSafeTargetPath(cfg, fp ?? string.Empty);

                var resp = await _api.CreatePipelineAsync(cfg, name: dlg.PipelineName);

                AddSuccessNotification("Pipeline ???? ????",
                    $"'{dlg.PipelineName}' (ID: {resp?.id})",
                    resp?.message ?? "???? ??????. ???? ???? ??? ??? ????? ???????????.");
            }
            catch (Exception ex)
            {
                AddErrorNotification("????? ?????? ????", "?? ???? ????? ?? ?????????", ex.Message);
            }
        }

        private void CancelRun_Click(object sender, RoutedEventArgs e)
        {
            _runCts?.Cancel();
            AddInfoNotification("?????", "????? ??????.");
        }

        private async void RunSavedPipeline_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new PipeWiseClient.Windows.PipelinePickerWindow { Owner = this };
                var ok = picker.ShowDialog() == true && picker.SelectedPipeline != null;
                if (!ok)
                {
                    AddInfoNotification("????? ?????", "?? ???? ????????.");
                    return;
                }

                var p = picker.SelectedPipeline!;
                SetPhase(UiPhase.Running);
                UpdateSystemStatus($"???? '{p.name}'.", true);
                AddInfoNotification("????", $"???? ?? '{p.name}'");

                _runCts = new CancellationTokenSource();
                RunProgressBar.Value = 0; RunProgressText.Text = "0%";
                var progress = new Progress<(string Status, int Percent)>(pr =>
                {
                    RunProgressBar.Value = pr.Percent;
                    RunProgressText.Text = $"{pr.Percent}%";
                    SystemStatusText.Text = $"?? {pr.Status} ({pr.Percent}%)";
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
                    AddInfoNotification("????", "?????? ???? ?? ?????.");
                    UpdateSystemStatus("????? ?????", false);
                    return;
                }

                AddSuccessNotification("???? ??????", $"'{p.name}' ????? ??????", runResult?.message);
                UpdateSystemStatus("?????? ????? ????", true);

                if (!string.IsNullOrWhiteSpace(runResult?.TargetPath))
                {
                    try { System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{runResult.TargetPath}\""); }
                    catch (Exception ex) { AddErrorNotification("????? ????? ?????", runResult.TargetPath, ex.Message); }
                }

                _hasLastRunReport = true;
                SetPhase(UiPhase.Completed);
            }
            catch (Exception ex)
            {
                AddErrorNotification("????? ????? ????????", "?? ???? ????? ?? ????????? ?????", ex.Message);
                UpdateSystemStatus("????? ??????", false);

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
                    AddWarningNotification("???? ???", "?? ????? ???? ???? ???? ???? ????");
                    return;
                }

                var cfg = _loadedConfig ?? BuildPipelineConfig();
                if (cfg?.Source == null || cfg.Target == null)
                {
                    AddErrorNotification("????? ???????????", "?? ???? ????? ??????????? ?????");
                    SetPhase(_hasFile ? UiPhase.FileSelected : UiPhase.Idle);
                    return;
                }
                cfg.Source.Path = FilePathTextBox.Text;
                EnsureSafeTargetPath(cfg, FilePathTextBox.Text);

                SetPhase(UiPhase.Running);
                UpdateSystemStatus("???? ?????...", true);
                AddInfoNotification("????", "????? ?????? ?? ?????...");

                _runCts = new CancellationTokenSource();
                RunProgressBar.Value = 0; RunProgressText.Text = "0%";

                var progress = new Progress<(string Status, int Percent)>(pr =>
                {
                    RunProgressBar.Value = pr.Percent;
                    RunProgressText.Text = $"{pr.Percent}%";
                    SystemStatusText.Text = $"?? {pr.Status} ({pr.Percent}%)";
                });

                RunPipelineResult? result = null;
                try
                {
                    result = await _api.RunWithProgressAsync(cfg, progress, TimeSpan.FromMilliseconds(500), _runCts.Token);
                }
                catch (OperationCanceledException)
                {
                    AddInfoNotification("????", "?????? ???? ?? ?????.");
                    UpdateSystemStatus("????? ?????", false);
                    return;
                }

                if (result == null)
                {
                    AddErrorNotification("????? ??????", "?????? ????.");
                    UpdateSystemStatus("????? ??????", false);
                    SetPhase(UiPhase.Completed);
                    return;
                }

                if (!string.IsNullOrWhiteSpace(result.TargetPath))
                {
                    try
                    {
                        System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{result.TargetPath}\"");
                    }
                    catch (Exception ex)
                    {
                        AddWarningNotification("???? ????", $"????? ???? ?? ?? ?????? ????? ?? ??????.\n{result.TargetPath}\n\n{ex.Message}");
                    }
                }

                UpdateSystemStatus("?????? ????? ????", true);
                _hasLastRunReport = true;
                SetPhase(UiPhase.Completed);
            }
            catch (Exception ex)
            {
                AddErrorNotification("????? ????? Pipeline", ex.Message, ex.StackTrace);
                UpdateSystemStatus("????? ??????", false);
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
    }
}
