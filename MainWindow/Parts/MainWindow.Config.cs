// PipeWise_Client/MainWindow/Parts/MainWindow.Config.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PipeWiseClient.Models;

namespace PipeWiseClient
{
    public partial class MainWindow
    {
        private List<(string column, string action)> _pendingOperationsToApply = new List<(string, string)>();
        private PipeWiseClient.Models.CompatResult LocalValidateCompatibility(PipelineConfig cfg, string filePath, List<string> detectedColumns)
        {
            var result = new PipeWiseClient.Models.CompatResult();

            try
            {
                var requiredCols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var p in cfg.Processors ?? Array.Empty<ProcessorConfig>())
                {
                    if (!p.Config.TryGetValue("operations", out var opsObj) || opsObj == null)
                        continue;

                    if (opsObj is Newtonsoft.Json.Linq.JArray jarr)
                    {
                        foreach (var tok in jarr.OfType<Newtonsoft.Json.Linq.JObject>())
                        {
                            string? col = (string?)tok["column"] ?? (string?)tok["field"];
                            if (col == null && tok["fields"] is JArray farr && farr.First is JValue v && v.Type == JTokenType.String)
                                col = (string?)v;

                            if (!string.IsNullOrWhiteSpace(col))
                                requiredCols.Add(col);
                        }
                    }
                    else if (opsObj is System.Text.Json.Nodes.JsonArray sArr)
                    {
                        foreach (var node in sArr)
                        {
                            var col = node?["column"]?.GetValue<string>();
                            if (!string.IsNullOrWhiteSpace(col))
                                requiredCols.Add(col);
                        }
                    }
                    else if (opsObj is IEnumerable<object> plainList)
                    {
                        foreach (var item in plainList)
                        {
                            var dict = item as Dictionary<string, object>;
                            if (dict != null && dict.TryGetValue("column", out var cObj) && cObj is string c && !string.IsNullOrWhiteSpace(c))
                                requiredCols.Add(c);
                        }
                    }
                }

                var colsLower = new HashSet<string>(detectedColumns.Select(s => s.Trim()), StringComparer.OrdinalIgnoreCase);
                var missing = requiredCols.Where(rc => !colsLower.Contains(rc.Trim())).ToList();
                if (missing.Any())
                {
                    result.IsCompatible = false;
                    result.Issues.Add(Issue($"עמודות חסרות בקובץ: {string.Join(", ", missing)}"));
                }

                var numericOps = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "sum", "average", "min", "max", "median", "std", "variance", "range", "count_valid", "count_distinct", "most_common" };
                foreach (var p in cfg.Processors ?? Array.Empty<ProcessorConfig>())
                {
                    if (!p.Config.TryGetValue("operations", out var opsObj) || opsObj == null) continue;

                    IEnumerable<(string action, string? column)> EnumerateOps()
                    {
                        if (opsObj is Newtonsoft.Json.Linq.JArray jarr)
                        {
                            foreach (var tok in jarr.OfType<Newtonsoft.Json.Linq.JObject>())
                                yield return (((string?)tok["action"]) ?? "", (string?)tok["column"]);
                        }
                        else if (opsObj is System.Text.Json.Nodes.JsonArray sArr)
                        {
                            foreach (var node in sArr)
                                yield return ((node?["action"]?.GetValue<string>()) ?? "", node?["column"]?.GetValue<string>());
                        }
                        else if (opsObj is IEnumerable<object> plainList)
                        {
                            foreach (var item in plainList)
                            {
                                var dict = item as Dictionary<string, object>;
                                var action = dict != null && dict.TryGetValue("action", out var aObj) ? aObj?.ToString() ?? "" : "";
                                var col = dict != null && dict.TryGetValue("column", out var cObj) ? cObj as string : null;
                                yield return (action, col);
                            }
                        }
                    }

                    foreach (var (action, col) in EnumerateOps())
                    {
                        if (string.IsNullOrWhiteSpace(action) || string.IsNullOrWhiteSpace(col)) continue;

                        if (numericOps.Contains(action) && colsLower.Contains(col))
                        {
                            var hint = col!.ToLowerInvariant();
                            if (!(hint.Contains("price") || hint.Contains("qty") || hint.Contains("quantity") || hint.Contains("total") || hint.Contains("amount") || hint.Contains("count")))
                            {
                                result.Issues.Add(Issue($"בדיקה: הפעולה '{action}' על '{col}' נראית מספרית — ודא שהעמודה מספרית."));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                result.IsCompatible = false;
                result.Issues.Add(Issue("שגיאה בבדיקת התאימות: " + ex.Message));
            }

            return result;
        }
        private async Task ApplyConfigToUI(PipelineConfig cfg)
        {
            _isApplyingConfig = true;

            try
            {
                _pendingOperationsToApply ??= new List<(string, string)>();
                _pendingOperationsToApply.Clear();

                var sourcePath = cfg.Source?.Path;
                if (!string.IsNullOrWhiteSpace(sourcePath) && File.Exists(sourcePath))
                {
                    FilePathTextBox!.Text = sourcePath;
                    await LoadFileColumns(sourcePath);
                }

                if (!string.IsNullOrWhiteSpace(cfg.Target?.Type))
                    SelectTargetTypeInUi(cfg.Target.Type);

                var globalActions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var perColumnOps  = new List<(string column, string action)>();

                foreach (var p in cfg.Processors ?? Array.Empty<ProcessorConfig>())
                {
                    if (p?.Config == null) continue;

                    object? opsObj = null;
                    if (!p.Config.TryGetValue("operations", out opsObj) || opsObj == null)
                        p.Config.TryGetValue("Operations", out opsObj);

                    if (opsObj == null) continue;

                    IEnumerable<object>? opsEnumerable = null;
                    if (opsObj is JArray jarr)
                    {
                        opsEnumerable = jarr;
                    }
                    else if (opsObj is IEnumerable<object> list)
                    {
                        opsEnumerable = list;
                    }

                    if (opsEnumerable == null) continue;

                    foreach (var item in opsEnumerable)
                    {
                        Dictionary<string, object>? dict =
                            item as Dictionary<string, object> ??
                            (NormalizeJToken(item) as Dictionary<string, object>);

                        if (dict == null) continue;

                        var action =
                            (dict.TryGetValue("action", out var a1) ? a1?.ToString() : null) ??
                            (dict.TryGetValue("Action", out var a2) ? a2?.ToString() : null);

                        if (string.IsNullOrWhiteSpace(action)) continue;

                        var columns = new List<string>();

                        string? firstCol =
                            (dict.TryGetValue("column", out var c1) ? c1?.ToString() : null) ??
                            (dict.TryGetValue("field", out var c2) ? c2?.ToString() : null) ??
                            (dict.TryGetValue("source_field", out var c3) ? c3?.ToString() : null);

                        if (!string.IsNullOrWhiteSpace(firstCol))
                        {
                            columns.Add(firstCol!);
                        }
                        else
                        {
                            if (dict.TryGetValue("fields", out var fObj) || dict.TryGetValue("Fields", out fObj))
                            {
                                if (fObj is JArray fj) fObj = NormalizeJToken(fj);

                                if (fObj is IEnumerable<object> fEnum)
                                {
                                    foreach (var f in fEnum)
                                    {
                                        var s = f?.ToString();
                                        if (!string.IsNullOrWhiteSpace(s))
                                            columns.Add(s!);
                                    }
                                }
                                else if (fObj is string fs && !string.IsNullOrWhiteSpace(fs))
                                {
                                    columns.Add(fs);
                                }
                            }
                        }

                        if (columns.Count == 0)
                        {
                            globalActions.Add(action);
                        }
                        else
                        {
                            foreach (var col in columns)
                                perColumnOps.Add((col, action));
                        }
                    }
                }

                if (RemoveEmptyRowsCheckBox != null)
                    RemoveEmptyRowsCheckBox.IsChecked = globalActions.Contains("remove_empty_rows");

                if (RemoveDuplicatesCheckBox != null)
                    RemoveDuplicatesCheckBox.IsChecked = globalActions.Contains("remove_duplicates");

                if (StripWhitespaceCheckBox != null)
                    StripWhitespaceCheckBox.IsChecked = globalActions.Contains("strip_whitespace");

                _pendingOperationsToApply.AddRange(perColumnOps);
                ApplyPendingOperations();
            }
            finally
            {
                _isApplyingConfig = false;
            }
        }

        private void ApplyPendingOperations()
        {
            if (_pendingOperationsToApply == null || _pendingOperationsToApply.Count == 0)
            {
                return;
            }

            if (ColumnsPanel?.Children.Count == 0)
            {
                return;
            }

            var appliedCount = 0;
            var pendingOperations = _pendingOperationsToApply.ToList();

            foreach (var (column, action) in pendingOperations)
            {
                var tag = $"{column}:{action}";
                var cb = FindCheckBoxByTag(ColumnsPanel, tag);
                if (cb != null)
                {
                    cb.IsChecked = true;
                    appliedCount++;
                }
                else
                {
                    AddInfoNotification("❌", $"לא נמצא: {tag}");
                }
            }

            if (appliedCount > 0)
            {
                _pendingOperationsToApply.RemoveAll(op =>
                    pendingOperations.Any(p => p.column == op.column && p.action == op.action &&
                                            FindCheckBoxByTag(ColumnsPanel, $"{op.column}:{op.action}") != null));
            }
        }

        private void EnsureSafeTargetPath(PipelineConfig cfg, string dataFilePath)
        {
            Directory.CreateDirectory(OUTPUT_DIR);

            var baseName = string.IsNullOrWhiteSpace(dataFilePath)
                ? "output"
                : Path.GetFileNameWithoutExtension(dataFilePath);

            var selectedTargetType = GetSelectedTargetType();
            var targetExt = ExtForTarget(selectedTargetType);

            var defaultType = selectedTargetType;
            var defaultPath = Path.Combine(OUTPUT_DIR, $"{baseName}_processed.{targetExt}");

            if (cfg.Target == null)
            {
                cfg.Target = new TargetConfig
                {
                    Type = defaultType,
                    Path = defaultPath
                };
                return;
            }

            if (string.IsNullOrWhiteSpace(cfg.Target.Type))
                cfg.Target.Type = defaultType;

            if (string.IsNullOrWhiteSpace(cfg.Target.Path))
                cfg.Target.Path = defaultPath;

            var effectiveType = string.IsNullOrWhiteSpace(cfg.Target.Type) ? selectedTargetType : cfg.Target.Type;
            var desiredExt = "." + ExtForTarget(effectiveType.ToLowerInvariant());

            if (!string.IsNullOrWhiteSpace(cfg.Target.Path))
            {
                var currentExt = Path.GetExtension(cfg.Target.Path);
                if (!string.Equals(currentExt, desiredExt, StringComparison.OrdinalIgnoreCase))
                    cfg.Target.Path = Path.ChangeExtension(cfg.Target.Path, desiredExt);
            }
        }

        private bool TryReadConfigFromJson(string filePath, out PipelineConfig? cfg, out string? error)
        {            
            try
            {
                if (!File.Exists(filePath))
                {
                    cfg = null;
                    error = "קובץ הקונפיגורציה לא קיים.";
                    return false;
                }

                var json = File.ReadAllText(filePath, Encoding.UTF8);
                
                cfg = Newtonsoft.Json.JsonConvert.DeserializeObject<PipelineConfig>(json);
                
                if (cfg == null) 
                { 
                    error = "קובץ קונפיג לא תקין."; 
                    return false; 
                }
                
                if (cfg.Source == null || cfg.Target == null) 
                { 
                    error = "חסרים source/target בקונפיג."; 
                    return false; 
                }
                
                error = null;
                return true;
            }
            catch (Exception ex)
            {
                cfg = null;
                error = ex.Message;
                return false;
            }
        }
    }
}
