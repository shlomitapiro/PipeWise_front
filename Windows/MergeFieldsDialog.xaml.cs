using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace PipeWiseClient.Windows
{
    public partial class MergeColumnsDialog : Window
    {
        public List<string> SelectedColumns { get; private set; } = new List<string>();
        public string TargetColumn { get; private set; } = string.Empty;
        public string Separator { get; private set; } = " ";
        public bool RemoveSourceColumns { get; private set; } = false;
        public string EmptyHandling { get; private set; } = "skip";
        public string EmptyReplacement { get; private set; } = string.Empty;

        private readonly HashSet<string> _existingColumns;
        private readonly List<CheckBox> _columnCheckBoxes = new List<CheckBox>();

        public MergeColumnsDialog(IEnumerable<string> availableColumns, string currentColumn)
        {
            InitializeComponent();

            _existingColumns = new HashSet<string>(availableColumns, StringComparer.OrdinalIgnoreCase);

            // יצירת checkboxes לכל עמודה (ללא העמודה הנוכחית)
            var columnsList = availableColumns.Where(c => c != currentColumn).ToList();
            CreateColumnCheckBoxes(columnsList);

            // ברירת מחדל לשם העמודה החדשה
            TargetColumnBox.Text = $"{currentColumn}_merged";

            // מאזינים לשינויים
            TargetColumnBox.TextChanged += (s, e) => UpdateValidation();
            EmptyHandlingCombo.SelectionChanged += EmptyHandling_Changed;

            UpdateValidation();
        }

        private void CreateColumnCheckBoxes(List<string> columns)
        {
            ColumnsPanel.Children.Clear();
            _columnCheckBoxes.Clear();

            foreach (var column in columns)
            {
                var checkBox = new CheckBox
                {
                    Content = column,
                    Margin = new System.Windows.Thickness(0, 2, 0, 2),
                    FontSize = 12
                };

                // הוסף מאזין לשינוי
                checkBox.Checked += (s, e) => UpdateValidation();
                checkBox.Unchecked += (s, e) => UpdateValidation();

                _columnCheckBoxes.Add(checkBox);
                ColumnsPanel.Children.Add(checkBox);
            }
        }

        private void EmptyHandling_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (EmptyHandlingCombo?.SelectedItem is ComboBoxItem item)
            {
                if (EmptyReplacementBox != null)
                {
                    EmptyReplacementBox.IsEnabled = item.Tag?.ToString() == "replace";
                }
            }
        }

        private void UpdateValidation()
        {
            var selectedColumns = GetSelectedColumns();
            var targetColumn = TargetColumnBox?.Text?.Trim() ?? "";

            // הלוגיקה החדשה: העמודה הנוכחית + לפחות עמודה נוספת אחת = מינימום 2 עמודות
            bool isValid = selectedColumns.Count >= 1 &&  // שונה מ-2 ל-1
                          !string.IsNullOrWhiteSpace(targetColumn) &&
                          !_existingColumns.Contains(targetColumn);

            if (OkButton != null)
            {
                OkButton.IsEnabled = isValid;

                // עדכון הודעות תקינות
                if (selectedColumns.Count < 1)
                {
                    OkButton.ToolTip = "יש לבחור לפחות עמודה נוספת אחת למיזוג";
                }
                else if (string.IsNullOrWhiteSpace(targetColumn))
                {
                    OkButton.ToolTip = "יש להכניס שם לעמודה החדשה";
                }
                else if (_existingColumns.Contains(targetColumn))
                {
                    OkButton.ToolTip = $"העמודה '{targetColumn}' כבר קיימת";
                }
                else
                {
                    OkButton.ToolTip = "לחץ כדי למזג את העמודות";
                }
            }
        }

        private List<string> GetSelectedColumns()
        {
            var selected = new List<string>();

            foreach (var checkBox in _columnCheckBoxes)
            {
                if (checkBox.IsChecked == true)
                {
                    selected.Add(checkBox.Content?.ToString() ?? "");
                }
            }

            return selected;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            var selectedColumns = GetSelectedColumns();

            if (selectedColumns.Count < 1)
            {
                MessageBox.Show("יש לבחור לפחות עמודה נוספת אחת למיזוג", "בחירה לא תקינה",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var targetColumn = TargetColumnBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(targetColumn))
            {
                MessageBox.Show("יש להכניס שם לעמודה החדשה", "שם עמודה חסר",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_existingColumns.Contains(targetColumn))
            {
                MessageBox.Show($"העמודה '{targetColumn}' כבר קיימת. בחר שם אחר.", "שם עמודה קיים",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // שמור את התוצאות - העמודות הנבחרות פלוס העמודה הנוכחית
            SelectedColumns = selectedColumns;
            TargetColumn = targetColumn;

            // קבל מפריד
            var separatorItem = SeparatorCombo.SelectedItem as ComboBoxItem;
            Separator = !string.IsNullOrWhiteSpace(CustomSeparatorBox.Text)
                ? CustomSeparatorBox.Text
                : separatorItem?.Tag?.ToString() ?? " ";

            RemoveSourceColumns = RemoveSourceBox?.IsChecked == true;

            var emptyItem = EmptyHandlingCombo.SelectedItem as ComboBoxItem;
            EmptyHandling = emptyItem?.Tag?.ToString() ?? "skip";
            EmptyReplacement = EmptyReplacementBox?.Text ?? string.Empty;

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
