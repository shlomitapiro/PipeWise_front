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

        public void Dispose() => _http.Dispose();


        // ------------------ Jobs API (Start → Progress → Result) ------------------
        public async Task<RunStartResponse> StartRunAsync(object pipelineConfig, CancellationToken ct = default)
        {
            using var payload = JsonContent.Create(pipelineConfig);
            var res = await _http.PostAsync("runs", payload, ct);
            res.EnsureSuccessStatusCode();
            var dto = await res.Content.ReadFromJsonAsync<RunStartResponse>(cancellationToken: ct);
            if (dto == null || string.IsNullOrWhiteSpace(dto.RunId))
                throw new InvalidOperationException("Server did not return a valid run_id.");
            return dto;
        }

        public async Task<RunProgressResponse> GetRunProgressAsync(string runId, CancellationToken ct = default)
        {
            var dto = await _http.GetFromJsonAsync<RunProgressResponse>($"runs/{runId}/progress", ct);
            return dto ?? new RunProgressResponse { Status = "unknown", Percent = 0 };
        }

        public async Task<RunResultEnvelope?> GetRunResultAsync(string runId, CancellationToken ct = default)
        {
            return await _http.GetFromJsonAsync<RunResultEnvelope>($"runs/{runId}/result", ct);
        }


        // ------------------ Reports ------------------

        public async Task<List<ReportInfo>> GetReportsListAsync(int limit = 50, CancellationToken ct = default)
        {
            var res = await _http.GetAsync($"reports?limit={limit}", ct);
            if (!res.IsSuccessStatusCode) return new List<ReportInfo>();

            var json = await res.Content.ReadAsStringAsync(ct);
            var parsed = System.Text.Json.JsonSerializer.Deserialize<ReportsListResponse>(json);
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
            if (!res.IsSuccessStatusCode) return Array.Empty<byte>(); // ↓ לא מחזירים null
            return await res.Content.ReadAsByteArrayAsync(ct);
        }

        public async Task<CleanupResult?> CleanupOldReportsAsync(int maxReports = 100, int maxAgeDays = 30, CancellationToken ct = default)
        {
            // ↓ StringContent עם קידוד וסוג תוכן – אין null-encoding
            using var content = new StringContent(string.Empty, Encoding.UTF8, "application/json");
            var res = await _http.PostAsync($"reports/cleanup?max_reports={maxReports}&max_age_days={maxAgeDays}", content, ct);

            if (!res.IsSuccessStatusCode) return null;

            var json = await res.Content.ReadAsStringAsync(ct);
            var parsed = System.Text.Json.JsonSerializer.Deserialize<CleanupResponse>(json);
            return parsed?.CleanupResult;
        }

        public async Task<bool> DownloadReportToFileAsync(string reportId, string fileType, string destinationPath, CancellationToken ct = default)
        {
            using var res = await _http.GetAsync($"reports/{reportId}/download?file_type={fileType}",
                                                HttpCompletionOption.ResponseHeadersRead, ct);
            if (!res.IsSuccessStatusCode) return false;
            await using var input = await res.Content.ReadAsStreamAsync(ct);
            await using var output = File.Create(destinationPath);
            await input.CopyToAsync(output, ct);
            return true;
        }


        // ------------------ Pipelines (saved) ------------------

        public async Task<PipelinesListResponse> ListPipelinesAsync(string? q = null, int limit = 100, CancellationToken ct = default)
        {
            var url = "pipelines";
            url += string.IsNullOrWhiteSpace(q) ? $"?limit={limit}" : $"?q={Uri.EscapeDataString(q)}&limit={limit}";
            var res = await _http.GetFromJsonAsync<PipelinesListResponse>(url, ct);
            return res ?? new PipelinesListResponse { pipelines = new List<PipelineSummary>(), total_count = 0 };
        }

        public Task<PipelineResponse?> GetPipelineAsync(string id, CancellationToken ct = default)
            => _http.GetFromJsonAsync<PipelineResponse?>($"pipelines/{id}", ct);

        public async Task<PipelineResponse> CreatePipelineAsync(
            PipelineConfig config,
            string? name = null,
            string? description = null,
            CancellationToken ct = default)
        {
            // אם ניתן שם/תיאור – נשלב אותם יחד עם השדות הראשיים שהשרת מצפה להם
            // payload ברמת-שורש: { name, description, source, processors, target }
            HttpContent payload;

            if (!string.IsNullOrWhiteSpace(name) || !string.IsNullOrWhiteSpace(description))
            {
                var body = new Dictionary<string, object?>()
                {
                    ["name"] = string.IsNullOrWhiteSpace(name) ? null : name,
                    ["description"] = string.IsNullOrWhiteSpace(description) ? null : description,
                    ["source"] = config.Source,
                    ["processors"] = config.Processors,
                    ["target"] = config.Target
                };
                payload = JsonContent.Create(body);
            }
            else
            {
                // תאימות: כפי שהיה קודם – שליחת האובייקט עצמו
                payload = JsonContent.Create(config);
            }

            var res = await _http.PostAsync("pipelines", payload, ct);

            // אם מסיבה כלשהי השרת לא מקבל פורמט עם name/description – fallback לפורמט הישן
            if (!res.IsSuccessStatusCode && (name != null || description != null))
            {
                var fallback = await _http.PostAsync("pipelines", JsonContent.Create(config), ct);
                fallback.EnsureSuccessStatusCode();
                var pr2 = await fallback.Content.ReadFromJsonAsync<PipelineResponse>(cancellationToken: ct);
                if (pr2 == null) throw new InvalidOperationException("Empty response from server.");
                return pr2;
            }

            res.EnsureSuccessStatusCode();
            var pr = await res.Content.ReadFromJsonAsync<PipelineResponse>(cancellationToken: ct);
            if (pr == null) throw new InvalidOperationException("Empty response from server.");
            return pr;
        }


        public async Task<PipelineResponse> UpdatePipelineAsync(string id, PipelineConfig config, CancellationToken ct = default)
        {
            var payload = JsonContent.Create(config);
            var res = await _http.PutAsync($"pipelines/{id}", payload, ct);
            res.EnsureSuccessStatusCode();
            var body = await res.Content.ReadFromJsonAsync<PipelineResponse>(cancellationToken: ct);
            return body ?? throw new InvalidOperationException("Empty or invalid server response for UpdatePipelineAsync.");
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
            using var form = new MultipartFormDataContent();
            var addedAny = false;

            if (!string.IsNullOrWhiteSpace(filePath))
            {
                await using var fs = File.OpenRead(filePath);
                var fileContent = new StreamContent(fs);
                form.Add(fileContent, "file", Path.GetFileName(filePath));
                addedAny = true;
            }

            if (overridesObj != null)
            {
                var json = System.Text.Json.JsonSerializer.Serialize(overridesObj,
                    new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
                form.Add(new StringContent(json, Encoding.UTF8, "application/json"), "overrides");
                addedAny = true;
            }

            if (report != null)
            {
                var json = System.Text.Json.JsonSerializer.Serialize(report,
                    new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
                form.Add(new StringContent(json, Encoding.UTF8, "application/json"), "report_settings");
                addedAny = true;
            }

            // ✅ אם אין חלקים — הוסף no-op כדי לרצות את ה-parser של multipart
            if (!addedAny)
                form.Add(new StringContent("1"), "noop");

            var res = await _http.PostAsync($"pipelines/{id}/run", form, ct);
            var text = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
                throw new HttpRequestException($"Server error ({(int)res.StatusCode} {res.StatusCode}): {text}");
            var body = System.Text.Json.JsonSerializer.Deserialize<RunPipelineResult>(text);
            return body ?? throw new InvalidOperationException("Empty or invalid server response for RunPipelineByIdAsync.");
        }

        public async Task<RunPipelineResult> RunWithProgressAsync(
            object pipelineConfig,
            IProgress<(string Status, int Percent)>? progress = null,
            TimeSpan? pollInterval = null,
            CancellationToken ct = default)
        {
            var start = await StartRunAsync(pipelineConfig, ct);
            var interval = pollInterval ?? TimeSpan.FromMilliseconds(500);

            while (true)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var pr = await GetRunProgressAsync(start.RunId, ct);
                    progress?.Report((pr.Status ?? "unknown", pr.Percent ?? 0));
                    if (pr.Status == "completed" || pr.Status == "failed")
                        break;
                }
                catch (HttpRequestException) { /* רשת זמנית — ננסה שוב */ }
                await Task.Delay(interval, ct);
            }

            var final = await GetRunResultAsync(start.RunId, ct)
                ?? throw new InvalidOperationException("Missing run result.");
            if (string.Equals(final.Status, "failed", StringComparison.OrdinalIgnoreCase))
            {
                var err = final.Result?.message; // או .Message אם זה PascalCase אצלך
                throw new InvalidOperationException(!string.IsNullOrWhiteSpace(err) ? err : "Run failed.");
            }

            return final.Result ?? new RunPipelineResult();
        }


        // ------------------ Ad-hoc run (/run-pipeline) ------------------
        // שימוש מה- MainWindow כשמריצים עם קובץ+קונפיג שלא נשמרו במאגר
        public async Task<RunPipelineResult> RunAdHocPipelineAsync(
            string filePath, PipelineConfig config, RunReportSettings? report = null, CancellationToken ct = default)
        {
            using var form = new MultipartFormDataContent();

            await using var fs = File.OpenRead(filePath);
            var fileContent = new StreamContent(fs);
            form.Add(fileContent, "file", Path.GetFileName(filePath));

            var cfgJson = System.Text.Json.JsonSerializer.Serialize(config);
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

            // נפרש לתוך RunPipelineResult כדי לקבל TargetPath
            var body = System.Text.Json.JsonSerializer.Deserialize<RunPipelineResult>(text);
            if (body == null) throw new InvalidOperationException("Empty or invalid server response for RunAdHocPipelineAsync.");
            return body;
        }

        public async Task<ReportInfo?> GetReportDetailsAsync(string reportId, CancellationToken ct = default)
        {
            var res = await _http.GetAsync($"reports/{reportId}", ct);
            if (!res.IsSuccessStatusCode) return null;

            var json = await res.Content.ReadAsStringAsync(ct);
            var dto = System.Text.Json.JsonSerializer.Deserialize<SingleReportResponse>(json);
            return dto?.Report;
        }

        private class SingleReportResponse
        {
            [System.Text.Json.Serialization.JsonPropertyName("report")]
            public ReportInfo? Report { get; set; }
        }

        // ------------------ Info & Validation ------------------

        public async Task<SupportedSourcesResponse?> GetSupportedSourcesAsync(CancellationToken ct = default)
        {
            try
            {
                var response = await _http.GetFromJsonAsync<SupportedSourcesResponse>("sources", ct);
                return response;
            }
            catch (HttpRequestException ex)
            {
                // רשת או שגיאת שרת
                throw new InvalidOperationException($"Failed to get supported sources: {ex.Message}", ex);
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                // timeout
                throw new InvalidOperationException("Request timed out while getting supported sources", ex);
            }
            catch (Exception ex)
            {
                // שגיאה כללית
                throw new InvalidOperationException($"Unexpected error getting supported sources: {ex.Message}", ex);
            }
        }

        public async Task<ProcessorsResponse?> GetProcessorsAsync(CancellationToken ct = default)
        {
            try
            {
                var response = await _http.GetFromJsonAsync<ProcessorsResponse>("processors", ct);
                return response;
            }
            catch (HttpRequestException ex)
            {
                // רשת או שגיאת שרת
                throw new InvalidOperationException($"Failed to get available processors: {ex.Message}", ex);
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                // timeout
                throw new InvalidOperationException("Request timed out while getting processors", ex);
            }
            catch (Exception ex)
            {
                // שגיאה כללית
                throw new InvalidOperationException($"Unexpected error getting processors: {ex.Message}", ex);
            }
        }

        public async Task<ColumnProfileResponse?> ProfileColumnsAsync(object payload, CancellationToken ct = default)
        {
            try
            {
                using var content = JsonContent.Create(payload);
                var response = await _http.PostAsync("columns/profile", content, ct);
                response.EnsureSuccessStatusCode();
                
                var result = await response.Content.ReadFromJsonAsync<ColumnProfileResponse>(cancellationToken: ct);
                return result;
            }
            catch (HttpRequestException ex)
            {
                // רשת או שגיאת שרת
                throw new InvalidOperationException($"Failed to profile columns: {ex.Message}", ex);
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                // timeout
                throw new InvalidOperationException("Request timed out while profiling columns", ex);
            }
            catch (Exception ex)
            {
                // שגיאה כללית
                throw new InvalidOperationException($"Unexpected error profiling columns: {ex.Message}", ex);
            }
        }
    }
}