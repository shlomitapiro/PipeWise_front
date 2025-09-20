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
            try
            {
                var filePath = FilePathTextBox.Text?.Trim();
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                {
                    AddWarningNotification("???? ???",
                        "?? ????? ???? ???? ???? ????? ????? ?????????");
                    return;
                }

                // פתיחת חלון קידוד קטגוריות
                var encodingWindow = new CategoricalEncodingWindow(_api, filePath, fieldName)
                {
                    Owner = this
                };

                if (encodingWindow.ShowDialog() == true && encodingWindow.Result != null)
                {
                   var winCfg = encodingWindow.Result; // PipeWiseClient.Windows.CategoricalEncodingConfig

                   // Map Windows.CategoricalEncodingConfig -> PipeWiseClient.Models.CategoricalEncodingConfig
                   var mapped = new PipeWiseClient.Models.CategoricalEncodingConfig
                   {
                       Field = winCfg.Field,
                       Mapping = new Dictionary<string, int>(winCfg.Mapping),
                       TargetField = winCfg.TargetField,
                       ReplaceOriginal = winCfg.ReplaceOriginal,
                       DeleteOriginal = winCfg.DeleteOriginal,
                       DefaultValue = winCfg.DefaultValue
                   };

                   // Save mapped config into column settings
                   var settings = _columnSettings[fieldName];
                   settings.CategoricalEncoding = mapped;

                   AddSuccessNotification("????? ?????????",
                       $"????? ????????? ????? ???? ??? '{fieldName}' ?? {mapped.Mapping.Count} ?????");
               }
            }
            catch (Exception ex)
            {
                AddErrorNotification("????? ?????? ?????????", 
                    "?? ???? ????? ???? ????? ?????????", ex.Message);
            }
        }
    }
}
