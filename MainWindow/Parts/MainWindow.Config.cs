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
                var sourcePath = cfg.Source?.Path;
                if (!string.IsNullOrWhiteSpace(sourcePath) && File.Exists(sourcePath))
                {
                    FilePathTextBox!.Text = sourcePath;
                    await LoadFileColumns(sourcePath);
                }

                if (!string.IsNullOrWhiteSpace(cfg.Target?.Type))
                    SelectTargetTypeInUi(cfg.Target.Type);

                var globalActions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var perColumnOps = new List<(string column, string action)>();

                foreach (var p in cfg.Processors ?? Array.Empty<ProcessorConfig>())
                {
                    if (!p.Config.TryGetValue("operations", out var opsObj) || opsObj == null) continue;

                    if (opsObj is JArray jarr)
                    {
                        foreach (var tok in jarr.OfType<JObject>())
                        {
                            var action = (string?)tok["action"];
                            // נסה column, ואם אין – field, ואם אין – הראשון מתוך fields
                            var column = (string?)tok["column"];
                            column ??= (string?)tok["field"];
                            if (column == null && tok["fields"] is JArray farr && farr.First is JValue v && v.Type == JTokenType.String)
                                column = (string?)v;
                            if (string.IsNullOrWhiteSpace(action)) continue;

                            if (string.IsNullOrWhiteSpace(column))
                                globalActions.Add(action);
                            else
                                perColumnOps.Add((column, action));
                        }
                    }
                }

                if (RemoveEmptyRowsCheckBox != null)
                    RemoveEmptyRowsCheckBox.IsChecked = globalActions.Contains("remove_empty_rows");
                if (RemoveDuplicatesCheckBox != null)
                    RemoveDuplicatesCheckBox.IsChecked = globalActions.Contains("remove_duplicates");
                if (StripWhitespaceCheckBox != null)
                    StripWhitespaceCheckBox.IsChecked = globalActions.Contains("strip_whitespace");

                if (ColumnsPanel != null && ColumnsPanel.Children.Count > 0 && perColumnOps.Count > 0)
                {
                    foreach (var (column, action) in perColumnOps)
                    {
                        var tag = $"{column}:{action}";
                        var cb = FindCheckBoxByTag(ColumnsPanel, tag);
                        if (cb != null) cb.IsChecked = true;
                    }
                }
                else if (perColumnOps.Count > 0 && !string.IsNullOrWhiteSpace(sourcePath) && !File.Exists(sourcePath))
                {
                    AddWarningNotification("קובץ מקור לא נטען",
                        "זוהו פעולות לפי עמודות, אך הקובץ ב-source.path לא נמצא. בחר קובץ נתונים זהה לזה שבקונפיג כדי לסמן אוטומטית.");
                }
            }
            finally
            {
                _isApplyingConfig = false;
            }
        }


        private void EnsureSafeTargetPath(PipelineConfig cfg, string dataFilePath)
        {
            Directory.CreateDirectory(OUTPUT_DIR);

            var baseName = string.IsNullOrWhiteSpace(dataFilePath)
                ? "output"
                : Path.GetFileNameWithoutExtension(dataFilePath);

            // לפי בחירת המשתמש
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
                var json = File.ReadAllText(filePath, Encoding.UTF8);
                cfg = Newtonsoft.Json.JsonConvert.DeserializeObject<PipelineConfig>(json);
                if (cfg == null) { error = "קובץ קונפיג לא תקין."; return false; }
                if (cfg.Source == null || cfg.Target == null) { error = "חסרים source/target בקונפיג."; return false; }
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
