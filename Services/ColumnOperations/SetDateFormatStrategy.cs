using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using PipeWiseClient.Interfaces;
using PipeWiseClient.Models;
using PipeWiseClient.Windows;

namespace PipeWiseClient.Services.ColumnOperations
{
    public class SetDateFormatStrategy : IColumnOperationStrategy
    {
        public string OperationName => "set_date_format";
        public string DisplayName => "קביעת פורמט תאריך";
        public string Description => "המרת פורמט תאריך לפורמט יעד";
        public string Category => "Validation";
        public bool RequiresConfiguration => true;

        private static readonly string[] DATE_INPUT_FORMATS = new[]
        {
            "%d-%m-%Y", "%d/%m/%Y", "%d.%m.%Y",
            "%Y-%m-%d", "%Y/%m/%d", "%m/%d/%Y", "%m-%d-%Y",
            "%d-%m-%Y %H:%M:%S", "%d/%m/%Y %H:%M:%S", "%d.%m.%Y %H:%M:%S",
            "%Y-%m-%d %H:%M:%S", "%Y-%m-%dT%H:%M:%S", "%m/%d/%Y %H:%M:%S",
            "%d-%m-%y", "%d/%m/%y", "%d.%m.%y",
            "%y-%m-%d", "%y/%m/%d", "%m/%d/%y",
            "%d-%m-%y %H:%M:%S", "%d/%m/%y %H:%M:%S", "%y-%m-%d %H:%M:%S",
        };

        private readonly IDialogFactory _dialogs;
        public SetDateFormatStrategy(IDialogFactory dialogs) { _dialogs = dialogs; }

        public async Task<bool> ConfigureAsync(string columnName, ColumnSettings settings, Window owner, System.Collections.Generic.IReadOnlyList<string> availableColumns, string filePath)
        {
            _dialogs.SetOwner(owner);
            var looksLikeDate = IsTypeSupportedForDateFormat(settings.InferredType);
            var dlg = _dialogs.CreateDateFormatDialog(columnName, looksLikeDate);
            var ok = dlg.ShowDialog() == true;
            if (!ok || string.IsNullOrWhiteSpace(dlg.SelectedPythonFormat)) return false;
            settings.DateFormatApply ??= new DateFormatApplySettings();
            settings.DateFormatApply.TargetFormat = dlg.SelectedPythonFormat!;
            return await Task.FromResult(true);
        }

        public Dictionary<string, object> BuildServerConfig(string columnName, ColumnSettings settings)
        {
            var fmt = settings.DateFormatApply?.TargetFormat;
            if (string.IsNullOrWhiteSpace(fmt))
                throw new System.InvalidOperationException("Date format not configured");

            var d = new Dictionary<string, object>
            {
                ["action"] = "set_date_format",
                ["field"] = columnName,
                ["input_formats"] = DATE_INPUT_FORMATS,
                ["target_format"] = fmt,
                ["action_on_violation"] = "warn"
            };
            return d;
        }

        private static bool IsTypeSupportedForDateFormat(string? inferred)
        {
            if (string.IsNullOrWhiteSpace(inferred)) return true;
            var t = inferred.ToLowerInvariant();
            return t.Contains("date") || t.Contains("time") || t.Contains("timestamp")
                || t.Contains("string") || t.Contains("text") || t.Contains("mixed");
        }
    }
}
