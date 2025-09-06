using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PipeWiseClient.Models
{
    public class ColumnProfile
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
        
        [JsonPropertyName("inferred_type")]
        public string InferredType { get; set; } = string.Empty;
        
        [JsonPropertyName("null_pct")]
        public double NullPercentage { get; set; }
        
        [JsonPropertyName("samples")]
        public List<object> Samples { get; set; } = new();
    }
    
    public class ColumnProfileResponse
    {
        [JsonPropertyName("columns")]
        public List<ColumnProfile> Columns { get; set; } = new();
        
        [JsonPropertyName("sample_size")]
        public int SampleSize { get; set; }
        
        [JsonPropertyName("total_rows")]
        public int TotalRows { get; set; }
    }
}