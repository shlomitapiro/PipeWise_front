using System.Collections.Generic;
using System.Linq;
using PipeWiseClient.Interfaces;

namespace PipeWiseClient.Services
{
    public class ColumnOperationRegistry
    {
        private readonly Dictionary<string, IColumnOperationStrategy> _strategies = new(System.StringComparer.OrdinalIgnoreCase);

        public void Register(IColumnOperationStrategy strategy)
        {
            if (strategy == null || string.IsNullOrWhiteSpace(strategy.OperationName)) return;
            _strategies[strategy.OperationName] = strategy;
        }

        public IColumnOperationStrategy? GetStrategy(string operationName)
        {
            if (string.IsNullOrWhiteSpace(operationName)) return null;
            return _strategies.TryGetValue(operationName, out var s) ? s : null;
        }

        public IEnumerable<IColumnOperationStrategy> GetAll() => _strategies.Values;

        public IEnumerable<IColumnOperationStrategy> GetByCategory(string category)
            => _strategies.Values.Where(s => string.Equals(s.Category, category, System.StringComparison.OrdinalIgnoreCase));

        public bool IsRegistered(string operationName) => _strategies.ContainsKey(operationName);
    }
}

