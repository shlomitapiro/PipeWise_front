using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Newtonsoft.Json;
using PipeWiseClient.Models;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace PipeWiseClient.Views
{
    public partial class PipelinePage : Page
    {
        private readonly HttpClient _httpClient = new HttpClient();
        private PipelineConfig _currentConfig = null;

        public PipelinePage()
        {
            InitializeComponent();
            
            // הירשמות לאירוע Unloaded לניקוי משאבים
            this.Unloaded += PipelinePage_Unloaded;
        }
        
        private void PipelinePage_Unloaded(object sender, RoutedEventArgs e)
        {
            _httpClient?.Dispose();
        }

        private void BrowseFile_Click(object sender, RoutedEventArgs e)
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
                    FilePathTextBox.Text = dialog.FileName;
                    
                    // עדכון תצוגת תוצאות
                    ResultTextBox.Text = $"קובץ נבחר: {Path.GetFileName(dialog.FileName)}\n" +
                                        $"גודל: {new FileInfo(dialog.FileName).Length:N0} bytes\n" +
                                        $"נתיב מלא: {dialog.FileName}\n\n" +
                                        "לחץ על 'Load Config' כדי לטעון קונפיגורציה או 'Run Pipeline' כדי להריץ עם הגדרות ברירת מחדל.";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"שגיאה בבחירת קובץ: {ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadConfig_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new OpenFileDialog
                {
                    Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                    Title = "בחר קובץ קונפיגורציה"
                };

                if (dialog.ShowDialog() == true)
                {
                    var configText = File.ReadAllText(dialog.FileName);
                    _currentConfig = JsonConvert.DeserializeObject<PipelineConfig>(configText);

                    if (_currentConfig != null)
                    {
                        ResultTextBox.Text = $"✅ קונפיגורציה נטענה בהצלחה: {Path.GetFileName(dialog.FileName)}\n\n" +
                                           $"📊 פרטי קונפיגורציה:\n" +
                                           $"• מקור: {_currentConfig.Source.Type} ({_currentConfig.Source.Path})\n" +
                                           $"• מספר עיבודים: {_currentConfig.Processors.Length}\n" +
                                           $"• יעד: {_currentConfig.Target.Type} ({_currentConfig.Target.Path})\n\n" +
                                           "כעת ניתן להריץ את הpipeline עם הקונפיגורציה הזו.";
                    }
                    else
                    {
                        MessageBox.Show("קובץ הקונפיגורציה לא תקין או ריק", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"שגיאה בטעינת קונפיגורציה: {ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
                _currentConfig = null;
            }
        }

        private void SaveConfig_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_currentConfig == null)
                {
                    // יצירת קונפיגורציה ברירת מחדל אם אין קונפיגורציה קיימת
                    if (string.IsNullOrEmpty(FilePathTextBox.Text))
                    {
                        MessageBox.Show("יש לבחור קובץ נתונים תחילה", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    _currentConfig = CreateDefaultConfig();
                }

                var dialog = new SaveFileDialog
                {
                    Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                    Title = "שמור קונפיגורציה",
                    FileName = "pipeline_config.json"
                };

                if (dialog.ShowDialog() == true)
                {
                    var configJson = JsonConvert.SerializeObject(_currentConfig, Formatting.Indented);
                    File.WriteAllText(dialog.FileName, configJson);

                    ResultTextBox.Text = $"✅ קונפיגורציה נשמרה בהצלחה!\n\n" +
                                       $"📁 נתיב: {dialog.FileName}\n" +
                                       $"📊 גודל: {new FileInfo(dialog.FileName).Length} bytes\n\n" +
                                       "ניתן לטעון את הקונפיגורציה הזו בפעם הבאה.";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"שגיאה בשמירת קונפיגורציה: {ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void RunPipeline_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // בדיקה שיש קובץ נתונים
                if (string.IsNullOrEmpty(FilePathTextBox.Text) || !File.Exists(FilePathTextBox.Text))
                {
                    MessageBox.Show("יש לבחור קובץ נתונים קיים תחילה", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // שימוש בקונפיגורציה קיימת או יצירת ברירת מחדל
                var config = _currentConfig ?? CreateDefaultConfig();

                ResultTextBox.Text = "🚀 מריץ Pipeline...\nאנא המתן...";

                // שליחת הקובץ והקונפיגורציה לשרת
                var result = await SendPipelineRequest(config);

                ResultTextBox.Text = $"✅ Pipeline הושלם בהצלחה!\n\n📊 תוצאות:\n{result}";
            }
            catch (Exception ex)
            {
                ResultTextBox.Text = $"❌ שגיאה בהרצת Pipeline:\n{ex.Message}";
                MessageBox.Show($"שגיאה מפורטת: {ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private PipelineConfig CreateDefaultConfig()
        {
            if (string.IsNullOrEmpty(FilePathTextBox.Text))
                throw new InvalidOperationException("לא נבחר קובץ נתונים");

            var fileExtension = Path.GetExtension(FilePathTextBox.Text).ToLower();
            var sourceType = fileExtension switch
            {
                ".csv" => "csv",
                ".json" => "json",
                ".xlsx" or ".xls" => "excel",
                ".xml" => "xml",
                _ => "csv"
            };

            var outputFileName = Path.GetFileNameWithoutExtension(FilePathTextBox.Text) + "_processed.csv";

            return new PipelineConfig
            {
                Source = new SourceConfig
                {
                    Type = sourceType,
                    Path = FilePathTextBox.Text
                },
                Processors = new[]
                {
                    new ProcessorConfig
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
                    }
                },
                Target = new TargetConfig
                {
                    Type = "csv",
                    Path = outputFileName
                }
            };
        }

        private async Task<string> SendPipelineRequest(PipelineConfig config)
        {
            using var content = new MultipartFormDataContent();
            
            // הוספת הקובץ
            var fileBytes = await File.ReadAllBytesAsync(FilePathTextBox.Text);
            content.Add(new ByteArrayContent(fileBytes), "file", Path.GetFileName(FilePathTextBox.Text));
            
            // הוספת הקונפיגורציה
            var configJson = JsonConvert.SerializeObject(config);
            content.Add(new StringContent(configJson, Encoding.UTF8, "application/json"), "config");

            // שליחה לשרת
            var response = await _httpClient.PostAsync("http://127.0.0.1:8000/run-pipeline", content);
            var result = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Server error ({response.StatusCode}): {result}");
            }

            return result;
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
                MessageBox.Show($"שגיאה בפתיחת חלון הדוחות: {ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}