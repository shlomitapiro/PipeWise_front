using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using PipeWiseClient.Interfaces;
using PipeWiseClient.Models;
using PipeWiseClient.Windows;

namespace PipeWiseClient.Services.ColumnOperations
{
    public class SplitFieldStrategy : IColumnOperationStrategy
    {
        public string OperationName => "split_field";
        public string DisplayName => "פיצול שדה";
        public string Description => "פיצול שדה לשדות מרובים לפי מפריד או אורך";
        public string Category => "Transform";
        public bool RequiresConfiguration => true;

        private readonly IDialogFactory _dialogs;
        public SplitFieldStrategy(IDialogFactory dialogs) { _dialogs = dialogs; }

        public async Task<bool> ConfigureAsync(string columnName, ColumnSettings settings, Window owner, System.Collections.Generic.IReadOnlyList<string> availableColumns, string filePath)
        {
            _dialogs.SetOwner(owner);
            var dlg = _dialogs.CreateSplitFieldWindow(columnName, new System.Collections.Generic.List<string>(availableColumns));
            var ok = dlg.ShowDialog() == true && dlg.Result != null && dlg.Result.TargetFields != null && dlg.Result.TargetFields.Count > 0;
            if (!ok) return false;
            settings.SplitFieldSettings = new SplitFieldSettings
            {
                SplitType = dlg.Result!.SplitType,
                Delimiter = dlg.Result!.Delimiter,
                Length = dlg.Result!.Length,
                TargetFields = dlg.Result!.TargetFields?.ToList() ?? new System.Collections.Generic.List<string>(),
                RemoveSource = dlg.Result!.RemoveSource
            };
            return await Task.FromResult(true);
        }

        public Dictionary<string, object> BuildServerConfig(string columnName, ColumnSettings settings)
        {
            var s = settings.SplitFieldSettings;
            if (s == null || s.TargetFields == null || s.TargetFields.Count == 0)
                throw new System.InvalidOperationException("Split field not configured");
            var d = new Dictionary<string, object>
            {
                ["action"] = "split_field",
                ["source_field"] = columnName,
                ["split_type"] = s.SplitType,
                ["target_fields"] = s.TargetFields.ToArray(),
                ["remove_source"] = s.RemoveSource
            };
            if (s.SplitType == "delimiter") d["delimiter"] = s.Delimiter;
            else if (s.SplitType == "fixed_length") d["length"] = s.Length;
            return d;
        }
    }
}
