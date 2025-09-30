using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using PipeWiseClient.Interfaces;
using PipeWiseClient.Models;
using PipeWiseClient.Windows;

namespace PipeWiseClient.Services.ColumnOperations
{
    public class RenameFieldStrategy : IColumnOperationStrategy
    {
        public string OperationName => "rename_field";
        public string DisplayName => "שינוי שם";
        public string Description => "שינוי שם עמודה";
        public string Category => "Transform";
        public bool RequiresConfiguration => true;

        private readonly IDialogFactory _dialogs;
        public RenameFieldStrategy(IDialogFactory dialogs) { _dialogs = dialogs; }

        public async Task<bool> ConfigureAsync(string columnName, ColumnSettings settings, Window owner, System.Collections.Generic.IReadOnlyList<string> availableColumns, string filePath)
        {
            _dialogs.SetOwner(owner);
            var dlg = _dialogs.CreateRenameColumnDialog(columnName, new System.Collections.Generic.List<string>(availableColumns));
            var ok = dlg.ShowDialog() == true && !string.IsNullOrWhiteSpace(dlg.NewName);
            if (!ok) return false;
            settings.RenameSettings ??= new RenameSettings();
            settings.RenameSettings.NewName = dlg.NewName.Trim();
            return await Task.FromResult(true);
        }

        public Dictionary<string, object> BuildServerConfig(string columnName, ColumnSettings settings)
        {
            var s = settings.RenameSettings;
            if (s == null || string.IsNullOrWhiteSpace(s.NewName))
                throw new System.InvalidOperationException("Rename not configured");
            return new Dictionary<string, object>
            {
                ["action"] = "rename_field",
                ["field"] = columnName,
                ["old_name"] = columnName,
                ["new_name"] = s.NewName
            };
        }
    }
}
