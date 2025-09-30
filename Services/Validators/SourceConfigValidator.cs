using System.IO;
using PipeWiseClient.Interfaces;
using PipeWiseClient.Models;

namespace PipeWiseClient.Services.Validators
{
    public class SourceConfigValidator : IValidator<SourceConfig>
    {
        public ValidationResult Validate(SourceConfig s)
        {
            var vr = new ValidationResult();
            var t = (s?.Type ?? "").ToLowerInvariant();
            if (t is not ("csv" or "json" or "excel" or "xml"))
                vr.AddError("סוג מקור לא נתמך", "Source.Type");

            var path = s?.Path;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                vr.AddError("נתיב קובץ לא קיים", "Source.Path");

            if (t == "excel")
            {
                var sheet = s?.SheetName;
                if (string.IsNullOrWhiteSpace(sheet))
                    vr.AddWarning("לא צוין שם גיליון - ייבחר הראשון", "Source.SheetName");
            }

            if (t == "csv")
            {
                var delim = s?.Delimiter;
                if (string.IsNullOrWhiteSpace(delim))
                    vr.AddWarning("לא צוין מפריד - ייבחר פסיק כברירת מחדל", "Source.Delimiter");
            }

            return vr;
        }
    }
}