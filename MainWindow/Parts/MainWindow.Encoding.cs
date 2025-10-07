// PipeWise_Client/MainWindow/Parts/MainWindow.Encoding.cs

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using PipeWiseClient.Windows;

namespace PipeWiseClient
{
    public partial class MainWindow
    {
        private async Task OpenCategoricalEncodingWindow(string fieldName)
        {
            await Task.Yield();
            try
            {
                var filePath = FilePathTextBox.Text?.Trim();
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                {
                    _notifications.Warning("קובץ חסר",
                        "יש לבחור קובץ תקין לפני הגדרת קידוד קטגוריאלי");
                    return;
                }

                var settings = _columnSettings[fieldName];
                var encodingWindow = new CategoricalEncodingWindow(_api, filePath, fieldName, settings)
                {
                    Owner = this
                };

                if (encodingWindow.ShowDialog() == true && encodingWindow.Result != null)
                {
                    var winCfg = encodingWindow.Result;

                    var mapped = new PipeWiseClient.Models.CategoricalEncodingConfig
                    {
                        Field = winCfg.Field,
                        Mapping = new Dictionary<string, int>(winCfg.Mapping),
                        TargetField = winCfg.TargetField,
                        ReplaceOriginal = winCfg.ReplaceOriginal,
                        DeleteOriginal = winCfg.DeleteOriginal,
                        DefaultValue = winCfg.DefaultValue
                    };

                    settings.CategoricalEncoding = mapped;

                    _notifications.Success("קידוד קטגוריאלי",
                        $"קידוד קטגוריאלי הוגדר עבור שדה '{fieldName}' עם {mapped.Mapping.Count} ערכים");
                }
            }
            catch (Exception ex)
            {
                _notifications.Error("שגיאה בקידוד קטגוריאלי",
                    "לא ניתן לפתוח חלון קידוד קטגוריאלי", ex.Message);
            }
        }
    }
}