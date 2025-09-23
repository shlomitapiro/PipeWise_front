

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
                
                if (jsonData is Newtonsoft.Json.Linq.JArray jsonArray && jsonArray.Count > 0)
                {
                    if (jsonArray[0] is Newtonsoft.Json.Linq.JObject firstObj)
                    {
                        _columnNames = firstObj.Properties().Select(p => p.Name).ToList();
                    }
                }
                else if (jsonData is Newtonsoft.Json.Linq.JObject jsonObj)
                {
                    var firstArray = jsonObj.Properties()
                        .Select(p => p.Value)
                        .OfType<Newtonsoft.Json.Linq.JArray>()
                        .FirstOrDefault();
                        
                    if (firstArray?.Count > 0 && firstArray[0] is Newtonsoft.Json.Linq.JObject firstRecord)
                    {
                        _columnNames = firstRecord.Properties().Select(p => p.Name).ToList();
                    }
                    else
                    {
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
                var doc = System.Xml.Linq.XDocument.Load(filePath);
                
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
            return "csv";
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
                ("הסר מזהה לא חוקי", "remove_invalid_identifier"),
                ("החלף ערכים ריקים", "replace_empty_values"),
                ("החלף ערכי NULL", "replace_null_values"),
                ("הסר ערכים ריקים", "remove_empty_values"),
                ("הסר ערכי NULL", "remove_null_values"),
                ("הפוך לאותיות גדולות", "to_uppercase"),
                ("הפוך לאותיות קטנות", "to_lowercase"),
                ("הסר תווים מיוחדים", "remove_special_characters"),
                ("אמת טווח מספרי", "set_numeric_range"),
                ("קבע פורמט תאריך", "set_date_format"),
                ("הסר תאריך לא חוקי", "remove_invalid_dates"),
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

        private async void OperationCheckBox_Changed(object sender, RoutedEventArgs e)
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
                                    checkBox.IsChecked = false;
                                    return;
                                }
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
                                    if (string.IsNullOrWhiteSpace(inferred)) return true;
                                    var t = inferred.ToLowerInvariant();
                                    return t.Contains("date") || t.Contains("time") || t.Contains("timestamp")
                                        || t.Contains("string") || t.Contains("text") || t.Contains("mixed");
                                }

                                var t = _columnSettings[columnName].InferredType;
                                var looksLikeDate = IsTypeSupportedForDateFormat(t);
                                var dlg = new Windows.DateFormatDialog(columnName, looksLikeDate) { Owner = this };

                                var ok = dlg.ShowDialog() == true;

                                if (!ok || string.IsNullOrWhiteSpace(dlg.SelectedPythonFormat))
                                {
                                    checkBox.IsChecked = false;
                                    return;
                                }

                                var s = _columnSettings[columnName];
                                s.DateFormatApply ??= new DateFormatApplySettings();
                                s.DateFormatApply.TargetFormat = dlg.SelectedPythonFormat!;
                            }
                            else if (operationName == "categorical_encoding")
                            {
                                await OpenCategoricalEncodingWindow(columnName);

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

                            if (operationName == "set_date_format")
                            {
                                var s = _columnSettings[columnName];
                                s.DateFormatApply = null;
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