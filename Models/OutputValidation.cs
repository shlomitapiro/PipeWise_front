using System.Collections.Generic;
using Newtonsoft.Json;

namespace PipeWiseClient.Models
{
    public class OutputValidation
    {
        [JsonProperty("passed")] public bool Passed { get; set; }

        [JsonProperty("errors")] public List<RuleError> Errors { get; set; } = new();

        [JsonProperty("warnings")] public object? Warnings { get; set; }

        [JsonProperty("stats")] public ValidationStats? Stats { get; set; }

        [JsonProperty("expected_columns")] public List<string> ExpectedColumns { get; set; } = new();

        [JsonProperty("actual_columns")] public List<string> ActualColumns { get; set; } = new();

        [JsonProperty("missing_columns")] public List<string> MissingColumns { get; set; } = new();

        [JsonProperty("extra_columns")] public List<string> ExtraColumns { get; set; } = new();
    }

    public class RuleError
    {
        [JsonProperty("rule")] public string Rule { get; set; } = "";
        [JsonProperty("message")] public string Message { get; set; } = "";
    }

    public class ValidationStats
    {
        [JsonProperty("rows_checked")] public int RowsChecked { get; set; }
        [JsonProperty("columns_checked")] public int ColumnsChecked { get; set; }
        [JsonProperty("file_size_mb")] public double FileSizeMb { get; set; }
        [JsonProperty("target_path")] public string? TargetPath { get; set; }
    }
}
