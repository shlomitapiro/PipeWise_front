using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using OfficeOpenXml;
using PipeWiseClient.Interfaces;

namespace PipeWiseClient.Services
{
    public class FileService : IFileService
    {
        private readonly INotificationService _notifications;

        public FileService(INotificationService notifications)
        {
            _notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
        }

        public async Task<List<string>> DetectColumnsAsync(string filePath)
        {
            if (!File.Exists(filePath)) throw new FileNotFoundException("File not found", filePath);

            try
            {
                var ext = Path.GetExtension(filePath).ToLowerInvariant();
                return ext switch
                {
                    ".csv" => await DetectCsvColumnsAsync(filePath),
                    ".xlsx" or ".xls" => await DetectExcelColumnsAsync(filePath),
                    ".json" => await DetectJsonColumnsAsync(filePath),
                    ".xml" => await DetectXmlColumnsAsync(filePath),
                    _ => throw new NotSupportedException($"Unsupported file type: {ext}")
                };
            }
            catch (NotSupportedException) { throw; }
            catch (Exception ex)
            {
                _notifications.Error("שגיאת קריאת קובץ", $"לא ניתן לזהות עמודות מהקובץ", ex.Message);
                throw;
            }
        }

        public async Task<Dictionary<string, string>> DetectColumnTypesAsync(string filePath, List<string> columnNames)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var name in columnNames)
            {
                result[name] = InferTypeFromName(name);
            }
            await Task.CompletedTask;
            return result;
        }

        public string GetFileType(string filePath)
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            return ext switch
            {
                ".csv" => "csv",
                ".json" => "json",
                ".xml" => "xml",
                ".xlsx" or ".xls" => "excel",
                _ => "unknown"
            };
        }

        public bool IsFileSupported(string filePath)
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            return ext is ".csv" or ".json" or ".xlsx" or ".xls" or ".xml";
        }

        private async Task<List<string>> DetectCsvColumnsAsync(string filePath)
        {
            using var reader = new StreamReader(filePath);
            var headerLine = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(headerLine)) return new List<string>();
            return headerLine.Split(',')
                             .Select(col => col.Trim().Trim('\"'))
                             .Where(s => !string.IsNullOrWhiteSpace(s))
                             .ToList();
        }

        private async Task<List<string>> DetectExcelColumnsAsync(string filePath)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using var package = new ExcelPackage(new FileInfo(filePath));
            await package.LoadAsync(File.OpenRead(filePath));
            var ws = package.Workbook.Worksheets.FirstOrDefault();
            var list = new List<string>();
            if (ws?.Dimension == null) return list;
            for (int col = 1; col <= ws.Dimension.End.Column; col++)
            {
                var val = ws.Cells[1, col].Value?.ToString();
                if (!string.IsNullOrWhiteSpace(val)) list.Add(val);
            }
            return list;
        }

        private async Task<List<string>> DetectJsonColumnsAsync(string filePath)
        {
            var text = await File.ReadAllTextAsync(filePath);
            var cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var token = JToken.Parse(text);
                if (token is JArray arr)
                {
                    foreach (var obj in arr.OfType<JObject>())
                        foreach (var p in obj.Properties()) cols.Add(p.Name);
                }
                else if (token is JObject obj)
                {
                    foreach (var p in obj.Properties()) cols.Add(p.Name);
                    var firstArray = obj.Properties().Select(p => p.Value).OfType<JArray>().FirstOrDefault();
                    if (firstArray != null)
                    {
                        foreach (var rec in firstArray.OfType<JObject>())
                            foreach (var p in rec.Properties()) cols.Add(p.Name);
                    }
                }
            }
            catch { /* ignore parse issues, return what we have */ }
            return cols.ToList();
        }

        private async Task<List<string>> DetectXmlColumnsAsync(string filePath)
        {
            var text = await File.ReadAllTextAsync(filePath);
            var cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var doc = System.Xml.Linq.XDocument.Parse(text);

                // Heuristic 1: common record containers (prefer leaf children)
                var commonRecordTags = new[] { "record", "item", "row", "entry", "data", "records", "items", "rows" };
                foreach (var tag in commonRecordTags)
                {
                    var rec = doc
                        .Descendants(tag)
                        .FirstOrDefault(e => e.HasElements && e.Elements().Any() && e.Elements().All(c => !c.HasElements));
                    if (rec != null)
                    {
                        foreach (var el in rec.Elements()) cols.Add(el.Name.LocalName);
                        if (cols.Count > 0) return cols.ToList();
                    }
                }

                // Heuristic 2: pick an element whose children are leaf elements (avoid selecting the root collection like <records>)
                var candidates = doc.Descendants().Where(e => e.HasElements && e.Elements().Any()).ToList();
                var best = candidates
                    .Where(e => e.Elements().All(c => !c.HasElements))
                    .OrderByDescending(e => e.Elements().Count())
                    .FirstOrDefault();
                best ??= candidates.OrderByDescending(e => e.Elements().Count()).FirstOrDefault();

                if (best != null)
                {
                    foreach (var el in best.Elements()) cols.Add(el.Name.LocalName);
                }
            }
            catch { /* ignore parse issues, return what we have */ }
            return cols.ToList();
        }

        private static string InferTypeFromName(string columnName)
        {
            var n = columnName.ToLowerInvariant();
            if (n.Contains("date") || n.Contains("time") || n.Contains("created") || n.Contains("updated") || n.EndsWith("_at") || n.EndsWith("_on"))
                return "date";
            if (n.Contains("id") || n.Contains("count") || n.Contains("age") || n.Contains("quantity") || n.Contains("amount") || n.Contains("price") || n.Contains("total") || n.Contains("num"))
                return "integer";
            if (n.StartsWith("is_") || n.StartsWith("has_") || n.EndsWith("_flag") || n.Contains("active"))
                return "boolean";
            return "string";
        }
    }
}

