using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace PipeWiseClient.Windows
{
    /// <summary>
    /// חלון פיצול שדה - מאפשר למשתמש לפצל שדה אחד למספר שדות
    /// </summary>
    public partial class SplitFieldWindow : Window
    {
        public class SplitFieldConfig
        {
            public string SourceField { get; set; } = "";
            public string SplitType { get; set; } = "delimiter"; // "delimiter" or "fixed_length"
            public string Delimiter { get; set; } = ",";
            public int Length { get; set; } = 3;
            public List<string> TargetFields { get; set; } = new();
            public bool RemoveSource { get; set; } = true;
        }

        public SplitFieldConfig? Result { get; private set; }
        
        private readonly ObservableCollection<string> _targetFields = new();
        private readonly List<string> _availableFields;

        public SplitFieldWindow(List<string> availableFields)
        {
            InitializeComponent();
            _availableFields = availableFields ?? new List<string>();
            
            InitializeWindow();
        }

        private void InitializeWindow()
        {
            // אתחול ComboBox של שדות זמינים
            SourceFieldComboBox.ItemsSource = _availableFields;
            SourceFieldComboBox.SelectionChanged += SourceFieldComboBox_SelectionChanged;

            // אתחול רשימת שדות יעד
            TargetFieldsListBox.ItemsSource = _targetFields;
            _targetFields.CollectionChanged += (s, e) => UpdateValidation();

            // הוספת שדות ברירת מחדל
            _targetFields.Add("חלק_1");
            _targetFields.Add("חלק_2");
            
            UpdateValidation();
        }

        private void DelimiterRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (DelimiterPanel != null && FixedLengthPanel != null)
            {
                DelimiterPanel.IsEnabled = true;
                FixedLengthPanel.IsEnabled = false;
            }
        }

        private void FixedLengthRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (DelimiterPanel != null && FixedLengthPanel != null)
            {
                DelimiterPanel.IsEnabled = false;
                FixedLengthPanel.IsEnabled = true;
            }
        }

        private void SourceFieldComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateValidation();
        }

        private void AddField_Click(object sender, RoutedEventArgs e)
        {
            string fieldName = NewFieldNameTextBox.Text?.Trim() ?? "";
            
            if (string.IsNullOrEmpty(fieldName))
            {
                MessageBox.Show("אנא הזן שם שדה", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_targetFields.Contains(fieldName))
            {
                MessageBox.Show("שם השדה כבר קיים", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_availableFields.Contains(fieldName))
            {
                var result = MessageBox.Show(
                    $"השדה '{fieldName}' כבר קיים בנתונים. האם להמשיך?",
                    "שדה קיים", 
                    MessageBoxButton.YesNo, 
                    MessageBoxImage.Question);
                if (result != MessageBoxResult.Yes)
                    return;
            }

            _targetFields.Add(fieldName);
            NewFieldNameTextBox.Text = $"חלק_{_targetFields.Count + 1}";
        }

        private void RemoveField_Click(object sender, RoutedEventArgs e)
        {
            int selectedIndex = TargetFieldsListBox.SelectedIndex;
            if (selectedIndex >= 0 && selectedIndex < _targetFields.Count)
            {
                _targetFields.RemoveAt(selectedIndex);
            }
        }

        private void UpdateValidation()
        {
            bool isValid = true;
            string warningMessage = "";

            // בדיקה שנבחר שדה מקור
            if (SourceFieldComboBox.SelectedItem == null)
            {
                isValid = false;
            }

            // בדיקה שיש לפחות שדה יעד אחד
            if (_targetFields.Count == 0)
            {
                isValid = false;
                warningMessage = "נדרש לפחות שדה יעד אחד";
            }

            // עדכון הודעות אזהרה
            if (!string.IsNullOrEmpty(warningMessage))
            {
                FieldCountWarning.Text = warningMessage;
                FieldCountWarning.Visibility = Visibility.Visible;
            }
            else
            {
                FieldCountWarning.Visibility = Visibility.Collapsed;
            }

            // עדכון מצב כפתור האישור
            OkButton.IsEnabled = isValid;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var config = BuildConfiguration();
                if (ValidateConfiguration(config))
                {
                    Result = config;
                    DialogResult = true;
                    Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"שגיאה ביצירת הקונפיגורציה: {ex.Message}",
                    "שגיאה", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Result = null;
            DialogResult = false;
            Close();
        }

        private SplitFieldConfig BuildConfiguration()
        {
            var config = new SplitFieldConfig();
            
            // שדה מקור
            config.SourceField = SourceFieldComboBox.SelectedItem as string ?? "";
            
            // סוג פיצול
            if (DelimiterRadio.IsChecked == true)
            {
                config.SplitType = "delimiter";
                var selectedItem = DelimiterComboBox.SelectedItem as ComboBoxItem;
                config.Delimiter = selectedItem?.Tag as string ?? ",";
            }
            else if (FixedLengthRadio.IsChecked == true)
            {
                config.SplitType = "fixed_length";
                if (!int.TryParse(LengthTextBox.Text, out int length) || length <= 0)
                {
                    throw new ArgumentException("מספר התווים חייב להיות מספר חיובי");
                }
                config.Length = length;
            }
            
            // שדות יעד
            config.TargetFields = _targetFields.ToList();
            
            // הגדרות נוספות
            config.RemoveSource = RemoveSourceCheckBox.IsChecked == true;
            
            return config;
        }

        private bool ValidateConfiguration(SplitFieldConfig config)
        {
            // בדיקות תקינות בסיסיות
            if (string.IsNullOrEmpty(config.SourceField))
            {
                MessageBox.Show("נדרש לבחור שדה מקור", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (config.TargetFields.Count == 0)
            {
                MessageBox.Show("נדרש לפחות שדה יעד אחד", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (config.SplitType == "delimiter" && string.IsNullOrEmpty(config.Delimiter))
            {
                MessageBox.Show("נדרש לבחור תו מפריד", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (config.SplitType == "fixed_length" && config.Length <= 0)
            {
                MessageBox.Show("מספר התווים חייב להיות חיובי", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            // בדיקת קונליקטים בשמות שדות
            if (config.TargetFields.Contains(config.SourceField) && !config.RemoveSource)
            {
                MessageBox.Show(
                    "שם אחד מהשדות החדשים זהה לשדה המקור. אנא בחר שמות שונים או סמן למחוק את השדה המקורי.",
                    "קונפליקט בשמות", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Warning);
                return false;
            }

            var duplicates = config.TargetFields.GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key);
            if (duplicates.Any())
            {
                MessageBox.Show(
                    $"שמות שדות כפולים: {string.Join(", ", duplicates)}",
                    "שמות כפולים", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Warning);
                return false;
            }

            // אזהרה לגבי שכתוב שדות קיימים
            var existingFields = config.TargetFields.Intersect(_availableFields);
            if (existingFields.Any())
            {
                var result = MessageBox.Show(
                    $"השדות הבאים כבר קיימים ויוחלפו: {string.Join(", ", existingFields)}\n\nהאם להמשיך?",
                    "שכתוב שדות קיימים",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (result != MessageBoxResult.Yes)
                {
                    return false;
                }
            }

            return true;
        }
    }
}