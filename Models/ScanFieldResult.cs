using System.Collections.Generic;

namespace PipeWiseClient.Models
{
    public class ScanFieldResult
    {
        public string FieldName { get; set; } = string.Empty;
        public List<string> UniqueValues { get; set; } = new();
        public int TotalRows { get; set; }
        public int NullCount { get; set; }
        public bool FieldExists { get; set; }
        public bool Truncated { get; set; }
        public List<string> AvailableFields { get; set; } = new();
        public string Message { get; set; } = string.Empty;
    }
}

