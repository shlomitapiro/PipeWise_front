using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using PipeWiseClient.Interfaces;
using PipeWiseClient.Models;
using PipeWiseClient.Windows;

namespace PipeWiseClient.Services.ColumnOperations
{
    public class MergeColumnsStrategy : IColumnOperationStrategy
    {
        public string OperationName => "merge_columns";
        public string DisplayName => "מיזוג עמודות";
        public string Description => "מיזוג מספר עמודות לעמודה אחת";
        public string Category => "Transform";
        public bool RequiresConfiguration => true;

        private readonly IDialogFactory _dialogs;
        public MergeColumnsStrategy(IDialogFactory dialogs) { _dialogs = dialogs; }

        public async Task<bool> ConfigureAsync(string columnName, ColumnSettings settings, Window owner, System.Collections.Generic.IReadOnlyList<string> availableColumns, string filePath)
        {
            _dialogs.SetOwner(owner);
            var dlg = _dialogs.CreateMergeColumnsDialog(new System.Collections.Generic.List<string>(availableColumns), columnName);
            var ok = dlg.ShowDialog() == true && dlg.SelectedColumns != null && dlg.SelectedColumns.Count > 0;
            if (!ok) return false;
            var sources = new List<string> { columnName };
            if (dlg.SelectedColumns != null && dlg.SelectedColumns.Count > 0) sources.AddRange(dlg.SelectedColumns);
            settings.MergeColumnsSettings = new MergeColumnsSettings
            {
                SourceColumns = sources.Distinct(System.StringComparer.OrdinalIgnoreCase).ToList(),
                TargetColumn = dlg.TargetColumn,
                Separator = dlg.Separator,
                RemoveSourceColumns = dlg.RemoveSourceColumns,
                EmptyHandling = dlg.EmptyHandling,
                EmptyReplacement = dlg.EmptyReplacement
            };
            return await Task.FromResult(true);
        }

        public Dictionary<string, object> BuildServerConfig(string columnName, ColumnSettings settings)
        {
            var s = settings.MergeColumnsSettings;
            if (s == null || s.SourceColumns.Count < 1 || string.IsNullOrWhiteSpace(s.TargetColumn))
                throw new System.InvalidOperationException("Merge not configured");
            return new Dictionary<string, object>
            {
                ["action"] = "merge_columns",
                ["source_columns"] = s.SourceColumns.ToArray(),
                ["target_column"] = s.TargetColumn,
                ["separator"] = s.Separator,
                ["remove_source"] = s.RemoveSourceColumns,
                ["handle_empty"] = s.EmptyHandling,
                ["empty_replacement"] = s.EmptyReplacement
            };
        }
    }
}
