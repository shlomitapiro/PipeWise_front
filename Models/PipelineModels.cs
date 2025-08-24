// Models/PipelineModels.cs
using System.Collections.Generic;

namespace PipeWiseClient.Models
{
    public class PipelineSummary
    {
        public string id { get; set; } = "";
        public string name { get; set; } = "";
        public string description { get; set; } = "";
        public long? updated_at { get; set; }
        public string last_status { get; set; } = "";
    }

    public class PipelinesListResponse
    {
        public List<PipelineSummary> pipelines { get; set; } = new();
        public int total_count { get; set; }
    }

    public class PipelineResponse
    {
        public string id { get; set; } = "";
        public PipelineConfig? pipeline { get; set; }
        public string message { get; set; } = "";
    }


    public class RunReportSettings
    {
        public bool generate_html { get; set; } = true;
        public bool generate_pdf  { get; set; } = true;
        public bool auto_open_html { get; set; } = false;
    }

    public class RunPipelineResult
    {
        public string status { get; set; } = "";
        public string message { get; set; } = "";
        public object? summary { get; set; }
        public object? report_paths { get; set; }
        public object? data_preview { get; set; }
        public List<Dictionary<string, object>>? sample_data { get; set; }
        public string? progress_file { get; set; }
        public object? config_echo { get; set; }
        public string? error { get; set; }
    }
}
