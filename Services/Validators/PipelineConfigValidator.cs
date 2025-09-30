using System;
using PipeWiseClient.Interfaces;
using PipeWiseClient.Models;

namespace PipeWiseClient.Services.Validators
{
    public class PipelineConfigValidator : IValidator<PipelineConfig>
    {
        public ValidationResult Validate(PipelineConfig item)
        {
            var vr = new ValidationResult();
            if (item.Source == null) vr.AddError("חסר הגדרת מקור", "Source");
            if (string.IsNullOrWhiteSpace(item.Source?.Type)) vr.AddError("חסר סוג מקור", "Source.Type");
            if (string.IsNullOrWhiteSpace(item.Source?.Path)) vr.AddError("חסר נתיב מקור", "Source.Path");
            if (item.Processors == null || item.Processors.Length == 0) vr.AddError("חייב להיות לפחות Processor אחד", "Processors");
            if (item.Target == null) vr.AddError("חסר הגדרת יעד", "Target");
            if (string.IsNullOrWhiteSpace(item.Target?.Type)) vr.AddError("חסר סוג יעד", "Target.Type");
            if (string.IsNullOrWhiteSpace(item.Target?.Path)) vr.AddWarning("נתיב יעד ריק - ייווצר אוטומטית", "Target.Path");
            return vr;
        }
    }
}

