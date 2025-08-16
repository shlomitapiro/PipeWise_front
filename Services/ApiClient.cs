using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using Newtonsoft.Json;
using PipeWiseClient.Models;

namespace PipeWiseClient.Services
{
    public static class ApiClient
    {
        private static readonly HttpClient client = new HttpClient();
        private const string BASE_URL = "http://localhost:8000";

        public static async Task<string> SendPipelineRequestAsync(PipelineConfig config)
        {
            var json = JsonConvert.SerializeObject(config);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync($"{BASE_URL}/pipeline/run", content);
            return await response.Content.ReadAsStringAsync();
        }

        // === פונקציות דוחות חדשות ===

        public static async Task<List<ReportInfo>> GetReportsListAsync(int limit = 50)
        {
            try
            {
                var response = await client.GetAsync($"{BASE_URL}/reports?limit={limit}");
                
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var result = JsonConvert.DeserializeObject<ReportsListResponse>(json);
                    return result?.Reports ?? new List<ReportInfo>();
                }
                
                return new List<ReportInfo>();
            }
            catch
            {
                return new List<ReportInfo>();
            }
        }

        public static async Task<bool> DeleteReportAsync(string reportId)
        {
            try
            {
                var response = await client.DeleteAsync($"{BASE_URL}/reports/{reportId}");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public static async Task<byte[]?> DownloadReportFileAsync(string reportId, string fileType)
        {
            try
            {
                var response = await client.GetAsync($"{BASE_URL}/reports/{reportId}/download?file_type={fileType}");
                
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsByteArrayAsync();
                }
                
                return null;
            }
            catch
            {
                return null;
            }
        }

        public static async Task<CleanupResult?> CleanupOldReportsAsync(int maxReports = 100, int maxAgeDays = 30)
        {
            try
            {
                var response = await client.PostAsync($"{BASE_URL}/reports/cleanup?max_reports={maxReports}&max_age_days={maxAgeDays}", null);
                
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var result = JsonConvert.DeserializeObject<CleanupResponse>(json);
                    return result?.CleanupResult;
                }
                
                return null;
            }
            catch
            {
                return null;
            }
        }
    }

    // מודלים לתגובות API
    public class ReportsListResponse
    {
        public List<ReportInfo> Reports { get; set; } = new List<ReportInfo>();
        public int TotalCount { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class CleanupResponse
    {
        public CleanupResult? CleanupResult { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class CleanupResult
    {
        public int DeletedReports { get; set; }
        public int KeptReports { get; set; }
        public int TotalProcessed { get; set; }
    }

    public class ReportInfo
    {
        [JsonProperty("pipeline_name")]
        public string? PipelineName { get; set; }

        [JsonProperty("status")]
        public string? Status { get; set; }

        [JsonProperty("duration")]
        public string? Duration { get; set; }

        [JsonProperty("start_time")]
        public string? StartTime { get; set; }

        [JsonProperty("created_at")]
        public string? CreatedAt { get; set; }

        [JsonProperty("input_rows")]
        public int InputRows { get; set; }

        [JsonProperty("output_rows")]
        public int OutputRows { get; set; }

        [JsonProperty("total_errors")]
        public int TotalErrors { get; set; }

        [JsonProperty("total_warnings")]
        public int TotalWarnings { get; set; }

        [JsonProperty("source_type")]
        public string? SourceType { get; set; }

        [JsonProperty("html_path")]
        public string? HtmlPath { get; set; }

        [JsonProperty("pdf_path")]
        public string? PdfPath { get; set; }

        [JsonProperty("files_exist")]
        public FilesExistInfo? FilesExist { get; set; }

        [JsonProperty("report_id")]
        public string? ReportId { get; set; }
    }

    public class FilesExistInfo
    {
        [JsonProperty("html")]
        public bool Html { get; set; }

        [JsonProperty("pdf")]
        public bool Pdf { get; set; }
    }
}