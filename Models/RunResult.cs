using System.Collections.Generic;
using Newtonsoft.Json;

namespace PipeWiseClient.Models
{public class RunResult
{
    [JsonProperty("status")]       public string Status      { get; set; } = "";
    [JsonProperty("message")]      public string Message     { get; set; } = "";
    [JsonProperty("report_paths")] public Dictionary<string, string> ReportPaths { get; set; } = new();
    [JsonProperty("data_preview")] public DataPreview Preview { get; set; } = new DataPreview();

    public class DataPreview
    {
        [JsonProperty("columns")]   public List<string> Columns { get; set; } = new();
        [JsonProperty("total_rows")]public int TotalRows { get; set; }
    }
}

}
