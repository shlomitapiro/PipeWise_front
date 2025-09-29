using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using PipeWiseClient.Models;

namespace PipeWiseClient.Interfaces
{
    public interface IColumnOperationStrategy
    {
        string OperationName { get; }
        string DisplayName { get; }
        string Description { get; }
        string Category { get; }
        bool RequiresConfiguration { get; }

        Task<bool> ConfigureAsync(string columnName, ColumnSettings settings, Window owner, System.Collections.Generic.IReadOnlyList<string> availableColumns, string filePath);
        Dictionary<string, object> BuildServerConfig(string columnName, ColumnSettings settings);
    }
}
