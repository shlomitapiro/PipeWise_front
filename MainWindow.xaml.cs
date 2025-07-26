using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
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
                ("הסר אם ערך חסר", "remove_if_missing")
            }, columnName, "cleaning");
            mainPanel.Children.Add(cleaningGroup);

            // פאנל טרנספורמציות
            var transformGroup = CreateOperationGroup("🔄 טרנספורמציות", new[]
            {
                ("הפוך לאותיות גדולות", "to_uppercase"),
                ("הפוך לאותיות קטנות", "to_lowercase"),
                ("המר לטיפוס מספרי", "convert_to_number")
            }, columnName, "transform");
            mainPanel.Children.Add(transformGroup);

            // פאנל ולידציה
            var validationGroup = CreateOperationGroup("✅ ולידציה", new[]
            {
                ("שדה חובה", "required_field"),
                ("בדוק ערכים תקינים", "validate_values")
            }, columnName, "validation");
            mainPanel.Children.Add(validationGroup);

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

                // בניית processors בהתבסס על ההגדרות
                foreach (var column in _columnSettings)
                {
                    var columnName = column.Key;
                    var settings = column.Value;

                    foreach (var category in settings.Operations)
                    {
                        if (category.Value.Count > 0)
                        {
                            processors.Add(new ProcessorConfig
                            {
                                Type = GetProcessorType(category.Key),
                                Config = new Dictionary<string, object>
                                {
                                    ["operations"] = category.Value.Select(op => new Dictionary<string, object>
                                    {
                                        ["action"] = op,
                                        ["fields"] = new[] { columnName }
                                    }).ToList()
                                }
                            });
                        }
                    }
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
                System.Diagnostics.Debug.WriteLine($"Source Type: {config.Source?.Type}");
                System.Diagnostics.Debug.WriteLine($"Source Path: {config.Source?.Path}");
                System.Diagnostics.Debug.WriteLine($"Processors Count: {config.Processors?.Length}");
                System.Diagnostics.Debug.WriteLine($"Target Type: {config.Target?.Type}");

                return config;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error building config: {ex.Message}");
                throw;
            }
        }

        private string GetProcessorType(string category)
        {
            return category switch
            {
                "cleaning" => "cleaner",
                "transform" => "transformer",
                "validation" => "validator",
                _ => "cleaner"
            };
        }
    }

    public class ColumnSettings
    {
        public Dictionary<string, List<string>> Operations { get; set; } = new Dictionary<string, List<string>>();
    }
}