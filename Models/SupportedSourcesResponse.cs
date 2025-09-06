using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PipeWiseClient.Models
{
    public class SupportedSourcesResponse
    {
        [JsonPropertyName("supported_sources")]
        public List<string> SupportedSources { get; set; } = new();
        
        [JsonPropertyName("total_types")]
        public int TotalTypes { get; set; }
        
        [JsonPropertyName("file_based")]
        public List<string> FileBased { get; set; } = new();
        
        [JsonPropertyName("note")]
        public string? Note { get; set; }
    }
}