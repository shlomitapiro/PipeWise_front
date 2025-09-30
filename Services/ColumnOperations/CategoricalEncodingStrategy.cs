using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using PipeWiseClient.Interfaces;
using PipeWiseClient.Models;
using PipeWiseClient.Windows;

namespace PipeWiseClient.Services.ColumnOperations
{
    public class CategoricalEncodingStrategy : IColumnOperationStrategy
    {
        public string OperationName => "categorical_encoding";
        public string DisplayName => "קידוד קטגוריאלי";
        public string Description => "מפה ערכים קטגוריאליים למספרים";
        public string Category => "Transform";
        public bool RequiresConfiguration => true;

        private readonly IDialogFactory _dialogs;
        private readonly IApiClient _api;
        public CategoricalEncodingStrategy(IDialogFactory dialogs, IApiClient api)
        {
            _dialogs = dialogs; _api = api;
        }

        public async Task<bool> ConfigureAsync(string columnName, ColumnSettings settings, Window owner, System.Collections.Generic.IReadOnlyList<string> availableColumns, string filePath)
        {
            _dialogs.SetOwner(owner);
            var fp = filePath?.Trim();
            if (string.IsNullOrWhiteSpace(fp) || !System.IO.File.Exists(fp))
                return false;
            var dlg = _dialogs.CreateCategoricalEncodingWindow(_api, fp!, columnName);
            if (dlg.ShowDialog() == true && dlg.Result != null)
            {
                settings.CategoricalEncoding = new PipeWiseClient.Models.CategoricalEncodingConfig
                {
                    Field = dlg.Result.Field,
                    Mapping = new Dictionary<string, int>(dlg.Result.Mapping),
                    ReplaceOriginal = dlg.Result.ReplaceOriginal,
                    DeleteOriginal = dlg.Result.DeleteOriginal,
                    DefaultValue = dlg.Result.DefaultValue,
                    TargetField = dlg.Result.TargetField
                };
                return await Task.FromResult(true);
            }
            return false;
        }

        public Dictionary<string, object> BuildServerConfig(string columnName, ColumnSettings settings)
        {
            var c = settings.CategoricalEncoding;
            if (c == null || c.Mapping.Count == 0)
                throw new System.InvalidOperationException("Categorical encoding not configured");
            var d = new Dictionary<string, object>
            {
                ["action"] = "categorical_encoding",
                ["field"] = columnName,
                ["mapping"] = c.Mapping,
                ["replace_original"] = c.ReplaceOriginal,
                ["delete_original"] = c.DeleteOriginal,
                ["default_value"] = c.DefaultValue
            };
            if (!c.ReplaceOriginal && !string.IsNullOrWhiteSpace(c.TargetField))
                d["target_field"] = c.TargetField!;
            return d;
        }
    }
}
