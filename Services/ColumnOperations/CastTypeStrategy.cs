using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using PipeWiseClient.Interfaces;
using PipeWiseClient.Models;
using PipeWiseClient.Helpers;

namespace PipeWiseClient.Services.ColumnOperations
{
    public class CastTypeStrategy : IColumnOperationStrategy
    {
        public string OperationName => "cast_type";
        public string DisplayName => "המרת טיפוס";
        public string Description => "המרת טיפוס שדה לסוג אחר";
        public string Category => "Transform";
        public bool RequiresConfiguration => true;

        public Task<bool> ConfigureAsync(string columnName, ColumnSettings settings, Window owner, System.Collections.Generic.IReadOnlyList<string> availableColumns, string filePath)
        {
            var typeOptions = new[] { "int", "float", "str", "bool" };
            var selectedType = InputDialogs.ShowSelectionDialog("בחירת טיפוס יעד", $"בחר טיפוס יעד עבור '{columnName}':", typeOptions);
            if (string.IsNullOrWhiteSpace(selectedType)) return Task.FromResult(false);
            settings.CastType ??= new CastTypeSettings();
            settings.CastType.ToType = selectedType;
            return Task.FromResult(true);
        }

        public Dictionary<string, object> BuildServerConfig(string columnName, ColumnSettings settings)
        {
            if (settings.CastType == null || string.IsNullOrWhiteSpace(settings.CastType.ToType))
                throw new System.InvalidOperationException("Cast type not configured");
            return new Dictionary<string, object>
            {
                ["action"] = "cast_type",
                ["field"] = columnName,
                ["to_type"] = settings.CastType.ToType!
            };
        }
    }
}
