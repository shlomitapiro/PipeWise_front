using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using Newtonsoft.Json;
using OfficeOpenXml;
using PipeWiseClient.Models;
using PipeWiseClient.Windows;

#if false
namespace PipeWiseClient
{
    public partial class MainWindow
    {
        private async void BrowseFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new OpenFileDialog
                {
                    Filter = "CSV Files (*.csv)|*.csv|JSON Files (*.json)|*.json|Excel Files (*.xlsx;*.xls)|*.xlsx;*.xls|XML Files (*.xml)|*.xml|All Files (*.*)|*.*",
                    Title = "??? ???? ??????"
                };

                if (dialog.ShowDialog() == true)
                {
                    FilePathTextBox!.Text = dialog.FileName;
                    var fileInfo = new FileInfo(dialog.FileName);

                    FileInfoTextBlock!.Text = $"???? ????: {Path.GetFileName(dialog.FileName)} | ????: {fileInfo.Length:N0} bytes";

                    AddSuccessNotification(
                        "???? ????",
                        $"????: {Path.GetFileName(dialog.FileName)}",
                        $"????: {fileInfo.Length:N0} bytes\n????: {dialog.FileName}"
                    );

                    // ????? ???? ??? ?????? ?????? ???? ???? (?? ???)
                    _loadedConfig = null;
                    _hasCompatibleConfig = false;
                    _hasLastRunReport = false;

                    await LoadFileColumns(dialog.FileName);

                    SetPhase(UiPhase.FileSelected);
                }
            }
            catch (Exception ex)
            {
                AddErrorNotification("????? ?????? ????", "?? ???? ????? ?? ?????", ex.Message);
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
                        AddWarningNotification("????? ?? ????", "?? ???? ????? ?????? ???? ????? ???? ??");
                        return;
                }

                if (_columnNames.Count > 0)
                {
                    await DetectColumnTypes(filePath);
                    ShowColumnsInterface();
                    AddInfoNotification("?????? ?????", $"????? {_columnNames.Count} ?????? ??????");
                }
                else
                {
                    AddWarningNotification("??? ??????", "?? ????? ?????? ?????");
                }
            }
            catch (Exception ex)
            {
                AddErrorNotification("????? ?????? ??????", "?? ???? ????? ?? ?????? ?????", ex.Message);
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
            return "csv"; // ????? ???? ????? ???????? ??????
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

            // Panel header
            var headerPanel = new Grid();
            headerPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var headerText = new TextBlock
            {
                Text = $"?? {columnName}",
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

            var cleaningGroup = CreateOperationGroup("?? ?????", new[]
            {
                ("??? ???? ?? ????",   "remove_invalid_identifier"),
                ("???? ????? ?????", "replace_empty_values"),
                ("???? ???? NULL",  "replace_null_values"),
                ("??? ????? ?????", "remove_empty_values"),
                ("??? ???? NULL",   "remove_null_values"),
                ("???? ??????? ??????", "to_uppercase"),
                ("???? ??????? ?????",  "to_lowercase"),
                ("??? ????? ???????",   "remove_special_characters"),
                ("??? ???? ?????",      "set_numeric_range"),
                ("??? ????? ?????",     "set_date_format"),
                ("??? ????? ?? ????",   "remove_invalid_dates"),

            }, columnName);
            operationsPanel.Children.Add(cleaningGroup);

            var transformGroup = CreateOperationGroup("?? ???????????", new[]
            {
                ("??? ?? ?????", "rename_field"),
                ("??? ??????", "merge_columns"),
                ("??? ???", "split_field"),
                ("??? ?????", "cast_type"),
                ("???? ????? ??????? (0-1)", "normalize_numeric"),
                ("????? ?????????", "categorical_encoding")
            }, columnName);
            operationsPanel.Children.Add(transformGroup);

            var aggregationGroup = CreateOperationGroup("?? ???????", new[]
            {
                ("????", "sum"),
                ("?????", "average"),
                ("???????", "min"),
                ("???????", "max"),
                ("?????", "median"),
                ("????? ???", "std"),
                ("?????", "variance"),
                ("????", "range"),
                ("????? ????? ??????", "count_valid"),
                ("????? ???????", "count_distinct"),
                ("??? ??? ????", "most_common"),
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
                                    checkBox.IsChecked = false; // ?????? ????
                                    return;
                                }
                                // ???? ?? ?????? ??????
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
                                    if (string.IsNullOrWhiteSpace(inferred)) return true; // fail-open ??? ????? ?????? ????
                                    var t = inferred.ToLowerInvariant();
                                    return t.Contains("date") || t.Contains("time") || t.Contains("timestamp")
                                        || t.Contains("string") || t.Contains("text") || t.Contains("mixed");
                                }

                                var t = _columnSettings[columnName].InferredType;
                                var looksLikeDate = IsTypeSupportedForDateFormat(t);
                                var dlg = new Windows.DateFormatDialog(columnName, looksLikeDate) { Owner = this };

                                // ?????? ???? ?? ???????
                                var ok = dlg.ShowDialog() == true;

                                // ?? ?????? ???? ?? ?? ???? ????? - ?????? ?? ??????
                                if (!ok || string.IsNullOrWhiteSpace(dlg.SelectedPythonFormat))
                                {
                                    checkBox.IsChecked = false;
                                    return;
                                }

                                // ????? ?????? ?????
                                var s = _columnSettings[columnName];
                                s.DateFormatApply ??= new DateFormatApplySettings();
                                s.DateFormatApply.TargetFormat = dlg.SelectedPythonFormat!;
                            }

                            else if (operationName == "remove_invalid_dates")
                            {
                                var dlg = new RemoveInvalidDatesDialog(columnName) { Owner = this };
                                var ok = dlg.ShowDialog() == true;
                                if (!ok) { checkBox.IsChecked = false; return; }

                                var s = _columnSettings[columnName];
                                s.InvalidDateRemoval ??= new InvalidDateRemovalSettings();
                                s.InvalidDateRemoval.MinYear = dlg.MinYear;
                                s.InvalidDateRemoval.MaxYear = dlg.MaxYear;
                                s.InvalidDateRemoval.EmptyAction = dlg.EmptyAction;
                                s.InvalidDateRemoval.EmptyReplacement = dlg.EmptyReplacement;
                                s.InvalidDateRemoval.MinDateIso = dlg.MinDateISO;
                                s.InvalidDateRemoval.MaxDateIso = dlg.MaxDateISO;
                            }

                            else if (operationName == "set_numeric_range")
                            {
                                var dlg = new NumericRangeDialog(columnName) { Owner = this };
                                var ok = dlg.ShowDialog() == true;
                                if (!ok) { checkBox.IsChecked = false; return; }

                                var s = _columnSettings[columnName];
                                s.NumericRange ??= new NumericRangeSettings();
                                s.NumericRange.Min = dlg.Min;
                                s.NumericRange.Max = dlg.Max;
                                s.NumericRange.ActionOnViolation = dlg.ActionOnViolation;
                                s.NumericRange.ReplacementValue = dlg.ReplacementValue;
                            }

                            else if (operationName == "remove_invalid_identifier")
                            {
                                var dlg = new RemoveInvalidIdentifierDialog(columnName) { Owner = this };
                                var ok = dlg.ShowDialog() == true;
                                if (!ok) { checkBox.IsChecked = false; return; }

                                var s = _columnSettings[columnName];
                                s.IdentifierValidation ??= new IdentifierValidationSettings();

                                s.IdentifierValidation.IdType = dlg.IdType;
                                s.IdentifierValidation.TreatWhitespaceAsEmpty = dlg.TreatWhitespaceAsEmpty;
                                s.IdentifierValidation.EmptyAction = dlg.EmptyAction;
                                s.IdentifierValidation.EmptyReplacement = dlg.EmptyReplacement;

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
                                        AcceptHyphenated = dlg.UuidAcceptHyphenated,
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

                                AddInfoNotification("????? ?? ?????",
                                    $"?????? '{columnName}' ????? ?-'{dialog.NewName}' ?????? ???????");
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

                                var settings = _columnSettings[columnName];
                                settings.MergeColumnsSettings ??= new MergeColumnsSettings();

                                var allColumnsToMerge = new List<string> { columnName };
                                allColumnsToMerge.AddRange(dialog.SelectedColumns);

                                settings.MergeColumnsSettings.SourceColumns = allColumnsToMerge;
                                settings.MergeColumnsSettings.TargetColumn = dialog.TargetColumn;
                                settings.MergeColumnsSettings.Separator = dialog.Separator;
                                settings.MergeColumnsSettings.RemoveSourceColumns = dialog.RemoveSourceColumns;
                                settings.MergeColumnsSettings.EmptyHandling = dialog.EmptyHandling;
                                settings.MergeColumnsSettings.EmptyReplacement = dialog.EmptyReplacement;

                                var allColumnsText = string.Join(", ", allColumnsToMerge);
                                AddInfoNotification("????? ??????",
                                    $"??????? [{allColumnsText}] ?????? ?????? '{dialog.TargetColumn}' ?? ????? '{dialog.Separator}'");
                            }

                            else if (operationName == "split_field")
                            {
                                var allColumns = _columnNames.ToList();
                                var dialog = new SplitFieldWindow(allColumns)
                                {
                                    Owner = this
                                };

                                var result = dialog.ShowDialog();

                                if (result != true)
                                {
                                    checkBox.IsChecked = false;
                                    return;
                                }

                                var splitConfig = dialog.Result;
                                var settings = _columnSettings[columnName];
                                settings.SplitFieldSettings ??= new SplitFieldSettings();

                                settings.SplitFieldSettings.SplitType = splitConfig.SplitType;
                                settings.SplitFieldSettings.Delimiter = splitConfig.Delimiter;
                                settings.SplitFieldSettings.Length = splitConfig.Length;
                                settings.SplitFieldSettings.TargetFields = splitConfig.TargetFields;
                                settings.SplitFieldSettings.RemoveSource = splitConfig.RemoveSource;

                                var fieldsText = string.Join(", ", splitConfig.TargetFields);
                                AddInfoNotification("????? ???",
                                    $"???? '{columnName}' ????? ?-{splitConfig.TargetFields.Count} ????: {fieldsText}");
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
    }
}
#endif
