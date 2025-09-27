using System;
using System.Collections.Generic;

namespace PipeWiseClient.Models
{
    public class DateFormatApplySettings
    {
        public string TargetFormat { get; set; } = "%Y-%m-%d";
        public string? OutputAs { get; set; }
    }

    public class ColumnSettings
    {
        public HashSet<string> Operations { get; set; } = new HashSet<string>();
        public string InferredType { get; set; } = string.Empty;
        public DateValidationSettings? DateValidationSettings { get; set; }
        public ReplaceEmptySettings? ReplaceEmpty { get; set; }
        public ReplaceEmptySettings? ReplaceNull { get; set; }
        public NumericRangeSettings? NumericRange { get; set; }
        public DateFormatApplySettings? DateFormatApply { get; set; }
        public InvalidDateRemovalSettings? InvalidDateRemoval { get; set; }
        public IdentifierValidationSettings? IdentifierValidation { get; set; }
        public NormalizeSettings? NormalizeSettings { get; set; }
        public RenameSettings? RenameSettings { get; set; }
        public MergeColumnsSettings? MergeColumnsSettings { get; set; }
        public SplitFieldSettings? SplitFieldSettings { get; set; }
        public CategoricalEncodingConfig? CategoricalEncoding { get; set; }
        public CastTypeSettings? CastType { get; set; }
        public string? ColumnName { get; set; }
        public bool RemoveColumn { get; set; } = false;
    }

    public class NumericRangeSettings
    {
        public double? Min { get; set; }
        public double? Max { get; set; }
        public string ActionOnViolation { get; set; } = "remove";
        public double? ReplacementValue { get; set; }
    }

    public class ReplaceEmptySettings
    {
        public string? Value { get; set; }
        public int MaxLength { get; set; } = 255;
    }

    public class DateValidationSettings
    {
        public string Action { get; set; } = "remove_row";
        public DateTime? ReplacementDate { get; set; }
        public string DateFormat { get; set; } = "dd/MM/yyyy";
    }

    public class InvalidDateRemovalSettings
    {
        public int? MinYear { get; set; }
        public int? MaxYear { get; set; }
        public string EmptyAction { get; set; } = "remove"; // remove | replace
        public string? EmptyReplacement { get; set; }
        public string? MinDateIso { get; set; }
        public string? MaxDateIso { get; set; }
    }

    public class IdentifierValidationSettings
    {
        public string IdType { get; set; } = "numeric"; // numeric | string | uuid
        public bool TreatWhitespaceAsEmpty { get; set; } = true;

        public string EmptyAction { get; set; } = "remove"; // remove | replace
        public string? EmptyReplacement { get; set; }

        public NumericIdentifierOptions? Numeric { get; set; }
        public StringIdentifierOptions? String { get; set; }
        public UuidIdentifierOptions? Uuid { get; set; }
    }

    public class NumericIdentifierOptions
    {
        public bool IntegerOnly { get; set; } = true;
        public bool AllowLeadingZeros { get; set; } = true;
        public bool AllowNegative { get; set; } = false;
        public bool AllowThousandSeparators { get; set; } = false;
        public int? MaxDigits { get; set; } = 20;
    }

    public class StringIdentifierOptions
    {
        public int MinLength { get; set; } = 1;
        public int? MaxLength { get; set; } = null;
        public bool DisallowWhitespace { get; set; } = false;
        public string? Regex { get; set; } = null;
    }

    public class UuidIdentifierOptions
    {
        public bool AcceptHyphenated { get; set; } = true;
        public bool AcceptBraced { get; set; } = false;
        public bool AcceptUrn { get; set; } = false;
    }

    public class NormalizeSettings
    {
        public string? TargetField { get; set; }
    }

    public class RenameSettings
    {
        public string NewName { get; set; } = string.Empty;
    }

    public class MergeColumnsSettings
    {
        public List<string> SourceColumns { get; set; } = new List<string>();
        public string TargetColumn { get; set; } = string.Empty;
        public string Separator { get; set; } = " ";
        public bool RemoveSourceColumns { get; set; } = false;
        public string EmptyHandling { get; set; } = "skip";
        public string EmptyReplacement { get; set; } = string.Empty;
    }

    public class SplitFieldSettings
    {
        public string SplitType { get; set; } = "delimiter";
        public string Delimiter { get; set; } = ",";
        public int Length { get; set; } = 3;
        public List<string> TargetFields { get; set; } = new();
        public bool RemoveSource { get; set; } = true;
    }

    public class CategoricalEncodingConfig
    {
        public string Field { get; set; } = string.Empty;
        public Dictionary<string, int> Mapping { get; set; } = new();
        public string? TargetField { get; set; }
        public bool ReplaceOriginal { get; set; } = true;
        public bool DeleteOriginal { get; set; } = false;
        public int DefaultValue { get; set; } = -1;
    }

    public class CastTypeSettings
    {
        public string? ToType { get; set; }
    }

}

