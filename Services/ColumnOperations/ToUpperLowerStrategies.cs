using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using PipeWiseClient.Interfaces;
using PipeWiseClient.Models;

namespace PipeWiseClient.Services.ColumnOperations
{
    public class ToUppercaseStrategy : IColumnOperationStrategy
    {
        public string OperationName => "to_uppercase";
        public string DisplayName => "לאותיות גדולות";
        public string Description => "המרת טקסט לאותיות גדולות";
        public string Category => "Transform";
        public bool RequiresConfiguration => false;
        public Task<bool> ConfigureAsync(string columnName, ColumnSettings settings, Window owner, System.Collections.Generic.IReadOnlyList<string> availableColumns, string filePath) => Task.FromResult(true);
        public Dictionary<string, object> BuildServerConfig(string columnName, ColumnSettings settings) => new()
        {
            ["action"] = "to_uppercase",
            ["fields"] = new[] { columnName }
        };
    }

    public class ToLowercaseStrategy : IColumnOperationStrategy
    {
        public string OperationName => "to_lowercase";
        public string DisplayName => "לאותיות קטנות";
        public string Description => "המרת טקסט לאותיות קטנות";
        public string Category => "Transform";
        public bool RequiresConfiguration => false;
        public Task<bool> ConfigureAsync(string columnName, ColumnSettings settings, Window owner, System.Collections.Generic.IReadOnlyList<string> availableColumns, string filePath) => Task.FromResult(true);
        public Dictionary<string, object> BuildServerConfig(string columnName, ColumnSettings settings) => new()
        {
            ["action"] = "to_lowercase",
            ["fields"] = new[] { columnName }
        };
    }
}
