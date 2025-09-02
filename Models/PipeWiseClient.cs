// Models for API responses, /Models/PipeWiseClient.cs
using System.Text.Json.Serialization;

namespace PipeWiseClient.Models
{
    public sealed class RunStartResponse
    {
        [JsonPropertyName("run_id")]
        public string RunId { get; set; } = "";
    }

    public sealed class RunProgressResponse
    {
        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("percent")]
        public int? Percent { get; set; }
    }

    public sealed class RunResultEnvelope
    {
        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("percent")]
        public int? Percent { get; set; }

        // This is exactly your existing RunPipelineResult shape from the server.
        [JsonPropertyName("result")]
        public RunPipelineResult? Result { get; set; }
    }
}
