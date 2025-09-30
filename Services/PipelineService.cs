using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using PipeWiseClient.Interfaces;
using PipeWiseClient.Models;

namespace PipeWiseClient.Services
{
    public class PipelineService : IPipelineService
    {
        private readonly IApiClient _api;
        private readonly INotificationService _notifications;
        private readonly IFileService _fileService;
        private readonly ColumnOperationRegistry _registry;
        private readonly IPipelineRepository _repository;
        private readonly PipeWiseClient.Interfaces.IValidator<PipelineConfig> _configValidator;

        public PipelineService(IApiClient api, INotificationService notifications, IFileService fileService, ColumnOperationRegistry registry, IPipelineRepository repository, PipeWiseClient.Interfaces.IValidator<PipelineConfig> configValidator)
        {
            _api = api ?? throw new ArgumentNullException(nameof(api));
            _notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
            _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _configValidator = configValidator ?? throw new ArgumentNullException(nameof(configValidator));
        }

        // Duplicate-run prevention (client-side): track only concrete in-flight work
        private readonly object _runSync = new object();
        private Task<RunPipelineResult>? _inFlightRunTask = null;

        private static readonly string[] DATE_INPUT_FORMATS = new[]
        {
            "%d-%m-%Y", "%d/%m/%Y", "%d.%m.%Y",
            "%Y-%m-%d", "%Y/%m/%d", "%m/%d/%Y", "%m-%d-%Y",
            "%d-%m-%Y %H:%M:%S", "%d/%m/%Y %H:%M:%S", "%d.%m.%Y %H:%M:%S",
            "%Y-%m-%d %H:%M:%S", "%Y-%m-%dT%H:%M:%S", "%m/%d/%Y %H:%M:%S",
            "%d-%m-%y", "%d/%m/%y", "%d.%m.%y",
            "%y-%m-%d", "%y/%m/%d", "%m/%d/%y",
            "%d-%m-%y %H:%M:%S", "%d/%m/%y %H:%M:%S", "%y-%m-%d %H:%M:%S",
        };

        public PipelineConfig BuildConfig(string filePath, Dictionary<string, ColumnSettings> columnSettings, string targetType, string outputDirectory)
            => BuildConfig(filePath, columnSettings, targetType, outputDirectory, removeEmptyRows: false, removeDuplicates: false, stripWhitespace: false, columnNames: null);

        public PipelineConfig BuildConfig(
            string filePath,
            Dictionary<string, ColumnSettings> columnSettings,
            string targetType,
            string outputDirectory,
            bool removeEmptyRows,
            bool removeDuplicates,
            bool stripWhitespace,
            List<string>? columnNames)
        {
            if (string.IsNullOrWhiteSpace(filePath)) throw new InvalidOperationException("No source file selected");

            var cleaningOps = new List<object>();
            var transformOps = new List<object>();
            var aggregationOps = new List<object>();

            // Global operations first
            if (removeEmptyRows)
                cleaningOps.Add(new Dictionary<string, object> { ["action"] = "remove_empty_rows" });
            if (removeDuplicates)
                cleaningOps.Add(new Dictionary<string, object> { ["action"] = "remove_duplicates" });
            if (stripWhitespace)
            {
                var fields = (columnNames != null && columnNames.Count > 0) ? columnNames.ToArray() : Array.Empty<string>();
                if (fields.Length > 0)
                {
                    cleaningOps.Add(new Dictionary<string, object>
                    {
                        ["action"] = "strip_whitespace",
                        ["fields"] = fields
                    });
                }
            }

            var columnsToRemove = new List<string>();
            foreach (var kvp in columnSettings)
            {
                if (kvp.Value.RemoveColumn)
                    columnsToRemove.Add(kvp.Key);
            }
            if (columnsToRemove.Count > 0)
            {
                foreach (var col in columnsToRemove)
                {
                    cleaningOps.Add(new Dictionary<string, object> { ["action"] = "remove_column", ["column"] = col });
                }
            }

            foreach (var kvp in columnSettings)
            {
                var columnName = kvp.Key;
                var settings = kvp.Value;

                foreach (var operation in settings.Operations.ToList())
                {
                    var strategy = _registry.GetStrategy(operation);
                    if (strategy != null)
                    {
                        try
                        {
                            var built = strategy.BuildServerConfig(columnName, settings);
                            var cat = strategy.Category.ToLowerInvariant();
                            if (cat == "transform") transformOps.Add(built);
                            else if (cat == "aggregation") aggregationOps.Add(built);
                            else cleaningOps.Add(built);
                        }
                        catch (Exception ex)
                        {
                            _notifications.Warning("אזהרה", $"שגיאה בבניית פעולה '{operation}': {ex.Message}");
                        }
                        continue;
                    }
                    var opDict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase) { ["action"] = operation };

                    if (string.Equals(operation, "replace_empty_values", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(operation, "replace_null_values", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(operation, "set_numeric_range", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(operation, "set_date_format", StringComparison.OrdinalIgnoreCase))
                    {
                        opDict["field"] = columnName;
                    }

                    if (string.Equals(operation, "set_numeric_range", StringComparison.OrdinalIgnoreCase) && settings.NumericRange is not null)
                    {
                        if (settings.NumericRange.Min.HasValue) opDict["min_value"] = settings.NumericRange.Min.Value;
                        if (settings.NumericRange.Max.HasValue) opDict["max_value"] = settings.NumericRange.Max.Value;
                        opDict["action_on_violation"] = string.IsNullOrWhiteSpace(settings.NumericRange.ActionOnViolation) ? "remove" : settings.NumericRange.ActionOnViolation;
                        if (string.Equals(settings.NumericRange.ActionOnViolation, "replace", StringComparison.OrdinalIgnoreCase) && settings.NumericRange.ReplacementValue.HasValue)
                            opDict["replacement_value"] = settings.NumericRange.ReplacementValue.Value;
                    }

                    if (string.Equals(operation, "set_date_format", StringComparison.OrdinalIgnoreCase))
                    {
                        var fmt = settings.DateFormatApply?.TargetFormat;
                        opDict["input_formats"] = DATE_INPUT_FORMATS;
                        if (!string.IsNullOrWhiteSpace(settings.DateFormatApply?.OutputAs))
                            opDict["output_as"] = settings.DateFormatApply!.OutputAs!;
                        if (!string.IsNullOrWhiteSpace(fmt)) opDict["target_format"] = fmt; else opDict["target_format"] = "%Y-%m-%d";
                        opDict["action_on_violation"] = "warn";
                    }

                    if (string.Equals(operation, "remove_invalid_dates", StringComparison.OrdinalIgnoreCase))
                    {
                        var s = settings.InvalidDateRemoval;
                        opDict["field"] = columnName;
                        opDict["action"] = "remove_invalid_dates";
                        opDict["input_formats"] = DATE_INPUT_FORMATS;
                        if (s != null)
                        {
                            if (s.MinYear.HasValue) opDict["min_year"] = s.MinYear.Value;
                            if (s.MaxYear.HasValue) opDict["max_year"] = s.MaxYear.Value;
                            if (!string.IsNullOrWhiteSpace(s.MinDateIso)) opDict["min_date"] = s.MinDateIso!;
                            if (!string.IsNullOrWhiteSpace(s.MaxDateIso)) opDict["max_date"] = s.MaxDateIso!;
                            opDict["empty_action"] = string.IsNullOrWhiteSpace(s.EmptyAction) ? "remove" : s.EmptyAction;
                            if (string.Equals(s.EmptyAction, "replace", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(s.EmptyReplacement))
                                opDict["empty_replacement"] = s.EmptyReplacement!;
                        }
                        opDict["treat_whitespace_as_empty"] = true;
                        cleaningOps.Add(opDict);
                        continue;
                    }

                    if (string.Equals(operation, "replace_empty_values", StringComparison.OrdinalIgnoreCase) && settings.ReplaceEmpty is not null)
                    {
                        if (!string.IsNullOrWhiteSpace(settings.ReplaceEmpty.Value)) opDict["replacement_value"] = settings.ReplaceEmpty.Value!;
                        opDict["expected_type"] = string.IsNullOrWhiteSpace(settings.InferredType) ? "string" : settings.InferredType.ToLowerInvariant();
                        opDict["max_length"] = settings.ReplaceEmpty.MaxLength <= 0 ? 255 : settings.ReplaceEmpty.MaxLength;
                    }

                    if (string.Equals(operation, "replace_null_values", StringComparison.OrdinalIgnoreCase) && settings.ReplaceNull is not null)
                    {
                        if (!string.IsNullOrWhiteSpace(settings.ReplaceNull.Value)) opDict["replacement_value"] = settings.ReplaceNull.Value!;
                        opDict["expected_type"] = string.IsNullOrWhiteSpace(settings.InferredType) ? "string" : settings.InferredType.ToLowerInvariant();
                        opDict["max_length"] = settings.ReplaceNull.MaxLength <= 0 ? 255 : settings.ReplaceNull.MaxLength;
                        opDict["null_definitions"] = new[] { "null", "n/a", "none", "undefined", "UNDEFINED", "NULL", "N/A", "NONE" };
                    }

                    if (string.Equals(operation, "remove_invalid_identifier", StringComparison.OrdinalIgnoreCase))
                    {
                        var s = settings.IdentifierValidation;
                        var op = new Dictionary<string, object>
                        {
                            ["action"] = "remove_invalid_identifier",
                            ["field"] = columnName
                        };
                        if (s != null)
                        {
                            op["id_type"] = string.IsNullOrWhiteSpace(s.IdType) ? "numeric" : s.IdType;
                            op["treat_whitespace_as_empty"] = s.TreatWhitespaceAsEmpty;
                            op["empty_action"] = string.IsNullOrWhiteSpace(s.EmptyAction) ? "remove" : s.EmptyAction;
                            if (string.Equals(s.EmptyAction, "replace", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(s.EmptyReplacement))
                                op["empty_replacement"] = s.EmptyReplacement!;
                            if (s.IdType == "numeric" && s.Numeric != null)
                            {
                                op["numeric"] = new Dictionary<string, object?>
                                {
                                    ["integer_only"] = s.Numeric.IntegerOnly,
                                    ["allow_leading_zeros"] = s.Numeric.AllowLeadingZeros,
                                    ["allow_negative"] = s.Numeric.AllowNegative,
                                    ["allow_thousand_separators"] = s.Numeric.AllowThousandSeparators,
                                    ["max_digits"] = s.Numeric.MaxDigits
                                };
                            }
                            else if (s.IdType == "string" && s.String != null)
                            {
                                op["string"] = new Dictionary<string, object?>
                                {
                                    ["min_length"] = s.String.MinLength,
                                    ["max_length"] = s.String.MaxLength,
                                    ["disallow_whitespace"] = s.String.DisallowWhitespace,
                                    ["regex"] = string.IsNullOrWhiteSpace(s.String.Regex) ? null : s.String.Regex
                                };
                            }
                            else if (s.IdType == "uuid" && s.Uuid != null)
                            {
                                op["uuid"] = new Dictionary<string, object?>
                                {
                                    ["accept_hyphenated"] = s.Uuid.AcceptHyphenated,
                                    ["accept_braced"] = s.Uuid.AcceptBraced,
                                    ["accept_urn"] = s.Uuid.AcceptUrn
                                };
                            }
                        }
                        else
                        {
                            op["id_type"] = "numeric";
                            op["treat_whitespace_as_empty"] = true;
                            op["empty_action"] = "remove";
                        }
                        cleaningOps.Add(op);
                        continue;
                    }

                    if (string.Equals(operation, "normalize_numeric", StringComparison.OrdinalIgnoreCase))
                    {
                        opDict["field"] = columnName;
                        if (!string.IsNullOrWhiteSpace(settings.NormalizeSettings?.TargetField))
                            opDict["target_field"] = settings.NormalizeSettings!.TargetField!;
                    }

                    if (string.Equals(operation, "rename_field", StringComparison.OrdinalIgnoreCase))
                    {
                        var s = settings.RenameSettings;
                        if (s != null && !string.IsNullOrWhiteSpace(s.NewName))
                        {
                            opDict["field"] = columnName;
                            opDict["action"] = "rename_field";
                            opDict["old_name"] = columnName;
                            opDict["new_name"] = s.NewName;
                        }
                        else continue;
                    }

                    if (string.Equals(operation, "merge_columns", StringComparison.OrdinalIgnoreCase))
                    {
                        var s = settings.MergeColumnsSettings;
                        if (s != null && s.SourceColumns.Count >= 2 && !string.IsNullOrWhiteSpace(s.TargetColumn))
                        {
                            opDict["action"] = "merge_columns";
                            opDict["source_columns"] = s.SourceColumns.ToArray();
                            opDict["target_column"] = s.TargetColumn;
                            opDict["separator"] = s.Separator;
                            opDict["remove_source"] = s.RemoveSourceColumns;
                            opDict["handle_empty"] = s.EmptyHandling;
                            if (!string.IsNullOrWhiteSpace(s.EmptyReplacement)) opDict["empty_replacement"] = s.EmptyReplacement;
                        }
                        else continue;
                    }

                    if (string.Equals(operation, "split_field", StringComparison.OrdinalIgnoreCase))
                    {
                        var s = settings.SplitFieldSettings;
                        if (s != null && s.TargetFields.Count > 0)
                        {
                            opDict["action"] = "split_field";
                            opDict["source_field"] = columnName;
                            opDict["split_type"] = s.SplitType;
                            if (s.SplitType == "delimiter") opDict["delimiter"] = s.Delimiter; else if (s.SplitType == "fixed_length") opDict["length"] = s.Length;
                            opDict["target_fields"] = s.TargetFields.ToArray();
                            opDict["remove_source"] = s.RemoveSource;
                        }
                        else continue;
                    }

                    if (string.Equals(operation, "categorical_encoding", StringComparison.OrdinalIgnoreCase))
                    {
                        var c = settings.CategoricalEncoding;
                        if (c != null && c.Mapping.Count > 0)
                        {
                            opDict["action"] = "categorical_encoding";
                            opDict["field"] = columnName;
                            opDict["mapping"] = c.Mapping;
                            opDict["replace_original"] = c.ReplaceOriginal;
                            opDict["delete_original"] = c.DeleteOriginal;
                            opDict["default_value"] = c.DefaultValue;
                            if (!c.ReplaceOriginal && !string.IsNullOrWhiteSpace(c.TargetField)) opDict["target_field"] = c.TargetField!;
                            transformOps.Add(opDict);
                            continue;
                        }
                        else continue;
                    }

                    if (string.Equals(operation, "cast_type", StringComparison.OrdinalIgnoreCase))
                    {
                        opDict["field"] = columnName;
                        if (settings.CastType != null && !string.IsNullOrWhiteSpace(settings.CastType.ToType)) opDict["to_type"] = settings.CastType.ToType!; else continue;
                    }

                    if (string.Equals(operation, "strip_whitespace", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(operation, "to_uppercase", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(operation, "to_lowercase", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(operation, "remove_special_characters", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(operation, "remove_empty_values", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(operation, "remove_null_values", StringComparison.OrdinalIgnoreCase))
                    {
                        opDict["fields"] = new[] { columnName };
                    }
                    else if (!opDict.ContainsKey("field"))
                    {
                        opDict["column"] = columnName;
                    }

                    if (operation.StartsWith("remove_") || operation.StartsWith("replace_") || operation == "strip_whitespace" || operation == "set_numeric_range" || operation == "set_date_format")
                        cleaningOps.Add(opDict);
                    else if (operation == "cast_type" || operation == "normalize_numeric" || operation == "rename_field" || operation == "merge_columns" || operation == "split_field" || operation == "categorical_encoding" || operation == "to_uppercase" || operation == "to_lowercase")
                        transformOps.Add(opDict);
                    else if (operation.StartsWith("validate_") || operation == "required_fields")
                        cleaningOps.Add(opDict);
                    else if (operation is "sum" or "average" or "min" or "max" or "median" or "std" or "variance" or "range" or "count_valid" or "count_distinct" or "most_common")
                        aggregationOps.Add(opDict);
                    else if (!cleaningOps.Contains(opDict) && !transformOps.Contains(opDict) && !aggregationOps.Contains(opDict))
                        cleaningOps.Add(opDict);
                }
            }

            var processors = new List<ProcessorConfig>();
            if (cleaningOps.Count > 0)
                processors.Add(new ProcessorConfig { Type = "cleaner", Config = new Dictionary<string, object> { ["operations"] = cleaningOps } });
            if (transformOps.Count > 0)
                processors.Add(new ProcessorConfig { Type = "transformer", Config = new Dictionary<string, object> { ["operations"] = transformOps } });
            if (aggregationOps.Count > 0)
                processors.Add(new ProcessorConfig { Type = "aggregator", Config = new Dictionary<string, object> { ["operations"] = aggregationOps } });

            var sourceType = _fileService.GetFileType(filePath);
            var src = new SourceConfig { Type = sourceType, Path = filePath };

            var ext = targetType switch { "json" => "json", "xml" => "xml", "excel" or "xlsx" => "xlsx", _ => "csv" };
            var baseName = Path.GetFileNameWithoutExtension(filePath);
            var absoluteTargetPath = Path.Combine(outputDirectory, $"{baseName}.{ext}");
            var target = new TargetConfig { Type = targetType, Path = absoluteTargetPath };

            return new PipelineConfig { Source = src, Processors = processors.ToArray(), Target = target };
        }
        public async Task<RunPipelineResult> ExecuteAsync(PipelineConfig config, IProgress<(string Status, int Percent)>? progress = null, CancellationToken cancellationToken = default)
        {
            Task<RunPipelineResult>? localTaskRef = null;
            lock (_runSync)
            {
                if (_inFlightRunTask != null && !_inFlightRunTask.IsCompleted && !_inFlightRunTask.IsCanceled)
                {
                    System.Diagnostics.Debug.WriteLine("ExecuteAsync: duplicate blocked (active in-flight task detected)");
                    throw new InvalidOperationException("Run blocked: active in-flight task detected. Wait for the current run to finish.");
                }
                _inFlightRunTask = Task.FromResult(new RunPipelineResult { status = "starting", message = "guard-placeholder" });
            }

            try
            {
                localTaskRef = _api.RunWithProgressAsync(config, progress, TimeSpan.FromMilliseconds(500), cancellationToken);
                lock (_runSync) { _inFlightRunTask = localTaskRef; }

                var result = await localTaskRef;
                return result;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                lock (_runSync)
                {
                    if (_inFlightRunTask == localTaskRef || _inFlightRunTask == null || _inFlightRunTask.IsCompleted || _inFlightRunTask.IsCanceled || _inFlightRunTask.IsFaulted)
                    {
                        _inFlightRunTask = null;
                        System.Diagnostics.Debug.WriteLine("ExecuteAsync: cleared in-flight guard");
                    }
                }
            }
        }
        public async Task<PipelineConfig?> LoadConfigAsync(string configPath)
            => await _repository.LoadAsync(configPath);

        public async Task SaveConfigAsync(PipelineConfig config, string configPath)
            => await _repository.SaveAsync(config, configPath);

        public bool ValidateConfig(PipelineConfig config, out List<string> errors)
        {
            var result = _configValidator.Validate(config);
            errors = new List<string>();
            foreach (var err in result.Errors) errors.Add(err.Message);
            return result.IsValid;
        }
    }
}




