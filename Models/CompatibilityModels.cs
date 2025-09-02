using System.Collections.Generic;
using PipeWiseClient.Models;


namespace PipeWiseClient.Models
{
    public enum IssueSeverity { Error, Warning, Info }

    public class CompatibilityIssue
    {
        public IssueSeverity Severity { get; set; } = IssueSeverity.Error;
        public string Code { get; set; } = "";        // למשל: missing_column, type_hint_numeric, validation_error
        public string? Column { get; set; }
        public string Message { get; set; } = "";
        public string? Expected { get; set; }
        public string? Actual { get; set; }
    }

    public sealed class CompatResult
    {
        public bool IsCompatible { get; set; } = true;
        public List<CompatibilityIssue> Issues { get; } = new List<CompatibilityIssue>();
        public int RequiredColumnsCount { get; set; }
        public int MissingColumnsCount { get; set; }
    }
}
