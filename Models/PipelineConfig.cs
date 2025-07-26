using System.Collections.Generic;
using Newtonsoft.Json;

namespace PipeWiseClient.Models
{
    public class PipelineConfig
    {
        [JsonProperty("source")]
        public SourceConfig Source { get; set; }
        
        [JsonProperty("processors")]
        public ProcessorConfig[] Processors { get; set; }
        
        [JsonProperty("target")]
        public TargetConfig Target { get; set; }
    }

    public class SourceConfig
    {
        [JsonProperty("type")]
        public string Type { get; set; }
        
        [JsonProperty("path")]
        public string Path { get; set; }
    }

    public class ProcessorConfig
    {
        [JsonProperty("type")]
        public string Type { get; set; }
        
        [JsonProperty("config")]
        public Dictionary<string, object> Config { get; set; }
    }

    public class TargetConfig
    {
        [JsonProperty("type")]
        public string Type { get; set; }
        
        [JsonProperty("path")]
        public string Path { get; set; }
    }
}