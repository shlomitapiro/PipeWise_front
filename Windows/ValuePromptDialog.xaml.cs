using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace PipeWiseClient.Windows
{
    public partial class ValuePromptDialog : Window
    {
        private readonly string _type;
        private readonly int _maxLength;

        public string ReplacementValue { get; private set; } = "";
        public int MaxLength => _maxLength;

        public ValuePromptDialog(string columnName, string inferredType, int maxLength = 255, string? initialValue = null)
        {
            InitializeComponent();
            _type = (inferredType ?? "string").ToLowerInvariant();
            _maxLength = maxLength;

            TitleText.Text = $"קבע ערך חלופי לתאים ריקים בעמודה '{columnName}'";
            HelpText.Text = _type switch
            {
                "int"    => $"סוג: מספר שלם • אורך מרבי: {maxLength}",
                "float"  => $"סוג: מספר עשרוני • אורך מרבי: {maxLength}",
                "bool"   => $"סוג: בוליאני (True/False)",
                "date"   => $"סוג: תאריך (נתמך: yyyy-MM-dd, dd/MM/yyyy, dd-MM-yyyy, MM/dd/yyyy) • אורך מרבי: {maxLength}",
                _        => $"סוג: טקסט • אורך מרבי: {maxLength}"
            };

            // UI mode per type
            if (_type == "bool")
            {
                BoolCombo.Visibility = Visibility.Visible;
                ValueBox.Visibility = Visibility.Collapsed;
                DatePickerCtl.Visibility = Visibility.Collapsed;
            }
            else if (_type == "date")
            {
                DatePickerCtl.Visibility = Visibility.Visible;
                ValueBox.Visibility = Visibility.Collapsed;
                BoolCombo.Visibility = Visibility.Collapsed;
            }
            else
            {
                ValueBox.Visibility = Visibility.Visible;
                BoolCombo.Visibility = Visibility.Collapsed;
                DatePickerCtl.Visibility = Visibility.Collapsed;
                if (!string.IsNullOrEmpty(initialValue))
                    ValueBox.Text = initialValue!;
            }
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_type == "bool")
                {
                    ReplacementValue = ((ComboBoxItem)BoolCombo.SelectedItem).Content?.ToString() ?? "false";
                    DialogResult = true; return;
                }
                if (_type == "date")
                {
                    var dt = DatePickerCtl.SelectedDate ?? DateTime.Today;
                    ReplacementValue = dt.ToString("yyyy-MM-dd");
                    DialogResult = true; return;
                }

                var s = ValueBox.Text ?? string.Empty;
                if (s.Length > _maxLength)
                {
                    MessageBox.Show(this, $"האורך {s.Length} חורג מהמגבלה { _maxLength }.", "ערך לא תקין", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                switch (_type)
                {
                    case "int":
                        if (!int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                        {
                            MessageBox.Show(this, "הערך חייב להיות מספר שלם תקין (דוגמה: 12).", "ערך לא תקין", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }
                        break;
                    case "float":
                        if (!double.TryParse(s, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out _))
                        {
                            MessageBox.Show(this, "הערך חייב להיות מספר עשרוני תקין (דוגמה: 12.5).", "ערך לא תקין", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }
                        break;
                    case "string":
                    default:
                        break;
                }

                ReplacementValue = s;
                DialogResult = true;
            }
            catch (Exception)
            {
                MessageBox.Show(this, "אירעה שגיאה בעת אימות הקלט.", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
