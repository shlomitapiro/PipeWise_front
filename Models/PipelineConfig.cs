using System.Collections.Generic;

namespace PipeWiseClient.Models
{
    public class PipelineConfig
    {
        public SourceConfig Source { get; set; }
        public ProcessorConfig[] Processors { get; set; }
        public TargetConfig Target { get; set; }
    }

    public class SourceConfig
    {
        public string Type { get; set; }
        public string Path { get; set; }
    }

    public class ProcessorConfig
    {
        public string Type { get; set; }
        public Dictionary<string, object> Config { get; set; }
    }

    public class TargetConfig
    {
        public string Type { get; set; }
        public string Path { get; set; }
    }
}
