// Models/ReportModels.cs
using System.Collections.Generic;
using Newtonsoft.Json;

namespace PipeWiseClient.Models
{
    public class ReportsListResponse
    {
        [JsonProperty("reports")]
        public List<ReportInfo> Reports { get; set; } = new();

        [JsonProperty("total_count")]
        public int TotalCount { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; } = "";
    }

    public class CleanupResponse
    {
        [JsonProperty("cleanup_result")]
        public CleanupResult? CleanupResult { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; } = "";
    }

    public class CleanupResult
    {
        [JsonProperty("deleted_reports")] public int DeletedReports { get; set; }
        [JsonProperty("kept_reports")]    public int KeptReports { get; set; }
        [JsonProperty("total_processed")] public int TotalProcessed { get; set; }
    }

    public class ReportInfo
    {
        [JsonProperty("report_id")]     public string? ReportId { get; set; }
        [JsonProperty("pipeline_name")] public string? PipelineName { get; set; }
        [JsonProperty("status")]        public string? Status { get; set; }
        [JsonProperty("duration")]      public string? Duration { get; set; }
        [JsonProperty("start_time")]    public string? StartTime { get; set; }
        [JsonProperty("created_at")]    public string? CreatedAt { get; set; }
        [JsonProperty("input_rows")]    public int InputRows { get; set; }
        [JsonProperty("output_rows")]   public int OutputRows { get; set; }
        [JsonProperty("total_errors")]  public int TotalErrors { get; set; }
        [JsonProperty("total_warnings")]public int TotalWarnings { get; set; }
        [JsonProperty("source_type")]   public string? SourceType { get; set; }
        [JsonProperty("html_path")]     public string? HtmlPath { get; set; }
        [JsonProperty("pdf_path")]      public string? PdfPath { get; set; }
        [JsonProperty("files_exist")]   public FilesExistInfo? FilesExist { get; set; }
    }

    public class FilesExistInfo
    {
        [JsonProperty("html")] public bool Html { get; set; }
        [JsonProperty("pdf")]  public bool Pdf  { get; set; }
    }
}
