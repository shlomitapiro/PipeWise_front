using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using PipeWiseClient.Interfaces;
using PipeWiseClient.Models;
using PipeWiseClient.Helpers;

namespace PipeWiseClient.Services.ColumnOperations
{
    public class NormalizeNumericStrategy : IColumnOperationStrategy
    {
        public string OperationName => "normalize_numeric";
        public string DisplayName => "נרמול מספרי";
        public string Description => "יוצר שדה מנורמל מערכים מספריים";
        public string Category => "Transform";
        public bool RequiresConfiguration => true;

        public Task<bool> ConfigureAsync(string columnName, ColumnSettings settings, Window owner, System.Collections.Generic.IReadOnlyList<string> availableColumns, string filePath)
        {
            var targetField = InputDialogs.ShowSingleValueDialog("נרמול מספרי", $"שם השדה המטרה עבור '{columnName}':", $"{columnName}_normalized");
            if (string.IsNullOrWhiteSpace(targetField)) return Task.FromResult(false as bool? ?? false);
            settings.NormalizeSettings ??= new NormalizeSettings();
            settings.NormalizeSettings.TargetField = targetField.Trim();
            return Task.FromResult(true);
        }

        public Dictionary<string, object> BuildServerConfig(string columnName, ColumnSettings settings)
        {
            var d = new Dictionary<string, object>
            {
                ["action"] = "normalize_numeric",
                ["field"] = columnName
            };
            if (!string.IsNullOrWhiteSpace(settings.NormalizeSettings?.TargetField))
                d["target_field"] = settings.NormalizeSettings.TargetField!;
            return d;
        }
    }
}
