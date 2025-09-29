using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using PipeWiseClient.Interfaces;
using PipeWiseClient.Models;
using PipeWiseClient.Windows;

namespace PipeWiseClient.Services.ColumnOperations
{
    public class ReplaceEmptyValuesStrategy : IColumnOperationStrategy
    {
        public string OperationName => "replace_empty_values";
        public string DisplayName => "החלפת ערכים ריקים";
        public string Description => "מחליף ערכים ריקים בערך שנבחר";
        public string Category => "Cleaning";
        public bool RequiresConfiguration => true;

        private readonly IDialogFactory _dialogs;
        public ReplaceEmptyValuesStrategy(IDialogFactory dialogs) { _dialogs = dialogs; }

        public async Task<bool> ConfigureAsync(string columnName, ColumnSettings settings, Window owner, System.Collections.Generic.IReadOnlyList<string> availableColumns, string filePath)
        {
            _dialogs.SetOwner(owner);
            var inferredType = settings.InferredType ?? "string";
            var dlg = _dialogs.CreateValuePromptDialog(columnName, inferredType, 255);
            var result = dlg.ShowDialog() == true;
            if (!result) return false;
            settings.ReplaceEmpty ??= new ReplaceEmptySettings();
            settings.ReplaceEmpty.Value = dlg.ReplacementValue;
            settings.ReplaceEmpty.MaxLength = dlg.MaxLength;
            return await Task.FromResult(true);
        }

        public Dictionary<string, object> BuildServerConfig(string columnName, ColumnSettings settings)
        {
            if (settings.ReplaceEmpty == null)
                throw new System.InvalidOperationException("ReplaceEmpty not configured");

            return new Dictionary<string, object>
            {
                ["action"] = "replace_empty_values",
                ["field"] = columnName,
                ["replacement_value"] = settings.ReplaceEmpty.Value ?? string.Empty,
                ["expected_type"] = string.IsNullOrWhiteSpace(settings.InferredType) ? "string" : settings.InferredType.ToLowerInvariant(),
                ["max_length"] = settings.ReplaceEmpty.MaxLength <= 0 ? 255 : settings.ReplaceEmpty.MaxLength
            };
        }
    }
}
