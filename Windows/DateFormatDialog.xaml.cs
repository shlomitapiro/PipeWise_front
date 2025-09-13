using System.Windows;
using System.Windows.Controls;

namespace PipeWiseClient.Windows
{
    public partial class DateFormatDialog : Window
    {
        public string? SelectedPythonFormat { get; private set; }
        public DateFormatDialog(string columnName, bool looksLikeDate)
        {
            InitializeComponent();
            TitleText.Text = $"בחירת פורמט תאריך לעמודה '{columnName}'";

            if (!looksLikeDate)
            {
                // רמז ויזואלי – לא חוסם, אבל נבטל בהגיון בכפתור OK (לפי הדרישה "לא יקרה כלום")
                TitleText.Text += " (אזהרה: לא זוהתה כעמודת תאריך)";
                Title = "קביעת פורמט תאריך – ייתכן שאינה עמודת תאריך";
            }
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (FormatCombo.SelectedItem is ComboBoxItem it && it.Tag is string tag && !string.IsNullOrWhiteSpace(tag))
            {
                SelectedPythonFormat = tag;
                DialogResult = true;
            }
            else
            {
                DialogResult = false;
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
