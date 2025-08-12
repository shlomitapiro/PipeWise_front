using System.Collections.Generic;
using Newtonsoft.Json;

namespace PipeWiseClient.Models
{
    public class PipelineConfig
    {
        [JsonProperty("source")]
        public required SourceConfig Source { get; set; }
        
        [JsonProperty("processors")]
        public required ProcessorConfig[] Processors { get; set; }
        
        [JsonProperty("target")]
        public required TargetConfig Target { get; set; }
    }

    public class SourceConfig
    {
        [JsonProperty("type")]
        public required string Type { get; set; }
        
        [JsonProperty("path")]
        public string? Path { get; set; }
        
        // MySQL properties
        [JsonProperty("host")]
        public string? Host { get; set; }
        
        [JsonProperty("user")]
        public string? User { get; set; }
        
        [JsonProperty("password")]
        public string? Password { get; set; }
        
        [JsonProperty("database")]
        public string? Database { get; set; }
        
        [JsonProperty("query")]
        public string? Query { get; set; }
        
        [JsonProperty("port")]
        public int? Port { get; set; }
        
        // Excel properties
        [JsonProperty("sheet_name")]
        public string? SheetName { get; set; }
        
        [JsonProperty("header_row")]
        public int? HeaderRow { get; set; }
    }

    public class ProcessorConfig
    {
        [JsonProperty("type")]
        public required string Type { get; set; }
        
        [JsonProperty("config")]
        public required Dictionary<string, object> Config { get; set; }
    }

    public class TargetConfig
    {
        [JsonProperty("type")]
        public required string Type { get; set; }
        
        [JsonProperty("path")]
        public required string Path { get; set; }
    }
}