using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using PipeWiseClient.Interfaces;
using PipeWiseClient.Models;
using PipeWiseClient.Windows;

namespace PipeWiseClient.Services.ColumnOperations
{
    public class ReplaceNullValuesStrategy : IColumnOperationStrategy
    {
        public string OperationName => "replace_null_values";
        public string DisplayName => "החלפת NULL";
        public string Description => "מחליף ערכי NULL בערך שנבחר";
        public string Category => "Cleaning";
        public bool RequiresConfiguration => true;

        private readonly IDialogFactory _dialogs;
        public ReplaceNullValuesStrategy(IDialogFactory dialogs) { _dialogs = dialogs; }

        public async Task<bool> ConfigureAsync(string columnName, ColumnSettings settings, Window owner, System.Collections.Generic.IReadOnlyList<string> availableColumns, string filePath)
        {
            _dialogs.SetOwner(owner);
            var inferredType = settings.InferredType ?? "string";
            var dlg = _dialogs.CreateValuePromptDialog(columnName, inferredType, 255);
            var ok = dlg.ShowDialog() == true;
            if (!ok) return false;
            settings.ReplaceNull ??= new ReplaceEmptySettings();
            settings.ReplaceNull.Value = dlg.ReplacementValue;
            settings.ReplaceNull.MaxLength = dlg.MaxLength;
            return await Task.FromResult(true);
        }

        public Dictionary<string, object> BuildServerConfig(string columnName, ColumnSettings settings)
        {
            if (settings.ReplaceNull == null)
                throw new System.InvalidOperationException("ReplaceNull not configured");

            return new Dictionary<string, object>
            {
                ["action"] = "replace_null_values",
                ["field"] = columnName,
                ["replacement_value"] = settings.ReplaceNull.Value ?? string.Empty,
                ["expected_type"] = string.IsNullOrWhiteSpace(settings.InferredType) ? "string" : settings.InferredType.ToLowerInvariant(),
                ["max_length"] = settings.ReplaceNull.MaxLength <= 0 ? 255 : settings.ReplaceNull.MaxLength,
                ["null_definitions"] = new[] { "null", "n/a", "none", "undefined", "UNDEFINED", "NULL", "N/A", "NONE" }
            };
        }
    }
}
