using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using PipeWiseClient.Interfaces;
using PipeWiseClient.Models;

namespace PipeWiseClient.Services.ColumnOperations
{
    public class AggregationOperationStrategy : IColumnOperationStrategy
    {
        private readonly string _op;
        private readonly string _display;
        private readonly string _desc;
        public AggregationOperationStrategy(string operationName, string displayName, string description)
        { _op = operationName; _display = displayName; _desc = description; }

        public string OperationName => _op;
        public string DisplayName => _display;
        public string Description => _desc;
        public string Category => "Aggregation";
        public bool RequiresConfiguration => false;

        public Task<bool> ConfigureAsync(string columnName, ColumnSettings settings, Window owner, System.Collections.Generic.IReadOnlyList<string> availableColumns, string filePath) => Task.FromResult(true);

        public Dictionary<string, object> BuildServerConfig(string columnName, ColumnSettings settings) => new()
        {
            ["action"] = _op,
            ["column"] = columnName
        };
    }
}
