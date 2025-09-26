using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace PipeWiseClient.Windows
{
    public partial class RemoveInvalidIdentifierDialog : Window
    {
        public string IdType { get; private set; } = "numeric"; // numeric | string | uuid
        public bool TreatWhitespaceAsEmpty { get; private set; } = true;

        public string EmptyAction { get; private set; } = "remove"; // remove | replace
        public string? EmptyReplacement { get; private set; }
        public bool NumIntegerOnly => NumIntegerOnlyBox.IsChecked == true;
        public bool NumAllowLeadingZeros => NumAllowLeadingZerosBox.IsChecked == true;
        public bool NumAllowNegative => NumAllowNegativeBox.IsChecked == true;
        public bool NumAllowThousandSeparators => NumAllowThousandSepBox.IsChecked == true;
        public int? NumMaxDigits
        {
            get
            {
                var s = (NumMaxDigitsBox.Text ?? "").Trim();
                if (string.IsNullOrEmpty(s)) return null;
                if (int.TryParse(s, out var n) && n > 0) return n;
                return null;
            }
        }
        public int StrMinLength
        {
            get
            {
                var s = (StrMinLenBox.Text ?? "").Trim();
                if (int.TryParse(s, out var n) && n >= 0) return n;
                return 1;
            }
        }
        public int? StrMaxLength
        {
            get
            {
                var s = (StrMaxLenBox.Text ?? "").Trim();
                if (string.IsNullOrEmpty(s)) return null;
                if (int.TryParse(s, out var n) && n >= 0) return n;
                return null;
            }
        }
        public bool StrDisallowWhitespace => StrDisallowWsBox.IsChecked == true;
        public string? StrRegex => string.IsNullOrWhiteSpace(StrRegexBox.Text) ? null : StrRegexBox.Text.Trim();
        public bool UuidAcceptHyphenated => UuidAcceptHyphenBox.IsChecked == true;
        public bool UuidAcceptBraced => UuidAcceptBracedBox.IsChecked == true;
        public bool UuidAcceptUrn => UuidAcceptUrnBox.IsChecked == true;
        public RemoveInvalidIdentifierDialog(string columnName)
        {
            InitializeComponent();
            Title = $"הסר מזהה לא חוקי – '{columnName}'";
            Loaded += (_, __) => IdTypeCombo_SelectionChanged(IdTypeCombo, null);
        }

        private void IdTypeCombo_SelectionChanged(object? sender, SelectionChangedEventArgs? e)
        {
            if (!IsLoaded || NumericPanel == null || StringPanel == null || UuidPanel == null) return;

            var tag = (IdTypeCombo?.SelectedItem as ComboBoxItem)?.Tag as string ?? "numeric";
            IdType = tag;

            NumericPanel.Visibility = tag == "numeric" ? Visibility.Visible : Visibility.Collapsed;
            StringPanel.Visibility = tag == "string" ? Visibility.Visible : Visibility.Collapsed;
            UuidPanel.Visibility = tag == "uuid" ? Visibility.Visible : Visibility.Collapsed;
        }

        private void EmptyRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (EmptyReplacementBox == null || EmptyRemoveRadio == null || EmptyReplaceRadio == null) return;
            EmptyReplacementBox.IsEnabled = EmptyReplaceRadio.IsChecked == true;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            TreatWhitespaceAsEmpty = TreatWsAsEmptyBox.IsChecked == true;

            if (EmptyReplaceRadio.IsChecked == true)
            {
                EmptyAction = "replace";
                var repl = (EmptyReplacementBox.Text ?? "").Trim();
                if (string.IsNullOrEmpty(repl))
                {
                    MessageBox.Show(this, "בחרת 'החלף בערך', אך לא הזנת ערך.", "שגיאת קלט",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                // ולידציה בסיסית נגד סוג המזהה
                if (!IsValidReplacementForType(repl))
                {
                    MessageBox.Show(this, "הערך החלופי אינו חוקי לפי סוג המזהה שנבחר.", "שגיאת קלט",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                EmptyReplacement = repl;
            }
            else
            {
                EmptyAction = "remove";
                EmptyReplacement = null;
            }

            // ולידציה בסיסית של שדות מתקדמים
            if (IdType == "numeric")
            {
                if (NumMaxDigitsBox.Text?.Trim().Length > 0 && NumMaxDigits == null)
                {
                    MessageBox.Show(this, "מקס' ספרות חייב להיות מספר חיובי או ריק.", "שגיאת קלט",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }
            else if (IdType == "string")
            {
                if (StrMaxLength.HasValue && StrMaxLength.Value < StrMinLength)
                {
                    MessageBox.Show(this, "אורך מקסימלי קטן מהמינימלי.", "שגיאת קלט",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (StrRegex != null)
                {
                    try { _ = new Regex(StrRegex); }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, $"Regex לא תקין: {ex.Message}", "שגיאת קלט",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }
            }

            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

        private bool IsValidReplacementForType(string value)
        {
            switch (IdType)
            {
                case "numeric":
                    var s = value.Trim();
                    if (string.IsNullOrEmpty(s)) return false;
                    if (NumAllowNegative && s.StartsWith("-")) s = s[1..];
                    if (!NumAllowThousandSeparators && (s.Contains(",") || s.Contains("."))) return false;
                    if (NumIntegerOnly && s.Contains(".")) return false;
                    foreach (var ch in s) if (!char.IsDigit(ch)) return false;
                    if (NumMaxDigits.HasValue && s.TrimStart('0').Length > NumMaxDigits.Value) return false;
                    return true;

                case "string":
                    if (value.Length < StrMinLength) return false;
                    if (StrMaxLength.HasValue && value.Length > StrMaxLength.Value) return false;
                    if (StrDisallowWhitespace && value.Contains(" ")) return false;
                    if (StrRegex != null && !Regex.IsMatch(value, StrRegex)) return false;
                    return true;

                case "uuid":
                    var v = value.Trim();
                    if (UuidAcceptUrn && v.StartsWith("urn:uuid:", StringComparison.OrdinalIgnoreCase))
                        v = v.Substring("urn:uuid:".Length);
                    if (UuidAcceptBraced && v.StartsWith("{") && v.EndsWith("}"))
                        v = v.Substring(1, v.Length - 2);
                    var rx = new Regex("^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}$");
                    return rx.IsMatch(v);

                default:
                    return false;
            }
        }
    }
}
