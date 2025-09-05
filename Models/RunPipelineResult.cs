// Models/RunPipelineResult.cs
using System.Collections.Generic;
using Newtonsoft.Json;

namespace PipeWiseClient.Models
{
    public class RunPipelineResult
    {
        [JsonProperty("status")]        public string Status { get; set; } = "";
        [JsonProperty("message")]       public string Message { get; set; } = "";

        [JsonProperty("target_path")]   public string? TargetPath { get; set; }

        [JsonProperty("report_paths")]  public Dictionary<string, string> ReportPaths { get; set; } = new();

        [JsonProperty("data_preview")]  public DataPreviewModel DataPreview { get; set; } = new();

        // ✨ חדש – ייאסף מהשרת אם קיים
        [JsonProperty("output_validation")] public OutputValidation? OutputValidation { get; set; }

        // שדות נוספים שהשרת עשוי להחזיר – נשאיר גמישות קדימה
        [JsonProperty("progress_file")]      public string? ProgressFile { get; set; }
        [JsonProperty("summary")]            public Dictionary<string, object>? Summary { get; set; }
        [JsonProperty("execution_summary")]  public Dictionary<string, object>? ExecutionSummary { get; set; }
        [JsonProperty("commit_info")]        public Dictionary<string, object>? CommitInfo { get; set; }
        [JsonProperty("config_echo")]        public Dictionary<string, object>? ConfigEcho { get; set; }
        [JsonProperty("sample_data")]        public List<Dictionary<string, object>>? SampleData { get; set; }

        public class DataPreviewModel
        {
            [JsonProperty("columns")]    public List<string> Columns { get; set; } = new();
            [JsonProperty("total_rows")] public int TotalRows { get; set; }
        }
    }
}
