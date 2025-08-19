using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using PipeWiseClient.Models;

namespace PipeWiseClient.Services
{
    public class ApiClient : IDisposable
    {
        private readonly HttpClient _http;

        // שנה כאן כתובת בסיס אם צריך
        private const string BASE_URL = "http://127.0.0.1:8000/";

        public ApiClient(HttpClient? http = null)
        {
            _http = http ?? new HttpClient { BaseAddress = new Uri(BASE_URL) };
        }

        public void Dispose() => _http?.Dispose();

        // ------------------ Reports ------------------

        public async Task<List<ReportInfo>> GetReportsListAsync(int limit = 50, CancellationToken ct = default)
        {
            var res = await _http.GetAsync($"reports?limit={limit}", ct);
            if (!res.IsSuccessStatusCode) return new List<ReportInfo>();

            var json = await res.Content.ReadAsStringAsync(ct);
            var parsed = Newtonsoft.Json.JsonConvert.DeserializeObject<ReportsListResponse>(json);
            return parsed?.Reports ?? new List<ReportInfo>();
        }

        public async Task<bool> DeleteReportAsync(string reportId, CancellationToken ct = default)
        {
            var res = await _http.DeleteAsync($"reports/{reportId}", ct);
            return res.IsSuccessStatusCode;
        }

        public async Task<byte[]> DownloadReportFileAsync(string reportId, string fileType, CancellationToken ct = default)
        {
            var res = await _http.GetAsync($"reports/{reportId}/download?file_type={fileType}", ct);
            if (!res.IsSuccessStatusCode) return null;
            return await res.Content.ReadAsByteArrayAsync(ct);
        }

        public async Task<CleanupResult> CleanupOldReportsAsync(int maxReports = 100, int maxAgeDays = 30, CancellationToken ct = default)
        {
            var res = await _http.PostAsync(
                $"reports/cleanup?max_reports={maxReports}&max_age_days={maxAgeDays}",
                new StringContent(string.Empty),
                ct);

            if (!res.IsSuccessStatusCode) return null;

            var json = await res.Content.ReadAsStringAsync(ct);
            var parsed = Newtonsoft.Json.JsonConvert.DeserializeObject<CleanupResponse>(json);
            return parsed?.CleanupResult;
        }

        // ------------------ Pipelines (saved) ------------------

        public async Task<PipelinesListResponse> ListPipelinesAsync(string q = null, int limit = 100, CancellationToken ct = default)
        {
            var url = "pipelines";
            url += string.IsNullOrWhiteSpace(q) ? $"?limit={limit}" : $"?q={Uri.EscapeDataString(q)}&limit={limit}";
            var res = await _http.GetFromJsonAsync<PipelinesListResponse>(url, ct);
            return res ?? new PipelinesListResponse { pipelines = new List<PipelineSummary>(), total_count = 0 };
        }
        public Task<PipelineResponse?> GetPipelineAsync(string id, CancellationToken ct = default)
            => _http.GetFromJsonAsync<PipelineResponse?>($"pipelines/{id}", ct);

        public async Task<PipelineResponse> CreatePipelineAsync(PipelineConfig config, CancellationToken ct = default)
        {
            var payload = JsonContent.Create(config);
            var res = await _http.PostAsync("pipelines", payload, ct);
            res.EnsureSuccessStatusCode();
            return await res.Content.ReadFromJsonAsync<PipelineResponse>(cancellationToken: ct);
        }

        public async Task<PipelineResponse> UpdatePipelineAsync(string id, PipelineConfig config, CancellationToken ct = default)
        {
            var payload = JsonContent.Create(config);
            var res = await _http.PutAsync($"pipelines/{id}", payload, ct);
            res.EnsureSuccessStatusCode();
            return await res.Content.ReadFromJsonAsync<PipelineResponse>(cancellationToken: ct);
        }

        public async Task DeletePipelineAsync(string id, CancellationToken ct = default)
        {
            var res = await _http.DeleteAsync($"pipelines/{id}", ct);
            res.EnsureSuccessStatusCode();
        }

        public async Task<RunPipelineResult> RunPipelineByIdAsync(
            string id,
            string? filePath = null,
            object? overridesObj = null,
            RunReportSettings? report = null,
            CancellationToken ct = default)

        {
            var form = new MultipartFormDataContent();

            if (!string.IsNullOrWhiteSpace(filePath))
                form.Add(new ByteArrayContent(await File.ReadAllBytesAsync(filePath, ct)), "file", Path.GetFileName(filePath));

            if (overridesObj != null)
                form.Add(new StringContent(JsonSerializer.Serialize(overridesObj), Encoding.UTF8, "application/json"), "overrides");

            if (report != null)
                form.Add(new StringContent(JsonSerializer.Serialize(report), Encoding.UTF8, "application/json"), "report_settings");

            var res = await _http.PostAsync($"pipelines/{id}/run", form, ct);
            res.EnsureSuccessStatusCode();
            return await res.Content.ReadFromJsonAsync<RunPipelineResult>(cancellationToken: ct);
        }

        // ------------------ Ad-hoc run (/run-pipeline) ------------------
        // שימוש מה- MainWindow כשמריצים עם קובץ+קונפיג שלא נשמרו במאגר
        public async Task<string> RunAdHocPipelineAsync(string filePath, PipelineConfig config, RunReportSettings report = null, CancellationToken ct = default)
        {
            using var form = new MultipartFormDataContent();

            form.Add(new ByteArrayContent(await File.ReadAllBytesAsync(filePath, ct)), "file", Path.GetFileName(filePath));

            var cfgJson = Newtonsoft.Json.JsonConvert.SerializeObject(config);
            form.Add(new StringContent(cfgJson, Encoding.UTF8, "application/json"), "config");

            if (report != null)
            {
                var repJson = JsonSerializer.Serialize(report);
                form.Add(new StringContent(repJson, Encoding.UTF8, "application/json"), "report_settings");
            }

            var res = await _http.PostAsync("run-pipeline", form, ct);
            var text = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
                throw new HttpRequestException($"Server error ({res.StatusCode}): {text}");
            return text;
        }
    }
}
