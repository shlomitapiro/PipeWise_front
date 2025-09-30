using System.Linq;
using PipeWiseClient.Interfaces;
using PipeWiseClient.Models;

namespace PipeWiseClient.Services.Validators
{
    public class ColumnSettingsValidator : IValidator<ColumnSettings>
    {
        public ValidationResult Validate(ColumnSettings s)
        {
            var vr = new ValidationResult();

            bool Has(string op) => s.Operations.Any(o => string.Equals(o, op, System.StringComparison.OrdinalIgnoreCase));

            if (Has("replace_empty_values"))
            {
                if (s.ReplaceEmpty == null) vr.AddError("חסר ערך להחלפת ריקים");
                else if (string.IsNullOrWhiteSpace(s.ReplaceEmpty.Value)) vr.AddError("ערך ההחלפה לריקים אינו תקין");
            }
            if (Has("replace_null_values"))
            {
                if (s.ReplaceNull == null) vr.AddError("חסר ערך להחלפת NULL");
                else if (string.IsNullOrWhiteSpace(s.ReplaceNull.Value)) vr.AddError("ערך ההחלפה ל-NULL אינו תקין");
            }
            if (Has("set_numeric_range"))
            {
                if (s.NumericRange == null) vr.AddError("טווח נומרי לא הוגדר");
                else if (s.NumericRange.Min.HasValue && s.NumericRange.Max.HasValue && s.NumericRange.Min > s.NumericRange.Max)
                    vr.AddError("טווח נומרי שגוי (Min>Max)");
            }
            if (Has("merge_columns"))
            {
                if (s.MergeColumnsSettings == null || s.MergeColumnsSettings.SourceColumns == null || s.MergeColumnsSettings.SourceColumns.Count == 0)
                    vr.AddError("הגדרת מיזוג עמודות חסרה");
            }
            if (Has("split_field"))
            {
                if (s.SplitFieldSettings == null || s.SplitFieldSettings.TargetFields == null || s.SplitFieldSettings.TargetFields.Count == 0)
                    vr.AddError("הגדרת פיצול שדה חסרה");
            }
            if (Has("categorical_encoding"))
            {
                if (s.CategoricalEncoding == null || s.CategoricalEncoding.Mapping == null || s.CategoricalEncoding.Mapping.Count == 0)
                    vr.AddError("הגדרת קידוד קטגוריאלי חסרה");
            }
            if (Has("cast_type"))
            {
                if (s.CastType == null || string.IsNullOrWhiteSpace(s.CastType.ToType))
                    vr.AddError("חסר סוג יעד להמרה");
            }

            return vr;
        }
    }
}

