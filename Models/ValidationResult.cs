using System.Collections.Generic;

namespace PipeWiseClient.Models
{
    public class ValidationResult
    {
        public bool IsValid => Errors.Count == 0;
        public List<ValidationError> Errors { get; } = new();
        public List<ValidationWarning> Warnings { get; } = new();

        public void AddError(string message, string? field = null)
            => Errors.Add(new ValidationError { Message = message, Field = field });

        public void AddWarning(string message, string? field = null)
            => Warnings.Add(new ValidationWarning { Message = message, Field = field });
    }

    public class ValidationError
    {
        public string Message { get; set; } = string.Empty;
        public string? Field { get; set; }
    }

    public class ValidationWarning
    {
        public string Message { get; set; } = string.Empty;
        public string? Field { get; set; }
    }
}

