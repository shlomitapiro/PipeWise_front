// PipeWise_Client/MainWindow/Parts/MainWindow.UISettings.cs

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Newtonsoft.Json;
using PipeWiseClient.Models;

namespace PipeWiseClient
{
    public partial class MainWindow
    {
        // ===== UI Settings =====
        public class UISettings
        {
            public double OperationsAreaHeight { get; set; } = 2;
            public double NotificationsAreaHeight { get; set; } = 1;
            public bool NotificationsCollapsed { get; set; } = false;
            public double WindowWidth { get; set; } = 900;
            public double WindowHeight { get; set; } = 700;
        }

        private void SaveUISettings()
        {
            try
            {
                var settings = new UISettings
                {
                    OperationsAreaHeight = GetGridRowHeight(2),
                    NotificationsAreaHeight = GetGridRowHeight(5),
                    NotificationsCollapsed = _notificationsCollapsed,
                    WindowWidth = this.Width,
                    WindowHeight = this.Height
                };

                var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
                File.WriteAllText(SETTINGS_FILE, json, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                _notifications.Warning("שמירת הגדרות", "לא ניתן לשמור הגדרות ממשק", ex.Message);
            }
        }

        private void LoadUISettings()
        {
            try
            {
                if (!File.Exists(SETTINGS_FILE))
                    return;

                var json = File.ReadAllText(SETTINGS_FILE, Encoding.UTF8);
                var settings = JsonConvert.DeserializeObject<UISettings>(json);

                if (settings != null)
                {
                    SetGridRowHeight(2, settings.OperationsAreaHeight);
                    SetGridRowHeight(5, settings.NotificationsAreaHeight);

                    _notificationsCollapsed = settings.NotificationsCollapsed;
                    if (_notificationsCollapsed && NotificationsScrollViewer != null && CollapseNotificationsBtn != null)
                    {
                        NotificationsScrollViewer.Visibility = Visibility.Collapsed;
                        CollapseNotificationsBtn.Content = "📂";
                    }

                    if (settings.WindowWidth > 0 && settings.WindowHeight > 0)
                    {
                        this.Width = Math.Max(settings.WindowWidth, 600);
                        this.Height = Math.Max(settings.WindowHeight, 500);
                    }
                }
            }
            catch (Exception ex)
            {
                _notifications.Warning("טעינת הגדרות", "לא ניתן לטעון הגדרות ממשק, נטענות ברירות מחדל", ex.Message);
            }
        }

        private double GetGridRowHeight(int rowIndex)
        {
            var grid = FindName("MainGrid") as Grid ??
                      (this.Content as Grid);

            if (grid != null && rowIndex < grid.RowDefinitions.Count)
            {
                var rowDefinition = grid.RowDefinitions[rowIndex];
                return rowDefinition.Height.Value;
            }
            return 1.0;
        }

        private void SetGridRowHeight(int rowIndex, double height)
        {
            var grid = FindName("MainGrid") as Grid ??
                      (this.Content as Grid);

            if (grid != null && rowIndex < grid.RowDefinitions.Count)
            {
                var rowDefinition = grid.RowDefinitions[rowIndex];
                rowDefinition.Height = new GridLength(Math.Max(height, 0.5), GridUnitType.Star);
            }
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveUISettings();
        }

        public void ResetUIToDefault()
        {
            try
            {
                SetGridRowHeight(2, 2.0);
                SetGridRowHeight(5, 1.0);

                this.Width = 900;
                this.Height = 700;

                if (NotificationsScrollViewer != null && CollapseNotificationsBtn != null)
                {
                    _notificationsCollapsed = false;
                    NotificationsScrollViewer.Visibility = Visibility.Visible;
                    CollapseNotificationsBtn.Content = "📦";
                }

                _notifications.Success("איפוס ממשק", "ממשק המשתמש הוחזר לברירת מחדל");

                _loadedConfig = null;
                _hasCompatibleConfig = false;
                _hasLastRunReport = false;
                SetPhase(UiPhase.Idle);
            }
            catch (Exception ex)
            {
                _notifications.Error("שגיאה באיפוס ממשק", "לא ניתן לאפס את ממשק המשתמש", ex.Message);
            }
        }

        private async Task DetectColumnTypes(string filePath)
        {
            try
            {
                var extension = Path.GetExtension(filePath).ToLower();
                var fileType = extension switch
                {
                    ".csv" => "csv",
                    ".json" => "json",
                    ".xml" => "xml",
                    ".xlsx" or ".xls" => "excel",
                    _ => "csv"
                };

                var payload = new
                {
                    source = new
                    {
                        type = fileType,
                        path = filePath
                    }
                };

                var profileResult = await _api.ProfileColumnsAsync(payload);

                if (profileResult?.Columns != null)
                {
                    foreach (var column in profileResult.Columns)
                    {
                        if (_columnSettings.ContainsKey(column.Name))
                        {
                            _columnSettings[column.Name].InferredType = column.InferredType;
                        }
                        else
                        {
                            _columnSettings[column.Name] = new ColumnSettings
                            {
                                InferredType = column.InferredType
                            };
                        }
                    }
                }
                #if DEBUG
                if (profileResult?.Columns != null)
                {
                    var debugInfo = string.Join("\n", profileResult.Columns.Select(c =>
                        $"{c.Name}: {c.InferredType}"));
                    _notifications.Info("DEBUG - סוגי עמודות", debugInfo);
                }
                #endif
            }
            catch (Exception ex)
            {
                _notifications.Warning("זיהוי סוגי עמודות", "לא ניתן לזהות סוגי עמודות", ex.Message);
            }
        }
    }
}