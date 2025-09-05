// Models/RunPipelineResult.cs
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PipeWiseClient.Models
{
    /// <summary>
    /// DTO חזק + עזרי המרה עבור המודל ה"חלש" RunPipelineResult שמוגדר ב-Models/PipelineModels.cs.
    /// אין כאן התנגשויות בשם המחלקה.
    /// </summary>
    public class RunPipelineResultDto
    {
        [JsonProperty("status")]           public string Status { get; set; } = "";
        [JsonProperty("message")]          public string Message { get; set; } = "";
        [JsonProperty("target_path")]      public string? TargetPath { get; set; }
        [JsonProperty("report_paths")]     public Dictionary<string, string> ReportPaths { get; set; } = new();
        [JsonProperty("data_preview")]     public DataPreviewModel DataPreview { get; set; } = new();

        // ✨ אימות תוצר אם השרת מחזיר
        [JsonProperty("output_validation")] public OutputValidation? OutputValidation { get; set; }

        // שדות אופציונליים/כלליים
        [JsonProperty("progress_file")]     public string? ProgressFile { get; set; }
        [JsonProperty("summary")]           public Dictionary<string, object>? Summary { get; set; }
        [JsonProperty("execution_summary")] public Dictionary<string, object>? ExecutionSummary { get; set; }
        [JsonProperty("commit_info")]       public Dictionary<string, object>? CommitInfo { get; set; }
        [JsonProperty("config_echo")]       public Dictionary<string, object>? ConfigEcho { get; set; }
        [JsonProperty("sample_data")]       public List<Dictionary<string, object>>? SampleData { get; set; }

        public class DataPreviewModel
        {
            [JsonProperty("columns")]    public List<string> Columns { get; set; } = new();
            [JsonProperty("total_rows")] public int TotalRows { get; set; }
        }

        /// <summary>
        /// המרה נוחה מהמודל ה"חלש" שמוחזר ע"י הדסיריאליזציה (PipelineModels.RunPipelineResult)
        /// אל DTO חזק.
        /// </summary>
        public static RunPipelineResultDto From(RunPipelineResult loose)
        {
            if (loose == null) return new RunPipelineResultDto();

            // המרה דרך JObject כדי למפות שמות שדות JSON כמו שהם (camelCase)
            var jo = JObject.FromObject(loose);
            var dto = jo.ToObject<RunPipelineResultDto>() ?? new RunPipelineResultDto();

            // הרצה זהירה אם report_paths אינו מילון פשוט
            if (dto.ReportPaths.Count == 0 && jo["report_paths"] is JObject rpObj)
            {
                foreach (var p in rpObj.Properties())
                {
                    var val = p.Value.Type == JTokenType.String
                        ? p.Value.ToString()
                        : p.Value.ToString(Formatting.None);
                    dto.ReportPaths[p.Name] = val;
                }
            }
            return dto;
        }
    }

    /// <summary>
    /// Extension Methods נוחים לעבודה עם המודל הקיים (PipelineModels.RunPipelineResult)
    /// בלי לשנות את המחלקה עצמה.
    /// </summary>
    public static class RunPipelineResultExtensions
    {
        /// <summary>המרה ל-DTO חזק.</summary>
        public static RunPipelineResultDto ToDto(this RunPipelineResult loose)
            => RunPipelineResultDto.From(loose);

        /// <summary>שליפת OutputValidation אם קיים בתשובת השרת.</summary>
        public static OutputValidation? GetOutputValidation(this RunPipelineResult loose)
            => loose.ToDto().OutputValidation;

        /// <summary>שליפת report_paths כמילון מחרוזות.</summary>
        public static Dictionary<string, string> GetReportPaths(this RunPipelineResult loose)
            => loose.ToDto().ReportPaths;
    }
}
