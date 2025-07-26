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

namespace PipeWiseClient
{
    public partial class MainWindow : Window
    {
        private readonly HttpClient _httpClient = new HttpClient();
        private List<string> _columnNames = new List<string>();
        private Dictionary<string, ColumnSettings> _columnSettings = new Dictionary<string, ColumnSettings>();

        public MainWindow()
        {
            InitializeComponent();
        }

        private void BrowseFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "CSV Files (*.csv)|*.csv|JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                Title = "בחר קובץ נתונים"
            };

            if (dialog.ShowDialog() == true)
            {
                FilePathTextBox.Text = dialog.FileName;
                LoadFileColumns(dialog.FileName);
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
            using (var reader = new StreamReader(filePath))
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
            var json = File.ReadAllText(filePath);
            var data = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(json);
            
            if (data?.Count > 0)
            {
                _columnNames = data[0].Keys.ToList();
            }
        }

        private void BuildColumnsUI()
        {
            ColumnsPanel.Children.Clear();
            NoFileMessageTextBlock.Visibility = Visibility.Collapsed;
            ColumnsPanel.Visibility = Visibility.Visible;

            foreach (string columnName in _columnNames)
            {
                var columnPanel = CreateColumnPanel(columnName);
                ColumnsPanel.Children.Add(columnPanel);
                
                // אתחול הגדרות עמודה
                _columnSettings[columnName] = new ColumnSettings();
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
            
            // כותרת עמודה
            var header = new TextBlock
            {
                Text = $"📊 {columnName}",
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                Foreground = System.Windows.Media.Brushes.DarkBlue,
                Margin = new Thickness(0, 0, 0, 10)
            };
            mainPanel.Children.Add(header);

            // פאנל פעולות ניקוי
            var cleaningGroup = CreateOperationGroup("🧹 פעולות ניקוי", new[]
            {
                ("הסר שורות ריקות", "remove_empty_rows"),
                ("הסר רווחים מיותרים", "strip_whitespace"),
                ("הסר כפילויות", "remove_duplicates"),
                ("הסר ערך חסר או null", "remove_if_missing"),
                ("החלף ערכי null", "replace_nulls"),
                ("הסר אם שווה לערך", "remove_if_equals"),
                ("הסר ערכים לא תקינים", "remove_if_invalid"),
                ("מחק עמודה זו", "drop_columns")
            }, columnName, "cleaning");
            mainPanel.Children.Add(cleaningGroup);

            // פאנל טרנספורמציות
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

        private void OperationCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            var checkBox = sender as CheckBox;
            var tagParts = checkBox.Tag.ToString().Split('|');
            var columnName = tagParts[0];
            var category = tagParts[1];
            var operation = tagParts[2];

            if (!_columnSettings.ContainsKey(columnName))
                _columnSettings[columnName] = new ColumnSettings();

            var settings = _columnSettings[columnName];

            if (checkBox.IsChecked == true)
            {
                // פעולות שדורשות קלט מהמשתמש
                string userInput = null;
                
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
                    
                    if (userInput == null) // אפשר ערך ריק
                    {
                        checkBox.IsChecked = false;
                        return;
                    }
                }
                else if (operation == "replace_values")
                {
                    var valueMapping = Helpers.InputDialogs.ShowValueMappingDialog(
                        "החלף ערכים",
                        columnName);
                    
                    if (valueMapping == null || valueMapping.Count == 0)
                    {
                        checkBox.IsChecked = false;
                        return;
                    }
                    
                    // שמירת המיפוי כJSON string
                    userInput = System.Text.Json.JsonSerializer.Serialize(valueMapping);
                }

                if (!settings.Operations.ContainsKey(category))
                    settings.Operations[category] = new List<string>();
                
                if (!settings.Operations[category].Contains(operation))
                {
                    settings.Operations[category].Add(operation);
                    
                    // דיבוג - הדפס מה נשמר
                    System.Diagnostics.Debug.WriteLine($"Added operation: {operation} to category: {category} for column: {columnName}");
                    
                    // שמירת הקלט של המשתמש
                    if (!settings.UserInputs.ContainsKey(operation))
                        settings.UserInputs[operation] = new Dictionary<string, object>();
                        
                    if (userInput != null)
                    {
                        if (operation == "remove_if_invalid")
                        {
                            // המרת הרשימה לarray
                            var values = userInput.Split(',').Select(v => v.Trim()).Where(v => !string.IsNullOrEmpty(v)).ToArray();
                            settings.UserInputs[operation]["values"] = values;
                        }
                        else if (operation == "replace_values")
                        {
                            // שמירת המיפוי כJSON
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

        private void ResetSettings_Click(object sender, RoutedEventArgs e)
        {
            _columnSettings.Clear();
            
            // איפוס כל ה-checkboxes
            foreach (Border border in ColumnsPanel.Children)
            {
                ResetCheckBoxesInPanel(border);
            }
            
            ResultTextBlock.Text = "ההגדרות אופסו";
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
                    File.WriteAllText(saveDialog.FileName, json);
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

                ResultTextBlock.Text = "מריץ Pipeline...";

                var config = BuildPipelineConfig();
                
                // דיבוג - בדוק שהקונפיגורציה נבנתה נכון
                if (config == null)
                {
                    ResultTextBlock.Text = "❌ שגיאה: לא ניתן לבנות קונפיגורציה";
                    return;
                }

                if (config.Source == null)
                {
                    ResultTextBlock.Text = "❌ שגיאה: Source לא נוצר";
                    return;
                }

                var json = JsonConvert.SerializeObject(config, Formatting.Indented);

                // דיבוג - הצג את ה-JSON שנשלח
                ResultTextBlock.Text = $"שולח קונפיגורציה:\n{json}\n\nמעבד...";

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
                ResultTextBlock.Text = $"❌ שגיאה בהרצת Pipeline: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}";
            }
        }

        private PipelineConfig BuildPipelineConfig()
        {
            try
            {
                var processors = new List<ProcessorConfig>();

                // פעולות גלובליות (על כל הנתונים)
                var globalOperations = new List<Dictionary<string, object>>();
                var fieldBasedOperations = new Dictionary<string, List<Dictionary<string, object>>>();

                // איסוף כל הפעולות וסיווגן
                foreach (var column in _columnSettings)
                {
                    var columnName = column.Key;
                    var settings = column.Value;

                    foreach (var category in settings.Operations)
                    {
                        foreach (var operation in category.Value)
                        {
                            if (IsGlobalOperation(operation))
                            {
                                // פעולות גלובליות - נוסיף רק פעם אחת
                                var globalOp = new Dictionary<string, object>
                                {
                                    ["action"] = operation
                                };

                                // עבור remove_if_missing, נאסף את כל השדות שנבחרו
                                if (operation == "remove_if_missing")
                                {
                                    var existingOp = globalOperations.FirstOrDefault(op => op["action"].ToString() == "remove_if_missing");
                                    if (existingOp != null)
                                    {
                                        // הוסף לרשימת השדות הקיימת
                                        var fields = (List<string>)existingOp["fields"];
                                        if (!fields.Contains(columnName))
                                            fields.Add(columnName);
                                    }
                                    else
                                    {
                                        globalOp["fields"] = new List<string> { columnName };
                                        globalOperations.Add(globalOp);
                                    }
                                }
                                else
                                {
                                    // פעולות אחרות - הוסף רק אם לא קיים
                                    if (!globalOperations.Any(op => op["action"].ToString() == operation))
                                    {
                                        globalOperations.Add(globalOp);
                                    }
                                }
                            }
                            else
                            {
                                // פעולות לפי שדה
                                if (!fieldBasedOperations.ContainsKey(columnName))
                                    fieldBasedOperations[columnName] = new List<Dictionary<string, object>>();

                                var operationConfig = new Dictionary<string, object>
                                {
                                    ["action"] = operation,
                                    ["fields"] = new[] { columnName }
                                };

                                // הוספת קונפיגורציה מיוחדת לפעולות מסוימות
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
                                    // הוסף את הערך שהמשתמש הגדיר
                                    if (settings.UserInputs.ContainsKey(operation) && settings.UserInputs[operation].ContainsKey("value"))
                                    {
                                        operationConfig["value"] = settings.UserInputs[operation]["value"];
                                    }
                                    else
                                    {
                                        operationConfig["value"] = ""; // ערך ברירת מחדל
                                    }
                                }
                                else if (operation == "remove_if_equals")
                                {
                                    operationConfig["field"] = columnName;
                                    // הוסף את הערך שהמשתמש הגדיר
                                    if (settings.UserInputs.ContainsKey(operation) && settings.UserInputs[operation].ContainsKey("value"))
                                    {
                                        operationConfig["value"] = settings.UserInputs[operation]["value"];
                                    }
                                    else
                                    {
                                        operationConfig["value"] = ""; // ערך ברירת מחדל
                                    }
                                }
                                else if (operation == "remove_if_invalid")
                                {
                                    operationConfig["field"] = columnName;
                                    // הוסף את הרשימה של ערכים לא תקינים
                                    if (settings.UserInputs.ContainsKey(operation) && settings.UserInputs[operation].ContainsKey("values"))
                                    {
                                        operationConfig["values"] = settings.UserInputs[operation]["values"];
                                    }
                                    else
                                    {
                                        operationConfig["values"] = new[] { "N/A", "לא ידוע" }; // ערכי ברירת מחדל
                                    }
                                }
                                else if (operation == "replace_values")
                                {
                                    operationConfig["field"] = columnName;
                                    // הוסף את המיפוי שהמשתמש הגדיר
                                    if (settings.UserInputs.ContainsKey(operation) && settings.UserInputs[operation].ContainsKey("mapping_json"))
                                    {
                                        var mappingJson = settings.UserInputs[operation]["mapping_json"].ToString();
                                        var mapping = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(mappingJson);
                                        operationConfig["mapping"] = mapping;
                                    }
                                    else
                                    {
                                        operationConfig["mapping"] = new Dictionary<string, string>(); // ערך ברירת מחדל
                                    }
                                }
                                else if (operation == "drop_columns")
                                {
                                    operationConfig["fields"] = new[] { columnName };
                                }

                                fieldBasedOperations[columnName].Add(operationConfig);
                                
                                // דיבוג - הדפס איזה processor type נבחר
                                var processorType = GetProcessorType(category.Key);
                                System.Diagnostics.Debug.WriteLine($"Operation: {operation}, Category: {category.Key}, ProcessorType: {processorType}");
                            }
                        }
                    }
                }

                // הוספת processor עבור פעולות גלובליות
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

                // הוספת processors עבור פעולות לפי שדה - מקובצים לפי processor type
                var cleaningOps = new List<Dictionary<string, object>>();
                var transformOps = new List<Dictionary<string, object>>();
                
                foreach (var fieldOps in fieldBasedOperations)
                {
                    foreach (var operationConfig in fieldOps.Value)
                    {
                        var action = operationConfig["action"].ToString();
                        
                        // קבע לאיזה processor זה שייך
                        if (IsTransformOperation(action))
                        {
                            transformOps.Add(operationConfig);
                        }
                        else
                        {
                            cleaningOps.Add(operationConfig);
                        }
                    }
                }
                
                // הוסף cleaner processor אם יש פעולות ניקוי
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
                
                // הוסף transformer processor אם יש פעולות טרנספורמציה
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

                // אם אין processors, הוסף cleaner בסיסי
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

                // קבלת סוג הקובץ
                string fileExtension = Path.GetExtension(FilePathTextBox.Text)?.TrimStart('.').ToLower();
                if (string.IsNullOrEmpty(fileExtension))
                {
                    fileExtension = "csv"; // ברירת מחדל
                }

                // יצירת קונפיגורציה
                var config = new PipelineConfig
                {
                    Source = new SourceConfig
                    {
                        Type = fileExtension,
                        Path = FilePathTextBox.Text
                    },
                    Processors = processors.ToArray(),
                    Target = new TargetConfig
                    {
                        Type = "csv",
                        Path = Path.ChangeExtension(FilePathTextBox.Text, "_processed.csv")
                    }
                };

                // דיבוג
                System.Diagnostics.Debug.WriteLine("=== Generated Pipeline Config ===");
                System.Diagnostics.Debug.WriteLine($"Global operations: {globalOperations.Count}");
                System.Diagnostics.Debug.WriteLine($"Field-based operations: {fieldBasedOperations.Count}");
                System.Diagnostics.Debug.WriteLine($"Total processors: {config.Processors?.Length}");
                
                // הדפס את כל הprocessors
                for (int i = 0; i < config.Processors.Length; i++)
                {
                    var processor = config.Processors[i];
                    System.Diagnostics.Debug.WriteLine($"Processor {i}: Type={processor.Type}");
                    if (processor.Config != null && processor.Config.ContainsKey("operations"))
                    {
                        var ops = processor.Config["operations"];
                        System.Diagnostics.Debug.WriteLine($"  Operations: {JsonConvert.SerializeObject(ops, Formatting.Indented)}");
                    }
                }

                return config;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error building config: {ex.Message}");
                throw;
            }
        }

        private bool IsGlobalOperation(string operation)
        {
            // פעולות שצריכות לחול על כל הנתונים
            var globalOperations = new[]
            {
                "remove_duplicates",
                "remove_empty_rows"
                // הסרתי remove_if_missing כי זה יכול להיות גם לפי שדה ספציפי
            };
            
            return globalOperations.Contains(operation);
        }

        private bool IsTransformOperation(string operation)
        {
            // פעולות שמתאימות ל-Transformer
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

        private string GetProcessorType(string category)
        {
            return category switch
            {
                "cleaning" => "cleaner",
                "transform" => "transformer",
                _ => "cleaner"
            };
        }
    }

    public class ColumnSettings
    {
        public Dictionary<string, List<string>> Operations { get; set; } = new Dictionary<string, List<string>>();
        public Dictionary<string, Dictionary<string, object>> UserInputs { get; set; } = new Dictionary<string, Dictionary<string, object>>();
    }
}