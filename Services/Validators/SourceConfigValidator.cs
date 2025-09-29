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
                vr.AddError("??? ???? ?? ????", "Source.Type");

            var path = s?.Path;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                vr.AddError("???? ???? ?? ????", "Source.Path");

            if (t == "excel")
            {
                var sheet = s?.SheetName;
                if (string.IsNullOrWhiteSpace(sheet))
                    vr.AddWarning("?? ????? ?? ?????? - ????? ??????", "Source.SheetName");
            }

            if (t == "csv")
            {
                var delim = s?.Delimiter;
                if (string.IsNullOrWhiteSpace(delim))
                    vr.AddWarning("?? ????? ????? - ????? ????? ?????", "Source.Delimiter");
            }

            return vr;
        }
    }
}
