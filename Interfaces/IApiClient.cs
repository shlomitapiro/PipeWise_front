using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PipeWiseClient.Models;

namespace PipeWiseClient.Interfaces
{
    public interface IApiClient : IDisposable
    {
        Task<PipeWiseClient.Services.ScanFieldResult> ScanFieldValuesAsync(string filePath, string fieldName, int maxUniqueValues = 100, CancellationToken ct = default);

        // Jobs API
        Task<RunStartResponse> StartRunAsync(object pipelineConfig, CancellationToken ct = default);
        Task<RunProgressResponse> GetRunProgressAsync(string runId, CancellationToken ct = default);
        Task<RunResultEnvelope?> GetRunResultAsync(string runId, CancellationToken ct = default);

        // Reports
        Task<List<ReportInfo>> GetReportsListAsync(int limit = 50, CancellationToken ct = default);
        Task<bool> DeleteReportAsync(string reportId, CancellationToken ct = default);
        Task<byte[]> DownloadReportFileAsync(string reportId, string fileType, CancellationToken ct = default);
        Task<CleanupResult?> CleanupOldReportsAsync(int maxReports = 100, int maxAgeDays = 30, CancellationToken ct = default);
        Task<bool> DownloadReportToFileAsync(string reportId, string fileType, string destinationPath, CancellationToken ct = default);

        // Pipelines (saved)
        Task<PipelinesListResponse> ListPipelinesAsync(string? q = null, int limit = 100, CancellationToken ct = default);
        Task<PipelineResponse?> GetPipelineAsync(string id, CancellationToken ct = default);
        Task<PipelineResponse> CreatePipelineAsync(PipelineConfig config, string? name = null, string? description = null, CancellationToken ct = default);
        Task<PipelineResponse> UpdatePipelineAsync(string id, PipelineConfig config, CancellationToken ct = default);
        Task<PipelineResponse> UpdatePipelineNameAsync(string id, string newName, CancellationToken ct = default);
        Task DeletePipelineAsync(string id, CancellationToken ct = default);
        Task<RunPipelineResult> RunPipelineByIdAsync(string id, string? filePath = null, object? overridesObj = null, RunReportSettings? report = null, CancellationToken ct = default);

        // Run pipeline with progress
        Task<RunPipelineResult> RunWithProgressAsync(object pipelineConfig, IProgress<(string Status, int Percent)>? progress = null, TimeSpan? pollInterval = null, CancellationToken ct = default);

        // Ad-hoc run
        Task<RunPipelineResult> RunAdHocPipelineAsync(string filePath, PipelineConfig config, RunReportSettings? report = null, CancellationToken ct = default);

        // Info & Validation
        Task<ReportInfo?> GetReportDetailsAsync(string reportId, CancellationToken ct = default);
        Task<SupportedSourcesResponse?> GetSupportedSourcesAsync(CancellationToken ct = default);
        Task<ProcessorsResponse?> GetProcessorsAsync(CancellationToken ct = default);
        Task<ColumnProfileResponse?> ProfileColumnsAsync(object payload, CancellationToken ct = default);
    }
}
