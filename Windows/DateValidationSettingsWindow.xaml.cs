using System;
using System.Windows;
using System.Windows.Controls; // הוסף שורה זו

namespace PipeWiseClient.Windows
{
    public partial class DateValidationSettingsWindow : Window
    {
        public string Action { get; private set; } = "remove_row";
        public DateTime? ReplacementDate { get; private set; }
        public string DateFormat { get; private set; } = "dd/MM/yyyy";

        public DateValidationSettingsWindow()
        {
            InitializeComponent();
            
            // ברירות מחדל
            RemoveRowRadio.IsChecked = true;
            ReplacementDatePicker.SelectedDate = DateTime.Today;
            DateFormatComboBox.SelectedIndex = 0; // DD/MM/YYYY
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            // בדיקת פורמט
            if (DateFormatComboBox.SelectedItem is ComboBoxItem selectedFormat && 
                selectedFormat.Tag is string format)
            {
                DateFormat = format;
            }

            // בדיקת פעולה
            if (RemoveRowRadio.IsChecked == true)
            {
                Action = "remove_row";
                ReplacementDate = null;
            }
            else if (ReplaceWithDateRadio.IsChecked == true)
            {
                Action = "replace_with_date";
                ReplacementDate = ReplacementDatePicker.SelectedDate ?? DateTime.Today;
            }

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}