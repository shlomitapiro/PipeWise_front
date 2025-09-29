using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using PipeWiseClient.Interfaces;
using PipeWiseClient.Models;
using PipeWiseClient.Windows;

namespace PipeWiseClient.Services.ColumnOperations
{
    public class SetNumericRangeStrategy : IColumnOperationStrategy
    {
        public string OperationName => "set_numeric_range";
        public string DisplayName => "הגדרת טווח מספרי";
        public string Description => "בדיקת ערך בתחום מוגדר והחלפה/הסרה במקרה חריג";
        public string Category => "Validation";
        public bool RequiresConfiguration => true;

        private readonly IDialogFactory _dialogs;
        public SetNumericRangeStrategy(IDialogFactory dialogs) { _dialogs = dialogs; }

        public async Task<bool> ConfigureAsync(string columnName, ColumnSettings settings, Window owner, System.Collections.Generic.IReadOnlyList<string> availableColumns, string filePath)
        {
            _dialogs.SetOwner(owner);
            var dlg = _dialogs.CreateNumericRangeDialog(columnName);
            var ok = dlg.ShowDialog() == true;
            if (!ok) return false;
            settings.NumericRange ??= new NumericRangeSettings();
            settings.NumericRange.Min = dlg.MinValue;
            settings.NumericRange.Max = dlg.MaxValue;
            settings.NumericRange.ActionOnViolation = dlg.ActionOnViolation;
            settings.NumericRange.ReplacementValue = dlg.ReplacementValue;
            return await Task.FromResult(true);
        }

        public Dictionary<string, object> BuildServerConfig(string columnName, ColumnSettings settings)
        {
            if (settings.NumericRange == null)
                throw new System.InvalidOperationException("Numeric range not configured");
            var d = new Dictionary<string, object>
            {
                ["action"] = "set_numeric_range",
                ["field"] = columnName,
                ["action_on_violation"] = string.IsNullOrWhiteSpace(settings.NumericRange.ActionOnViolation) ? "remove" : settings.NumericRange.ActionOnViolation
            };
            if (settings.NumericRange.Min.HasValue) d["min_value"] = settings.NumericRange.Min.Value;
            if (settings.NumericRange.Max.HasValue) d["max_value"] = settings.NumericRange.Max.Value;
            if (string.Equals(settings.NumericRange.ActionOnViolation, "replace", System.StringComparison.OrdinalIgnoreCase) && settings.NumericRange.ReplacementValue.HasValue)
                d["replacement_value"] = settings.NumericRange.ReplacementValue.Value;
            return d;
        }
    }
}
