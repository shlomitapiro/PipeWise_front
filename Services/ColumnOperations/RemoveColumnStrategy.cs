using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using PipeWiseClient.Interfaces;
using PipeWiseClient.Models;

namespace PipeWiseClient.Services.ColumnOperations
{
    public class RemoveColumnStrategy : IColumnOperationStrategy
    {
        public string OperationName => "remove_column";
        public string DisplayName => "מחיקת עמודה";
        public string Description => "מסיר את העמודה מהתוצאה";
        public string Category => "Cleaning";
        public bool RequiresConfiguration => false;

        public Task<bool> ConfigureAsync(string columnName, ColumnSettings settings, Window owner, System.Collections.Generic.IReadOnlyList<string> availableColumns, string filePath)
        {
            settings.RemoveColumn = true;
            return Task.FromResult(true);
        }

        public Dictionary<string, object> BuildServerConfig(string columnName, ColumnSettings settings)
        {
            return new Dictionary<string, object>
            {
                ["action"] = "remove_column",
                ["fields"] = new[] { columnName } 
            };
        }
    }
}
