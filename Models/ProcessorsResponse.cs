using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PipeWiseClient.Models
{
    public class ProcessorInfo
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;
        
        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;
        
        [JsonPropertyName("operations")]
        public List<string> Operations { get; set; } = new();
    }
    
    public class ProcessorsResponse
    {
        [JsonPropertyName("processors")]
        public List<ProcessorInfo> Processors { get; set; } = new();
    }
}