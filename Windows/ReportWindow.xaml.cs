using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using PipeWiseClient.Services;

namespace PipeWiseClient.Windows
{
    public partial class ReportsWindow : Window
    {
        private List<ReportDisplayModel> _reports = new List<ReportDisplayModel>();

        public ReportsWindow()
        {
            InitializeComponent();
             _ = LoadReportsAsync();
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadReportsAsync();
        }

        private async void CleanupButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "האם אתה בטוח שברצונך לנקות דוחות ישנים?\n\nפעולה זו תמחק דוחות שנוצרו לפני 30 יום או שמירות מעל 100 דוחות.",
                "אישור ניקוי דוחות",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    UpdateStatus("מנקה דוחות ישנים...");
                    
                    var cleanupResult = await ApiClient.CleanupOldReportsAsync(100, 30);
                    
                    if (cleanupResult != null)
                    {
                        MessageBox.Show(
                            $"ניקוי הושלם בהצלחה!\n\nנמחקו: {cleanupResult.DeletedReports} דוחות\nנשמרו: {cleanupResult.KeptReports} דוחות",
                            "ניקוי הושלם",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);

                        await LoadReportsAsync();
                    }
                    else
                    {
                        MessageBox.Show("שגיאה בניקוי דוחות", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"שגיאה בניקוי דוחות: {ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    UpdateStatus("מוכן");
                }
            }
        }

        private async void OpenHtmlButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ReportDisplayModel report)
            {
                try
                {
                    UpdateStatus("מוריד דוח HTML...");

                    // הורדת הקובץ HTML מהשרת
                    var htmlData = await ApiClient.DownloadReportFileAsync(report.ReportId, "html");
                    
                    if (htmlData != null)
                    {
                        // יצירת קובץ זמני
                        var tempFileName = Path.Combine(Path.GetTempPath(), $"{report.ReportId}_report.html");
                        
                        // שמירת הקובץ הזמני
                        await File.WriteAllBytesAsync(tempFileName, htmlData);
                        
                        // פתיחה בדפדפן המחדל
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = tempFileName,
                            UseShellExecute = true
                        });
                        
                        UpdateStatus($"נפתח דוח HTML: {report.PipelineName}");
                        
                        // מחיקת הקובץ הזמני אחרי 30 שניות (אופציונלי)
                        _ = Task.Delay(30000).ContinueWith(_ => 
                        {
                            try 
                            { 
                                if (File.Exists(tempFileName))
                                    File.Delete(tempFileName); 
                            }
                            catch { /* אם נכשל למחוק, זה לא נורא */ }
                        });
                    }
                    else
                    {
                        MessageBox.Show("לא ניתן להוריד את דוח ה-HTML", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"שגיאה בפתיחת דוח HTML: {ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    UpdateStatus("מוכן");
                }
            }
        }

        private async void DownloadPdfButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ReportDisplayModel report)
            {
                try
                {
                    UpdateStatus("בודק זמינות PDF...");

                    // בדיקה אם יש PDF זמין עבור הדוח הזה
                    if (string.IsNullOrEmpty(report.PdfPath))
                    {
                        MessageBox.Show(
                            "דוח PDF לא זמין עבור Pipeline זה.\n\n" +
                            "ייתכן שהשרת לא מגונדר ליצירת PDF או שהתכונה מושבתת.",
                            "PDF לא זמין", 
                            MessageBoxButton.OK, 
                            MessageBoxImage.Information);
                        return;
                    }

                    UpdateStatus("מוריד דוח PDF...");

                    // ניסיון להוריד את קובץ ה-PDF מהשרת
                    var pdfData = await ApiClient.DownloadReportFileAsync(report.ReportId, "pdf");
                    
                    if (pdfData != null && pdfData.Length > 0)
                    {
                        // הצגת דיאלוג שמירה
                        var saveDialog = new SaveFileDialog
                        {
                            Filter = "PDF Files (*.pdf)|*.pdf",
                            FileName = $"{report.PipelineName}_report_{DateTime.Now:yyyyMMdd_HHmmss}.pdf",
                            Title = "שמור דוח PDF"
                        };

                        if (saveDialog.ShowDialog() == true)
                        {
                            await File.WriteAllBytesAsync(saveDialog.FileName, pdfData);
                            
                            var result = MessageBox.Show(
                                $"הדוח נשמר בהצלחה!\n\nהאם תרצה לפתוח את הקובץ?",
                                "דוח נשמר",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Information);

                            if (result == MessageBoxResult.Yes)
                            {
                                Process.Start(new ProcessStartInfo
                                {
                                    FileName = saveDialog.FileName,
                                    UseShellExecute = true
                                });
                            }

                            UpdateStatus($"דוח PDF נשמר: {report.PipelineName}");
                        }
                    }
                    else
                    {
                        MessageBox.Show(
                            "שגיאה בהורדת דוח PDF.\n\n" +
                            "סיבות אפשריות:\n" +
                            "• השרת לא מגונדר ליצירת PDF\n" +
                            "• חסרות ספריות נדרשות (weasyprint)\n" +
                            "• קובץ PDF לא נוצר עבור הדוח הזה",
                            "שגיאה בהורדת PDF", 
                            MessageBoxButton.OK, 
                            MessageBoxImage.Warning);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"שגיאה בהורדת דוח PDF:\n\n{ex.Message}\n\n" +
                        "ודא שהשרת פועל ושהתכונה מופעלת.",
                        "שגיאה", 
                        MessageBoxButton.OK, 
                        MessageBoxImage.Error);
                }
                finally
                {
                    UpdateStatus("מוכן");
                }
            }
        }


        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ReportDisplayModel report)
            {
                var result = MessageBox.Show(
                    $"האם אתה בטוח שברצונך למחוק את הדוח:\n'{report.PipelineName}'?\n\nפעולה זו לא ניתנת לביטול.",
                    "אישור מחיקת דוח",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        UpdateStatus("מוחק דוח...");

                        var success = await ApiClient.DeleteReportAsync(report.ReportId);
                        
                        if (success)
                        {
                            UpdateStatus("דוח נמחק בהצלחה");
                            await LoadReportsAsync(); // רענון הרשימה
                        }
                        else
                        {
                            MessageBox.Show("שגיאה במחיקת הדוח", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"שגיאה במחיקת דוח: {ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    finally
                    {
                        UpdateStatus("מוכן");
                    }
                }
            }
        }

        private async Task LoadReportsAsync()
        {
            try
            {
                ShowLoading();
                UpdateStatus("טוען דוחות...");

                var reports = await ApiClient.GetReportsListAsync(100);
                
                if (reports?.Count > 0)
                {
                    _reports = reports.Select(r => new ReportDisplayModel(r)).ToList();
                    ShowReports();
                }
                else
                {
                    _reports = new List<ReportDisplayModel>();
                    ShowEmptyState();
                }

                UpdateReportsCount();
                UpdateLastUpdateTime();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"שגיאה בטעינת דוחות: {ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
                ShowEmptyState();
            }
            finally
            {
                UpdateStatus("מוכן");
            }
        }

        private void ShowLoading()
        {
            LoadingPanel.Visibility = Visibility.Visible;
            EmptyStatePanel.Visibility = Visibility.Collapsed;
            ReportsScrollViewer.Visibility = Visibility.Collapsed;
        }

        private void ShowEmptyState()
        {
            LoadingPanel.Visibility = Visibility.Collapsed;
            EmptyStatePanel.Visibility = Visibility.Visible;
            ReportsScrollViewer.Visibility = Visibility.Collapsed;
        }

        private void ShowReports()
        {
            LoadingPanel.Visibility = Visibility.Collapsed;
            EmptyStatePanel.Visibility = Visibility.Collapsed;
            ReportsScrollViewer.Visibility = Visibility.Visible;
            
            ReportsItemsControl.ItemsSource = _reports;
        }

        private void UpdateReportsCount()
        {
            TotalReportsTextBlock.Text = $"{_reports.Count} דוחות";
        }

        private void UpdateStatus(string message)
        {
            StatusTextBlock.Text = message;
        }

        private void UpdateLastUpdateTime()
        {
            LastUpdateTextBlock.Text = $"עדכון אחרון: {DateTime.Now:HH:mm:ss}";
        }
    }

    // מודל לתצוגה
    public class ReportDisplayModel
        {
            public string ReportId { get; set; } = string.Empty;
            public string PipelineName { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public string StatusText { get; set; } = string.Empty;
            public string StatusColor { get; set; } = string.Empty;
            public string Duration { get; set; } = string.Empty;
            public string FormattedDate { get; set; } = string.Empty;
            public string RowsInfo { get; set; } = string.Empty;
            public string SourceType { get; set; } = string.Empty;
            public int TotalErrors { get; set; }
            public int TotalWarnings { get; set; }
            public bool HasHtml { get; set; }
            public bool HasPdf { get; set; }
            public string HtmlPath { get; set; } = string.Empty;
            public string PdfPath { get; set; } = string.Empty;

            public ReportDisplayModel(ReportInfo report)
            {
                ReportId = report.ReportId ?? string.Empty;
                PipelineName = report.PipelineName ?? "Pipeline ללא שם";
                Status = report.Status ?? string.Empty;
                Duration = report.Duration ?? "לא ידוע";
                TotalErrors = report.TotalErrors;
                TotalWarnings = report.TotalWarnings;
                SourceType = report.SourceType ?? "לא ידוע";
                HtmlPath = report.HtmlPath ?? string.Empty;
                PdfPath = report.PdfPath ?? string.Empty;
                
                // חישוב StatusText ו-StatusColor
                (StatusText, StatusColor) = Status.ToLower() switch
                {
                    "success" => ("הושלם", "#27AE60"),
                    "warning" => ("אזהרות", "#F39C12"),
                    "error" => ("שגיאה", "#E74C3C"),
                    _ => ("לא ידוע", "#95A5A6")
                };

                // פורמט תאריך
                if (DateTime.TryParse(report.CreatedAt ?? report.StartTime, out var date))
                {
                    FormattedDate = date.ToString("dd/MM/yyyy HH:mm", CultureInfo.GetCultureInfo("he-IL"));
                }
                else
                {
                    FormattedDate = "תאריך לא זמין";
                }

                // מידע על שורות
                RowsInfo = $"{report.InputRows:N0} → {report.OutputRows:N0}";

                // מצב קבצים
                HasHtml = report.FilesExist?.Html ?? false;
                HasPdf = report.FilesExist?.Pdf ?? false;
            }
        }
    }
