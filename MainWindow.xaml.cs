using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Newtonsoft.Json;
using PipeWiseClient.Models;
using OfficeOpenXml;

namespace PipeWiseClient
{
    public partial class MainWindow : Window
    {
        private readonly HttpClient _httpClient = new HttpClient();
        private List<string> _columnNames = new List<string>();
        private Dictionary<string, ColumnSettings> _columnSettings = new Dictionary<string, ColumnSettings>();
        private bool _isMySqlMode = false;

        public MainWindow()
        {
            try
            {
                InitializeComponent();
                
                // אתחול EPPlus
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
                
                // בדיקה שהUI elements קיימים
                CheckUIElements();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"שגיאה באתחול החלון: {ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CheckUIElements()
        {
            // בדוק אם יש לנו את ה-MySQL elements (אופציונלי)
            var sourceTypeCombo = FindName("SourceTypeCombo") as ComboBox;
            var mysqlPanel = FindName("MySqlPanel") as Border;
            
            if (sourceTypeCombo == null || mysqlPanel == null)
            {
                // אם אין MySQL UI, השבת את MySQL mode
                _isMySqlMode = false;
                System.Diagnostics.Debug.WriteLine("MySQL UI elements not found - MySQL mode disabled");
            }
        }

        private void BrowseFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new OpenFileDialog
                {
                    Filter = "CSV Files (*.csv)|*.csv|JSON Files (*.json)|*.json|Excel Files (*.xlsx;*.xls)|*.xlsx;*.xls|All Files (*.*)|*.*",
                    Title = "בחר קובץ נתונים"
                };

                if (dialog.ShowDialog() == true)
                {
                    FilePathTextBox.Text = dialog.FileName;
                    LoadFileColumns(dialog.FileName);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"שגיאה בבחירת קובץ: {ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadFileColumns(string filePath)
        {
            try
            {
                _columnNames.Clear();
                _columnSettings.Clear();

                string extension = Path.GetExtension(filePath).ToLower();

                if (extension == ".csv")
                {
                    LoadCsvColumns(filePath);
                }
                else if (extension == ".json")
                {
                    LoadJsonColumns(filePath);
                }
                else if (extension == ".xlsx" || extension == ".xls")
                {
                    LoadExcelColumns(filePath);
                }
                else
                {
                    MessageBox.Show("פורמט קובץ לא נתמך", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                FileInfoTextBlock.Text = $"נטען קובץ עם {_columnNames.Count} עמודות: {string.Join(", ", _columnNames)}";
                BuildColumnsUI();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"שגיאה בטעינת הקובץ: {ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadCsvColumns(string filePath)
        {
            using (var reader = new StreamReader(filePath, encoding: System.Text.Encoding.UTF8))
            {
                var firstLine = reader.ReadLine();
                if (!string.IsNullOrEmpty(firstLine))
                {
                    _columnNames = firstLine.Split(',').Select(c => c.Trim().Trim('"')).ToList();
                }
            }
        }

        private void LoadJsonColumns(string filePath)
        {
            var json = File.ReadAllText(filePath, System.Text.Encoding.UTF8);
            var data = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(json);

            if (data?.Count > 0)
            {
                _columnNames = data[0].Keys.ToList();
            }
        }

        private void LoadExcelColumns(string filePath)
        {
            try
            {
                using (var package = new ExcelPackage(new FileInfo(filePath)))
                {
                    var worksheet = package.Workbook.Worksheets.FirstOrDefault();
                    if (worksheet == null)
                        throw new Exception("לא נמצאו גיליונות בקובץ Excel");

                    // קריאת שורת הכותרת (שורה 1)
                    var headerRow = 1;
                    var endColumn = worksheet.Dimension?.End.Column ?? 0;
                    
                    _columnNames = new List<string>();
                    for (int col = 1; col <= endColumn; col++)
                    {
                        var header = worksheet.Cells[headerRow, col].Text?.Trim();
                        if (!string.IsNullOrEmpty(header))
                            _columnNames.Add(header);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"שגיאה בקריאת קובץ Excel: {ex.Message}");
            }
        }

        private void BuildColumnsUI()
        {
            try
            {
                ColumnsPanel.Children.Clear();
                NoFileMessageTextBlock.Visibility = Visibility.Collapsed;
                GlobalOperationsPanel.Visibility = Visibility.Visible;
                ColumnsPanel.Visibility = Visibility.Visible;

                foreach (string columnName in _columnNames)
                {
                    var columnPanel = CreateColumnPanel(columnName);
                    ColumnsPanel.Children.Add(columnPanel);
                    _columnSettings[columnName] = new ColumnSettings();
                }

                if (!_columnSettings.ContainsKey("global"))
                    _columnSettings["global"] = new ColumnSettings();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"שגיאה בבניית ממשק העמודות: {ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private Border CreateColumnPanel(string columnName)
        {
            var border = new Border
            {
                Background = System.Windows.Media.Brushes.White,
                BorderBrush = System.Windows.Media.Brushes.LightGray,
                BorderThickness = new Thickness(1),
                CornerRadius = new System.Windows.CornerRadius(5),
                Margin = new Thickness(0, 5, 0, 5),
                Padding = new Thickness(15)
            };

            var mainPanel = new StackPanel();

            var header = new TextBlock
            {
                Text = $"📊 {columnName}",
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                Foreground = System.Windows.Media.Brushes.DarkBlue,
                Margin = new Thickness(0, 0, 0, 10)
            };
            mainPanel.Children.Add(header);

            var cleaningGroup = CreateOperationGroup("🧹 ניקוי עמודה", new[]
            {
                ("הסר אם ערך חסר או null", "remove_if_missing"),
                ("הסר כפילויות לפי עמודה", "remove_duplicates_by_field"),
                ("הסר רווחים מיותרים", "strip_whitespace"),
                ("הסר אם שווה לערך", "remove_if_equals"),
                ("הסר ערכים לא תקינים", "remove_if_invalid"),
                ("החלף ערכי null", "replace_nulls"),
                ("מחק עמודה זו", "drop_columns")
            }, columnName, "cleaning");
            mainPanel.Children.Add(cleaningGroup);

            var transformGroup = CreateOperationGroup("🔄 טרנספורמציות", new[]
            {
                ("הפוך לאותיות גדולות", "to_uppercase"),
                ("הפוך לאותיות קטנות", "to_lowercase"),
                ("המר לטיפוס מספרי", "cast_type_int"),
                ("המר לטיפוס עשרוני", "cast_type_float"),
                ("החלף ערכים", "replace_values")
            }, columnName, "transform");
            mainPanel.Children.Add(transformGroup);

            border.Child = mainPanel;
            return border;
        }

        private GroupBox CreateOperationGroup(string title, (string, string)[] operations, string columnName, string category)
        {
            var groupBox = new GroupBox
            {
                Header = title,
                Margin = new Thickness(0, 5, 0, 5),
                FontWeight = FontWeights.SemiBold
            };

            var panel = new WrapPanel { Orientation = Orientation.Horizontal };

            foreach (var (displayName, operationKey) in operations)
            {
                var checkBox = new CheckBox
                {
                    Content = displayName,
                    Margin = new Thickness(0, 2, 15, 2),
                    Tag = $"{columnName}|{category}|{operationKey}"
                };

                checkBox.Checked += OperationCheckBox_Changed;
                checkBox.Unchecked += OperationCheckBox_Changed;
                panel.Children.Add(checkBox);
            }

            groupBox.Content = panel;
            return groupBox;
        }

        private void GlobalOperationCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is not CheckBox checkBox || checkBox.Tag?.ToString() is not string tag)
                    return;

                var tagParts = tag.Split('|');
                if (tagParts.Length < 3) return;

                var category = tagParts[1];
                var operation = tagParts[2];

                if (!_columnSettings.ContainsKey("global"))
                    _columnSettings["global"] = new ColumnSettings();

                var settings = _columnSettings["global"];

                if (checkBox.IsChecked == true)
                {
                    if (!settings.Operations.ContainsKey(category))
                        settings.Operations[category] = new List<string>();

                    if (!settings.Operations[category].Contains(operation))
                        settings.Operations[category].Add(operation);
                }
                else
                {
                    if (settings.Operations.ContainsKey(category))
                        settings.Operations[category].Remove(operation);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"שגיאה בעדכון פעולה גלובלית: {ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OperationCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is not CheckBox checkBox || checkBox.Tag?.ToString() is not string tag)
                    return;

                var tagParts = tag.Split('|');
                if (tagParts.Length < 3) return;

                var columnName = tagParts[0];
                var category = tagParts[1];
                var operation = tagParts[2];

                if (!_columnSettings.ContainsKey(columnName))
                    _columnSettings[columnName] = new ColumnSettings();

                var settings = _columnSettings[columnName];

                if (checkBox.IsChecked == true)
                {
                    string? userInput = null;

                    if (operation == "remove_if_equals")
                    {
                        userInput = Helpers.InputDialogs.ShowSingleValueDialog(
                            "הסר שורות עם ערך",
                            $"איזה ערך ברצונך להסיר מהעמודה '{columnName}'?",
                            "");

                        if (string.IsNullOrEmpty(userInput))
                        {
                            checkBox.IsChecked = false;
                            return;
                        }
                    }
                    else if (operation == "remove_if_invalid")
                    {
                        userInput = Helpers.InputDialogs.ShowMultiValueDialog(
                            "הסר ערכים לא תקינים",
                            $"אילו ערכים נחשבים לא תקינים בעמודה '{columnName}'?\n(לדוגמה: N/A, לא ידוע, שגוי)",
                            "N/A, לא ידוע, שגוי");

                        if (string.IsNullOrEmpty(userInput))
                        {
                            checkBox.IsChecked = false;
                            return;
                        }
                    }
                    else if (operation == "replace_nulls")
                    {
                        userInput = Helpers.InputDialogs.ShowSingleValueDialog(
                            "החלף ערכי null",
                            $"באיזה ערך להחליף ערכים ריקים/null בעמודה '{columnName}'?",
                            "לא זמין");

                        if (userInput == null)
                        {
                            checkBox.IsChecked = false;
                            return;
                        }
                    }
                    else if (operation == "replace_values")
                    {
                        var valueMapping = Helpers.InputDialogs.ShowValueMappingDialog("החלף ערכים", columnName);

                        if (valueMapping == null || valueMapping.Count == 0)
                        {
                            checkBox.IsChecked = false;
                            return;
                        }

                        userInput = System.Text.Json.JsonSerializer.Serialize(valueMapping);
                    }

                    if (!settings.Operations.ContainsKey(category))
                        settings.Operations[category] = new List<string>();

                    if (!settings.Operations[category].Contains(operation))
                    {
                        settings.Operations[category].Add(operation);

                        if (!settings.UserInputs.ContainsKey(operation))
                            settings.UserInputs[operation] = new Dictionary<string, object>();

                        if (userInput != null)
                        {
                            if (operation == "remove_if_invalid")
                            {
                                var values = userInput.Split(',').Select(v => v.Trim()).Where(v => !string.IsNullOrEmpty(v)).ToArray();
                                settings.UserInputs[operation]["values"] = values;
                            }
                            else if (operation == "replace_values")
                            {
                                settings.UserInputs[operation]["mapping_json"] = userInput;
                            }
                            else
                            {
                                settings.UserInputs[operation]["value"] = userInput;
                            }
                        }
                    }
                }
                else
                {
                    if (settings.Operations.ContainsKey(category))
                    {
                        settings.Operations[category].Remove(operation);
                        if (settings.UserInputs.ContainsKey(operation))
                            settings.UserInputs.Remove(operation);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"שגיאה בעדכון פעולה: {ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ResetSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _columnSettings.Clear();
                foreach (Border border in ColumnsPanel.Children)
                {
                    ResetCheckBoxesInPanel(border);
                }
                ResultTextBlock.Text = "ההגדרות אופסו";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"שגיאה באיפוס הגדרות: {ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
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
                var json = JsonConvert.SerializeObject(config, Formatting.Indented);

                var saveDialog = new SaveFileDialog
                {
                    Filter = "JSON Files (*.json)|*.json",
                    Title = "שמור קונפיגורציה"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    File.WriteAllText(saveDialog.FileName, json, System.Text.Encoding.UTF8);
                    ResultTextBlock.Text = "הקונפיגורציה נשמרה בהצלחה";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"שגיאה בשמירת הקונפיגורציה: {ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void RunPipeline_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!_isMySqlMode)
                {
                    if (string.IsNullOrWhiteSpace(FilePathTextBox.Text))
                    {
                        MessageBox.Show("נא לבחור קובץ מקור", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    if (!File.Exists(FilePathTextBox.Text))
                    {
                        MessageBox.Show("הקובץ הנבחר לא קיים", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                ResultTextBlock.Text = "מריץ Pipeline...";

                var config = BuildPipelineConfig();
                if (config?.Source == null)
                {
                    ResultTextBlock.Text = "❌ שגיאה: לא ניתן לבנות קונפיגורציה";
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
                    ResultTextBlock.Text = $"✅ Pipeline הושלם בהצלחה!\n\nתוצאה:\n{result}";
                }
                else
                {
                    ResultTextBlock.Text = $"❌ שגיאה מהשרת ({response.StatusCode}):\n{result}";
                }
            }
            catch (Exception ex)
            {
                ResultTextBlock.Text = $"❌ שגיאה בהרצת Pipeline: {ex.Message}";
                MessageBox.Show($"שגיאה מפורטת: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private PipelineConfig? BuildPipelineConfig()
        {
            try
            {
                var processors = new List<ProcessorConfig>();

                var globalOperations = new List<Dictionary<string, object>>();

                if (_columnSettings.ContainsKey("global"))
                {
                    var globalSettings = _columnSettings["global"];
                    foreach (var category in globalSettings.Operations)
                    {
                        foreach (var operation in category.Value)
                        {
                            globalOperations.Add(new Dictionary<string, object>
                            {
                                ["action"] = operation
                            });
                        }
                    }
                }

                var cleaningOps = new List<Dictionary<string, object>>();
                var transformOps = new List<Dictionary<string, object>>();

                foreach (var columnEntry in _columnSettings)
                {
                    var columnName = columnEntry.Key;
                    var settings = columnEntry.Value;

                    if (columnName == "global")
                        continue;

                    foreach (var category in settings.Operations)
                    {
                        foreach (var operation in category.Value)
                        {
                            var operationConfig = new Dictionary<string, object>
                            {
                                ["action"] = operation,
                                ["fields"] = new[] { columnName }
                            };

                            if (operation == "cast_type_int")
                            {
                                operationConfig["action"] = "cast_type";
                                operationConfig["field"] = columnName;
                                operationConfig["to_type"] = "int";
                            }
                            else if (operation == "cast_type_float")
                            {
                                operationConfig["action"] = "cast_type";
                                operationConfig["field"] = columnName;
                                operationConfig["to_type"] = "float";
                            }
                            else if (operation == "replace_nulls")
                            {
                                operationConfig["field"] = columnName;
                                if (settings.UserInputs.ContainsKey(operation) && settings.UserInputs[operation].ContainsKey("value"))
                                    operationConfig["value"] = settings.UserInputs[operation]["value"];
                                else
                                    operationConfig["value"] = "";
                            }
                            else if (operation == "remove_if_equals")
                            {
                                operationConfig["field"] = columnName;
                                if (settings.UserInputs.ContainsKey(operation) && settings.UserInputs[operation].ContainsKey("value"))
                                    operationConfig["value"] = settings.UserInputs[operation]["value"];
                                else
                                    operationConfig["value"] = "";
                            }
                            else if (operation == "remove_if_invalid")
                            {
                                operationConfig["field"] = columnName;
                                if (settings.UserInputs.ContainsKey(operation) && settings.UserInputs[operation].ContainsKey("values"))
                                    operationConfig["values"] = settings.UserInputs[operation]["values"];
                                else
                                    operationConfig["values"] = new[] { "N/A", "לא ידוע" };
                            }
                            else if (operation == "replace_values")
                            {
                                operationConfig["field"] = columnName;
                                if (settings.UserInputs.ContainsKey(operation) && settings.UserInputs[operation].ContainsKey("mapping_json"))
                                {
                                    var mappingJson = settings.UserInputs[operation]["mapping_json"]?.ToString();
                                    if (!string.IsNullOrEmpty(mappingJson))
                                    {
                                        var mapping = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(mappingJson);
                                        operationConfig["mapping"] = mapping ?? new Dictionary<string, string>();
                                    }
                                    else
                                    {
                                        operationConfig["mapping"] = new Dictionary<string, string>();
                                    }
                                }
                                else
                                {
                                    operationConfig["mapping"] = new Dictionary<string, string>();
                                }
                            }
                            else if (operation == "drop_columns")
                            {
                                operationConfig["fields"] = new[] { columnName };
                            }

                            if (IsTransformOperation(operation))
                                transformOps.Add(operationConfig);
                            else
                                cleaningOps.Add(operationConfig);
                        }
                    }
                }

                if (globalOperations.Count > 0)
                {
                    processors.Add(new ProcessorConfig
                    {
                        Type = "cleaner",
                        Config = new Dictionary<string, object>
                        {
                            ["operations"] = globalOperations
                        }
                    });
                }

                if (cleaningOps.Count > 0)
                {
                    processors.Add(new ProcessorConfig
                    {
                        Type = "cleaner",
                        Config = new Dictionary<string, object>
                        {
                            ["operations"] = cleaningOps
                        }
                    });
                }

                if (transformOps.Count > 0)
                {
                    processors.Add(new ProcessorConfig
                    {
                        Type = "transformer",
                        Config = new Dictionary<string, object>
                        {
                            ["operations"] = transformOps
                        }
                    });
                }

                if (processors.Count == 0)
                {
                    processors.Add(new ProcessorConfig
                    {
                        Type = "cleaner",
                        Config = new Dictionary<string, object>
                        {
                            ["operations"] = new List<Dictionary<string, object>>()
                        }
                    });
                }

                string fileExt = Path.GetExtension(FilePathTextBox.Text)?.TrimStart('.').ToLower() ?? "csv";

                var config = new PipelineConfig
                {
                    Source = new SourceConfig
                    {
                        Type = fileExt,
                        Path = FilePathTextBox.Text
                    },
                    Processors = processors.ToArray(),
                    Target = new TargetConfig
                    {
                        Type = "csv",
                        Path = Path.ChangeExtension(FilePathTextBox.Text, "_processed.csv") ?? "output.csv"
                    }
                };

                return config;
            }
            catch (Exception ex)
            {
                throw new Exception($"שגיאה בבניית קונפיגורציה: {ex.Message}", ex);
            }
        }

        private bool IsTransformOperation(string operation)
        {
            var transformOperations = new[]
            {
                "to_uppercase",
                "to_lowercase",
                "cast_type",
                "replace_values",
                "rename_field"
            };

            return transformOperations.Contains(operation);
        }

        private void SourceTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (sender is not ComboBox combo) return;

                _isMySqlMode = (combo.SelectedIndex == 1);

                var mysqlPanel = FindName("MySqlPanel") as Border;
                if (mysqlPanel != null)
                {
                    mysqlPanel.Visibility = _isMySqlMode ? Visibility.Visible : Visibility.Collapsed;
                }

                FilePathTextBox.IsEnabled = !_isMySqlMode;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"שגיאה בשינוי סוג מקור: {ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public class ColumnSettings
    {
        public Dictionary<string, List<string>> Operations { get; set; } = new Dictionary<string, List<string>>();
        public Dictionary<string, Dictionary<string, object>> UserInputs { get; set; } = new Dictionary<string, Dictionary<string, object>>();
    }
}