using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace PipeWiseClient.Windows
{
    public partial class CalculateTotalDialog : Window
    {
        public List<string> SelectedFields { get; private set; } = new List<string>();
        public string TargetFieldName { get; private set; } = "total";

        private readonly List<string> _availableColumns;
        private readonly List<CheckBox> _fieldCheckBoxes = new List<CheckBox>();

        public CalculateTotalDialog(List<string> availableColumns)
        {
            InitializeComponent();
            _availableColumns = availableColumns ?? new List<string>();
            
            SetupUI();
            UpdateStatus();
        }

        private void SetupUI()
        {
            // יצירת CheckBox לכל עמודה
            foreach (var column in _availableColumns)
            {
                var checkBox = new CheckBox
                {
                    Content = column,
                    Tag = column,
                    Margin = new Thickness(0, 0, 0, 8),
                    FontSize = 12
                };

                checkBox.Checked += FieldCheckBox_Changed;
                checkBox.Unchecked += FieldCheckBox_Changed;

                _fieldCheckBoxes.Add(checkBox);
                FieldsPanel.Children.Add(checkBox);
            }

            // אירוע שינוי שם השדה
            TargetFieldTextBox.TextChanged += (s, e) => UpdateStatus();
        }

        private void FieldCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            // ספור כמה שדות נבחרו
            var selectedCount = _fieldCheckBoxes.Count(cb => cb.IsChecked == true);
            
            // בדיקת שם השדה החדש
            var targetName = TargetFieldTextBox.Text?.Trim() ?? "";
            var isValidTargetName = !string.IsNullOrWhiteSpace(targetName) && 
                                    !_availableColumns.Contains(targetName);

            // עדכון סטטוס
            if (selectedCount < 2)
            {
                StatusText.Text = $"בחר לפחות 2 שדות כדי להמשיך (נבחרו: {selectedCount})";
                StatusText.Foreground = System.Windows.Media.Brushes.Red;
                OkButton.IsEnabled = false;
            }
            else if (string.IsNullOrWhiteSpace(targetName))
            {
                StatusText.Text = "הזן שם לשדה החדש";
                StatusText.Foreground = System.Windows.Media.Brushes.Red;
                OkButton.IsEnabled = false;
            }
            else if (_availableColumns.Contains(targetName))
            {
                StatusText.Text = "שם השדה כבר קיים - בחר שם אחר";
                StatusText.Foreground = System.Windows.Media.Brushes.Red;
                OkButton.IsEnabled = false;
            }
            else
            {
                StatusText.Text = $"מוכן! יסוכמו {selectedCount} שדות ויווצר שדה '{targetName}'";
                StatusText.Foreground = System.Windows.Media.Brushes.Green;
                OkButton.IsEnabled = true;
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // איסוף השדות הנבחרים
                SelectedFields = _fieldCheckBoxes
                    .Where(cb => cb.IsChecked == true)
                    .Select(cb => cb.Tag.ToString())
                    .ToList();

                TargetFieldName = TargetFieldTextBox.Text?.Trim() ?? "total";

                // בדיקה אחרונה
                if (SelectedFields.Count < 2)
                {
                    MessageBox.Show("יש לבחור לפחות 2 שדות", "שגיאה", 
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(TargetFieldName))
                {
                    MessageBox.Show("יש להזין שם לשדה החדש", "שגיאה", 
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"שגיאה: {ex.Message}", "שגיאה", 
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}