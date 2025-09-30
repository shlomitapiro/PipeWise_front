using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PipeWiseClient.Models;

namespace PipeWiseClient.Interfaces
{
    public interface IPipelineService
    {
        PipelineConfig BuildConfig(string filePath, Dictionary<string, ColumnSettings> columnSettings, string targetType, string outputDirectory);
        PipelineConfig BuildConfig(string filePath, Dictionary<string, ColumnSettings> columnSettings, string targetType, string outputDirectory, bool removeEmptyRows, bool removeDuplicates, bool stripWhitespace, System.Collections.Generic.List<string>? columnNames);
        Task<RunPipelineResult> ExecuteAsync(PipelineConfig config, IProgress<(string Status, int Percent)>? progress = null, CancellationToken cancellationToken = default);
        Task<PipelineConfig?> LoadConfigAsync(string configPath);
        Task SaveConfigAsync(PipelineConfig config, string configPath);
        bool ValidateConfig(PipelineConfig config, out List<string> errors);
    }
}
