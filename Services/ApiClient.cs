using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using PipeWiseClient.Models;

namespace PipeWiseClient.Services
{
    /// <summary>
    /// תוצאת סריקת שדה לערכים ייחודיים
    /// </summary>
    public class ScanFieldResult // ✅ זה יכול להיות מחוץ למחלקה
    {
        public string FieldName { get; set; } = string.Empty;
        public List<string> UniqueValues { get; set; } = new();
        public int TotalRows { get; set; }
        public int NullCount { get; set; }
        public bool FieldExists { get; set; }
        public bool Truncated { get; set; }
        public List<string> AvailableFields { get; set; } = new();
        public string Message { get; set; } = string.Empty;
    }

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

        /// <summary>
        /// סריקת שדה בקובץ למציאת ערכים ייחודיים
        /// משמש לקידוד קטגוריאלי
        /// </summary>
        public async Task<ScanFieldResult> ScanFieldValuesAsync( 
            string filePath,
            string fieldName,
            int maxUniqueValues = 100,
            CancellationToken ct = default)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"ScanFieldValuesAsync called:");
                System.Diagnostics.Debug.WriteLine($"  filePath: '{filePath}'");
                System.Diagnostics.Debug.WriteLine($"  fieldName: '{fieldName}'");
                System.Diagnostics.Debug.WriteLine($"  File exists: {File.Exists(filePath)}");

                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException($"File not found: {filePath}");
                }

                using var form = new MultipartFormDataContent();

                // הוסף את הקובץ
                var fileBytes = await File.ReadAllBytesAsync(filePath, ct);
                var fileContent = new ByteArrayContent(fileBytes);

                // הגדרת Content-Type לפי סוג הקובץ
                var extension = Path.GetExtension(filePath).ToLower();
                var contentType = extension switch
                {
                    ".csv" => "text/csv",
                    ".json" => "application/json",
                    ".xml" => "text/xml", // שינוי ל-text/xml
                    ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    ".xls" => "application/vnd.ms-excel",
                    _ => "application/octet-stream"
                };

                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
                form.Add(fileContent, "file", Path.GetFileName(filePath));

                // הוסף פרמטרים נוספים
                form.Add(new StringContent(fieldName), "field_name");
                form.Add(new StringContent(maxUniqueValues.ToString()), "max_unique_values");

                System.Diagnostics.Debug.WriteLine($"Sending request to /scan-field with file: {Path.GetFileName(filePath)}, field: {fieldName}");

                // שליחת הבקשה
                var response = await _http.PostAsync("/scan-field", form, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                System.Diagnostics.Debug.WriteLine($"Server response status: {response.StatusCode}");
                if (!response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"Server response content: {content}");
                }

                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException($"API Error: {response.StatusCode} - {content}");
                }

                var result = System.Text.Json.JsonSerializer.Deserialize<ScanFieldResult>(content,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
                    });

                return result ?? new ScanFieldResult
                {
                    FieldExists = false,
                    Message = "Failed to parse server response"
                };
            }
            catch (HttpRequestException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error scanning field values: {ex.Message}", ex);
            }
        }

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
            if (!res.IsSuccessStatusCode) return Array.Empty<byte>();
            return await res.Content.ReadAsByteArrayAsync(ct);
        }

        public async Task<CleanupResult?> CleanupOldReportsAsync(int maxReports = 100, int maxAgeDays = 30, CancellationToken ct = default)
        {
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
                payload = JsonContent.Create(config);
            }

            var res = await _http.PostAsync("pipelines", payload, ct);

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
                catch (HttpRequestException) { }
                await Task.Delay(interval, ct);
            }

            var final = await GetRunResultAsync(start.RunId, ct)
                ?? throw new InvalidOperationException("Missing run result.");
            if (string.Equals(final.Status, "failed", StringComparison.OrdinalIgnoreCase))
            {
                var err = final.Result?.message;
                throw new InvalidOperationException(!string.IsNullOrWhiteSpace(err) ? err : "Run failed.");
            }

            return final.Result ?? new RunPipelineResult();
        }

        // ------------------ Ad-hoc run (/run-pipeline) ------------------
        public async Task<RunPipelineResult> RunAdHocPipelineAsync( // ✅ רק מתודה אחת!
            string filePath,
            PipelineConfig config,
            RunReportSettings? report = null,
            CancellationToken ct = default)
        {
            using var form = new MultipartFormDataContent();

            var fileBytes = await File.ReadAllBytesAsync(filePath, ct);
            var fileContent = new ByteArrayContent(fileBytes);

            var extension = Path.GetExtension(filePath).ToLower();
            var contentType = extension switch
            {
                ".csv" => "text/csv",
                ".json" => "application/json",
                ".xml" => "application/xml",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".xls" => "application/vnd.ms-excel",
                _ => "application/octet-stream"
            };

            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
            form.Add(fileContent, "file", Path.GetFileName(filePath));

            var configJson = System.Text.Json.JsonSerializer.Serialize(config, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                WriteIndented = false,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            });
            form.Add(new StringContent(configJson), "config");

            if (report != null)
            {
                var reportJson = System.Text.Json.JsonSerializer.Serialize(report);
                form.Add(new StringContent(reportJson), "report_settings");
            }

            var response = await _http.PostAsync("/run-pipeline", form, ct);
            var content = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"API Error: {response.StatusCode} - {content}");
            }

            return System.Text.Json.JsonSerializer.Deserialize<RunPipelineResult>(content,
                 new JsonSerializerOptions
                 {
                     PropertyNameCaseInsensitive = true,
                     PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
                }) ?? new RunPipelineResult { message = "Failed to parse response" };
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
                throw new InvalidOperationException($"Failed to get supported sources: {ex.Message}", ex);
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                throw new InvalidOperationException("Request timed out while getting supported sources", ex);
            }
            catch (Exception ex)
            {
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
                throw new InvalidOperationException($"Failed to get available processors: {ex.Message}", ex);
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                throw new InvalidOperationException("Request timed out while getting processors", ex);
            }
            catch (Exception ex)
            {
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
                throw new InvalidOperationException($"Failed to profile columns: {ex.Message}", ex);
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                throw new InvalidOperationException("Request timed out while profiling columns", ex);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Unexpected error profiling columns: {ex.Message}", ex);
            }
        }
    }
}
