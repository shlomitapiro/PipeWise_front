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
        public required string Path { get; set; }
        
        // Excel specific properties
        [JsonProperty("sheet_name")]
        public string? SheetName { get; set; }
        
        [JsonProperty("header_row")]
        public int? HeaderRow { get; set; }
        
        [JsonProperty("engine")]
        public string? Engine { get; set; }
        
        // CSV specific properties
        [JsonProperty("delimiter")]
        public string? Delimiter { get; set; }
        
        // JSON specific properties
        [JsonProperty("root_key")]
        public string? RootKey { get; set; }
        
        // XML specific properties
        [JsonProperty("record_tag")]
        public string? RecordTag { get; set; }
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
        
        // JSON target specific properties
        [JsonProperty("pretty")]
        public bool? Pretty { get; set; }
    }
}