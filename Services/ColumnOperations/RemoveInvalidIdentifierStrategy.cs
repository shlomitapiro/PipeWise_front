using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using PipeWiseClient.Interfaces;
using PipeWiseClient.Models;

namespace PipeWiseClient.Services.ColumnOperations
{
    public class RemoveInvalidIdentifierStrategy : IColumnOperationStrategy
    {
        public string OperationName => "remove_invalid_identifier";
        public string DisplayName => "הסרת מזהה לא תקין";
        public string Description => "מסיר שורות עם מזהה לא תקין על פי כללים";
        public string Category => "Validation";
        public bool RequiresConfiguration => false;

        public Task<bool> ConfigureAsync(string columnName, ColumnSettings settings, Window owner, System.Collections.Generic.IReadOnlyList<string> availableColumns, string filePath)
        {
            // Defaults can be customized later via a dedicated dialog
            settings.IdentifierValidation ??= new IdentifierValidationSettings();
            return Task.FromResult(true);
        }

        public Dictionary<string, object> BuildServerConfig(string columnName, ColumnSettings settings)
        {
            var s = settings.IdentifierValidation;
            var op = new Dictionary<string, object>
            {
                ["action"] = "remove_invalid_identifier",
                ["field"] = columnName,
                ["id_type"] = string.IsNullOrWhiteSpace(s?.IdType) ? "numeric" : s!.IdType,
                ["treat_whitespace_as_empty"] = s?.TreatWhitespaceAsEmpty ?? true,
                ["empty_action"] = string.IsNullOrWhiteSpace(s?.EmptyAction) ? "remove" : s!.EmptyAction!
            };
            if (s?.EmptyAction == "replace" && !string.IsNullOrWhiteSpace(s.EmptyReplacement))
                op["empty_replacement"] = s.EmptyReplacement!;
            if (s?.IdType == "numeric" && s.Numeric != null)
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
            else if (s?.IdType == "string" && s.String != null)
            {
                op["string"] = new Dictionary<string, object?>
                {
                    ["min_length"] = s.String.MinLength,
                    ["max_length"] = s.String.MaxLength,
                    ["disallow_whitespace"] = s.String.DisallowWhitespace,
                    ["regex"] = string.IsNullOrWhiteSpace(s.String.Regex) ? null : s.String.Regex
                };
            }
            else if (s?.IdType == "uuid" && s.Uuid != null)
            {
                op["uuid"] = new Dictionary<string, object?>
                {
                    ["accept_hyphenated"] = s.Uuid.AcceptHyphenated,
                    ["accept_braced"] = s.Uuid.AcceptBraced,
                    ["accept_urn"] = s.Uuid.AcceptUrn
                };
            }
            return op;
        }
    }
}
