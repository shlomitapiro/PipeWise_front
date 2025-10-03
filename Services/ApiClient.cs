// API client for PipeWise server
// Services/ApiClient.cs
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using PipeWiseClient.Models;

using PipeWiseClient.Interfaces;
namespace PipeWiseClient.Services
{
    public class RunAlreadyStartedException : Exception
    {
        public string RunId { get; }
        public RunAlreadyStartedException(string runId, Exception inner)
            : base($"Run already started (run_id={runId})", inner)
        {
            RunId = runId;
        }
    }
    public class ScanFieldResult
    {
        [JsonPropertyName("field_name")]
        public string FieldName { get; set; } = string.Empty;

        [JsonPropertyName("unique_values")]
        public List<string> UniqueValues { get; set; } = new();

        [JsonPropertyName("total_rows")]
        public int TotalRows { get; set; }

        [JsonPropertyName("null_count")]
        public int NullCount { get; set; }

        [JsonPropertyName("field_exists")]
        public bool FieldExists { get; set; }

        [JsonPropertyName("truncated")]
        public bool Truncated { get; set; }

        [JsonPropertyName("available_fields")]
        public List<string> AvailableFields { get; set; } = new();

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;
    }

    public class ApiClient : IApiClient, IDisposable
    {
        private readonly HttpClient _http;

        private const string BASE_URL = "http://127.0.0.1:8000/";

        public ApiClient(HttpClient? http = null)
        {
            _http = http ?? new HttpClient { BaseAddress = new Uri(BASE_URL) };
        }

        private static object? NormalizeForHttp(object? value)
        {
            if (value is null) return null;

            if (value is JsonElement je) return FromJsonElement(je);

            if (value is IDictionary<string, object?> gdict)
            {
                var copy = new Dictionary<string, object?>(gdict.Count);
                foreach (var kv in gdict)
                    copy[kv.Key] = NormalizeForHttp(kv.Value);
                return copy;
            }

            if (value is System.Collections.IDictionary ndict)
            {
                var copy = new Dictionary<string, object?>();
                foreach (var key in ndict.Keys)
                    if (key is string s)
                        copy[s] = NormalizeForHttp(ndict[key]);
                return copy;
            }

            if (value is System.Collections.IEnumerable seq && value is not string)
            {
                var list = new List<object?>();
                foreach (var item in seq)
                    list.Add(NormalizeForHttp(item));
                return list;
            }

            var type = value.GetType();
            if (!type.IsPrimitive && type != typeof(string) && !type.IsEnum)
            {
                var props = type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (props.Length > 0)
                {
                    var obj = new Dictionary<string, object?>(props.Length);
                    foreach (var p in props)
                        if (p.CanRead)
                            obj[p.Name] = NormalizeForHttp(p.GetValue(value));
                    return obj;
                }
            }

            return value;
        }


        private static object? FromJsonElement(JsonElement e)
        {
            switch (e.ValueKind)
            {
                case JsonValueKind.Object:
                    var obj = new Dictionary<string, object?>();
                    foreach (var p in e.EnumerateObject()) obj[p.Name] = FromJsonElement(p.Value);
                    return obj;

                case JsonValueKind.Array:
                    var arr = new List<object?>();
                    foreach (var it in e.EnumerateArray()) arr.Add(FromJsonElement(it));
                    return arr;

                case JsonValueKind.String: return e.GetString();
                case JsonValueKind.Number:
                    if (e.TryGetInt64(out var l)) return l;
                    if (e.TryGetDouble(out var d)) return d;
                    return e.GetRawText();
                case JsonValueKind.True:  return true;
                case JsonValueKind.False: return false;
                case JsonValueKind.Null:  return null;
                default: return null;
            }
        }

        public void Dispose() => _http.Dispose();

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

                var fileBytes = await File.ReadAllBytesAsync(filePath, ct);
                var fileContent = new ByteArrayContent(fileBytes);

                var extension = Path.GetExtension(filePath).ToLower();
                var contentType = extension switch
                {
                    ".csv"  => "text/csv",
                    ".json" => "application/json",
                    ".xml"  => "application/xml",
                    ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    ".xls"  => "application/vnd.ms-excel",
                    _       => "application/octet-stream"
                };

                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
                form.Add(fileContent, "file", Path.GetFileName(filePath));

                form.Add(new StringContent(fieldName), "field_name");
                form.Add(new StringContent(maxUniqueValues.ToString()), "max_unique_values");

                System.Diagnostics.Debug.WriteLine($"Sending request to /scan-field with file: {Path.GetFileName(filePath)}, field: {fieldName}");

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

                var result = System.Text.Json.JsonSerializer.Deserialize<ScanFieldResult>(
                    content,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                );

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
            catch (Exception)
            {
                throw;
            }
        }

        // ------------------ Jobs API (Start → Progress → Result) ------------------
        public async Task<RunStartResponse> StartRunAsync(object pipelineConfig, CancellationToken ct = default)
        {
            var normalized = new
            {
                source = NormalizeForHttp(pipelineConfig is PipelineConfig pc ? pc.Source : pipelineConfig.GetType().GetProperty("Source")?.GetValue(pipelineConfig)),
                processors = NormalizeForHttp(pipelineConfig is PipelineConfig pc2 ? pc2.Processors : pipelineConfig.GetType().GetProperty("Processors")?.GetValue(pipelineConfig)),
                target = NormalizeForHttp(pipelineConfig is PipelineConfig pc3 ? pc3.Target : pipelineConfig.GetType().GetProperty("Target")?.GetValue(pipelineConfig)),
            };

            var json = Newtonsoft.Json.JsonConvert.SerializeObject(normalized);
            using var payload = new StringContent(json, Encoding.UTF8, "application/json");

            var res = await _http.PostAsync("runs", payload, ct);

            if (!res.IsSuccessStatusCode)
            {
                var rawErr = await res.Content.ReadAsStringAsync(ct);
                System.Diagnostics.Debug.WriteLine($"StartRunAsync: non-success {(int)res.StatusCode} {res.StatusCode}. Body prefix: {rawErr?.Substring(0, System.Math.Min(120, rawErr?.Length ?? 0))}");

                // Only treat 409 Conflict as an existing active run (server-side duplicate prevention)
                if (res.StatusCode == System.Net.HttpStatusCode.Conflict)
                {
                    try
                    {
                        using var errDoc = System.Text.Json.JsonDocument.Parse(rawErr);
                        var rootErr = errDoc.RootElement;
                        string? existingRunId = null;
                        if (rootErr.TryGetProperty("run_id", out var e1)) existingRunId = e1.GetString();
                        else if (rootErr.TryGetProperty("runId", out var e2)) existingRunId = e2.GetString();
                        else if (rootErr.TryGetProperty("id", out var e3)) existingRunId = e3.GetString();
                        if (!string.IsNullOrWhiteSpace(existingRunId))
                        {
                            System.Diagnostics.Debug.WriteLine($"StartRunAsync: using existing active run_id={existingRunId}");
                            return new RunStartResponse { RunId = existingRunId! };
                        }
                    }
                    catch { /* ignore parse errors; will fall through to EnsureSuccessStatusCode */ }
                }

                // Not a duplicate active run; surface the error
                res.EnsureSuccessStatusCode();
            }

            var raw = await res.Content.ReadAsStringAsync(ct);
            // פרסור גמיש: run_id / runId / id
            string? runId = null;
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(raw);
                var root = doc.RootElement;
                if (root.TryGetProperty("run_id", out var p1)) runId = p1.GetString();
                else if (root.TryGetProperty("runId", out var p2)) runId = p2.GetString();
                else if (root.TryGetProperty("id", out var p3)) runId = p3.GetString();
            }
            catch { /* raw not json? let it fail below */ }

            if (string.IsNullOrWhiteSpace(runId))
                throw new InvalidOperationException("Server did not return a valid run_id.");

            return new RunStartResponse { RunId = runId! };

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
            if (!res.IsSuccessStatusCode) return Array.Empty<byte>();
            return await res.Content.ReadAsByteArrayAsync(ct);
        }

        public async Task<CleanupResult?> CleanupOldReportsAsync(int maxReports = 100, int maxAgeDays = 30, CancellationToken ct = default)
        {
            using var content = new StringContent(string.Empty, Encoding.UTF8, "application/json");
            var res = await _http.PostAsync($"reports/cleanup?max_reports={maxReports}&max_age_days={maxAgeDays}", content, ct);

            if (!res.IsSuccessStatusCode) return null;

            var json = await res.Content.ReadAsStringAsync(ct);
            var parsed = Newtonsoft.Json.JsonConvert.DeserializeObject<CleanupResponse>(json);
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
            var body = new Dictionary<string, object?>()
            {
                ["name"] = string.IsNullOrWhiteSpace(name) ? null : name,
                ["description"] = string.IsNullOrWhiteSpace(description) ? null : description,
                ["source"] = NormalizeForHttp(config.Source),
                ["processors"] = NormalizeForHttp(config.Processors),
                ["target"] = NormalizeForHttp(config.Target),
            };

            var httpPayload = JsonContent.Create(body); 
            var res = await _http.PostAsync("pipelines", httpPayload, ct);

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
            var updateBody = new {
                source = NormalizeForHttp(config.Source),
                processors = NormalizeForHttp(config.Processors),
                target = NormalizeForHttp(config.Target),
            };
            var payload = JsonContent.Create(updateBody);
            var res = await _http.PutAsync($"pipelines/{id}", payload, ct);
            res.EnsureSuccessStatusCode();

            var parsed = await res.Content.ReadFromJsonAsync<PipelineResponse>(cancellationToken: ct);
            return parsed ?? throw new InvalidOperationException("Empty or invalid server response for UpdatePipelineAsync.");
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
                var normalizedOverrides = NormalizeForHttp(overridesObj);
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(normalizedOverrides);
                form.Add(new StringContent(json, Encoding.UTF8, "application/json"), "overrides");
                addedAny = true;
            }

            if (report != null)
            {
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(report);
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
            const int MAX_POLL_ATTEMPTS = 600; 
            int pollCount = 0;
            
            string runId = string.Empty;
            try
            {
                var start = await StartRunAsync(pipelineConfig, ct);
                runId = start.RunId;
                progress?.Report(("starting", 0));

                var interval = pollInterval ?? TimeSpan.FromMilliseconds(500);
                
                while (true)
                {
                    pollCount++;
                    
                    if (pollCount > MAX_POLL_ATTEMPTS)
                    {
                        System.Diagnostics.Debug.WriteLine($"RunWithProgressAsync: polling timeout after {pollCount} attempts");
                        throw new TimeoutException($"Progress polling timed out after {pollCount * 500}ms");
                    }
                    
                    ct.ThrowIfCancellationRequested();
                    
                    try
                    {
                        var pr = await GetRunProgressAsync(start.RunId, ct);
                        var status = pr.Status?.ToLowerInvariant() ?? "unknown";
                        var percent = pr.Percent ?? 0;
                        
                        progress?.Report((status, percent));
                        
                        System.Diagnostics.Debug.WriteLine($"Poll #{pollCount}: status={status}, percent={percent}%");
                        
                        if (status == "completed" || 
                            status == "failed" || 
                            status == "error" ||
                            status == "expired" ||
                            status == "cancelled" ||
                             status == "unknown")
                        {
                            System.Diagnostics.Debug.WriteLine($"RunWithProgressAsync: exiting polling loop - status={status}");
                            break;
                        }
                    }
                    catch (HttpRequestException httpEx)
                    {
                        if (httpEx.StatusCode == System.Net.HttpStatusCode.NotFound)
                        {
                            System.Diagnostics.Debug.WriteLine("RunWithProgressAsync: run not found (404) - assuming completed");
                            break;
                        }
                        
                        System.Diagnostics.Debug.WriteLine($"Poll #{pollCount}: HTTP error - {httpEx.Message}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Poll #{pollCount}: unexpected error - {ex.Message}");
                        if (pollCount > 3)
                        {
                            System.Diagnostics.Debug.WriteLine("Too many polling errors - exiting");
                            throw;
                        }
                    }
                    
                    await Task.Delay(interval, ct);
                }

                var final = await GetRunResultAsync(start.RunId, ct);
                
                if (final == null)
                {
                    System.Diagnostics.Debug.WriteLine("RunWithProgressAsync: no final result - returning empty");
                    return new RunPipelineResult 
                    { 
                        status = "completed", 
                        message = "Pipeline completed (no result details)" 
                    };
                }
                
                if (string.Equals(final.Status, "failed", StringComparison.OrdinalIgnoreCase))
                {
                    var err = final.Result?.message;
                    throw new InvalidOperationException(!string.IsNullOrWhiteSpace(err) ? err : "Run failed.");
                }
                
                return final.Result ?? new RunPipelineResult();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                throw;
            }
        }
        // ------------------ Ad-hoc run (/run-pipeline) ------------------
        public async Task<RunPipelineResult> RunAdHocPipelineAsync(
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

            var cfgBody = new {
                source     = NormalizeForHttp(config.Source),
                processors = NormalizeForHttp(config.Processors),
                target     = NormalizeForHttp(config.Target),
            };
            var configJson = Newtonsoft.Json.JsonConvert.SerializeObject(cfgBody);
            form.Add(new StringContent(configJson, Encoding.UTF8, "application/json"), "config");

            if (report != null)
            {
                var reportJson = Newtonsoft.Json.JsonConvert.SerializeObject(report);
                form.Add(new StringContent(reportJson, Encoding.UTF8, "application/json"), "report_settings");
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
            var dto = Newtonsoft.Json.JsonConvert.DeserializeObject<SingleReportResponse>(json);
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

