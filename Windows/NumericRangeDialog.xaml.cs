using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace PipeWiseClient.Windows
{
    public partial class NumericRangeDialog : Window
    {
        public double? MinValue { get; private set; }
        public double? MaxValue { get; private set; }
        public string ActionOnViolation { get; private set; } = "remove";
        public double? ReplacementValue { get; private set; }

        private readonly string _columnName;

        public NumericRangeDialog(string columnName)
        {
            InitializeComponent();
            _columnName = columnName;
            TitleText.Text = $"הגדרת טווח מספרי לעמודה '{columnName}'";
            HelpText.Text = "ניתן למלא רק גבול אחד (תחתון/עליון) או את שניהם. ערך ריק משמעו ללא גבול.";
            ActionCombo.SelectionChanged += ActionCombo_SelectionChanged;
        }

        private void ActionCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var tag = ((ComboBoxItem)ActionCombo.SelectedItem).Tag?.ToString() ?? "remove";
            ReplacementBox.IsEnabled = tag == "replace";
        }

        private static bool TryParseDouble(string s, out double value)
        {
            s = (s ?? "").Trim();
            if (s == "")
            {
                value = 0; return false;
            }
            // נסה Invariant עם הסרת אלפים
            if (double.TryParse(s.Replace(",", ""), NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out value))
                return true;
            // נסה פורמט פסיק כעשרוני
            var s2 = s.Replace(".", "").Replace(",", ".");
            return double.TryParse(s2, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out value);
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // min/max (רשות)
                if (TryParseDouble(MinBox.Text, out var min))
                    MinValue = min;
                else if (!string.IsNullOrWhiteSpace(MinBox.Text))
                    throw new ArgumentException("גבול תחתון חייב להיות מספר.");

                if (TryParseDouble(MaxBox.Text, out var max))
                    MaxValue = max;
                else if (!string.IsNullOrWhiteSpace(MaxBox.Text))
                    throw new ArgumentException("גבול עליון חייב להיות מספר.");

                // פעולה בחריגה
                ActionOnViolation = ((ComboBoxItem)ActionCombo.SelectedItem).Tag?.ToString() ?? "remove";

                if (ActionOnViolation == "replace")
                {
                    if (!TryParseDouble(ReplacementBox.Text, out var repl))
                        throw new ArgumentException("ערך חלופי חייב להיות מספר.");
                    ReplacementValue = repl;
                }

                // אימות בסיסי: אם יש שני גבולות, לוודא היגיון
                if (MinValue.HasValue && MaxValue.HasValue && MinValue > MaxValue)
                    throw new ArgumentException("גבול תחתון גדול מהעליון.");

                DialogResult = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "קלט לא תקין", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
