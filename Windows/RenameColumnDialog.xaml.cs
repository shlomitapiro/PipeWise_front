using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace PipeWiseClient.Windows
{
    public partial class RenameColumnDialog : Window
    {
        public string OldName { get; private set; }
        public string NewName { get; private set; } = string.Empty;
        
        private readonly HashSet<string> _existingColumnNames;

        public RenameColumnDialog(string currentName, IEnumerable<string> existingColumnNames)
        {
            InitializeComponent();
            
            OldName = currentName;
            _existingColumnNames = new HashSet<string>(existingColumnNames, StringComparer.OrdinalIgnoreCase);
            
            TitleText.Text = $"שינוי שם העמודה: {currentName}";
            OldNameBox.Text = currentName;
            
            // מיקוד בתיבת הטקסט החדשה
            NewNameBox.Focus();
            
            // עדכון בזמן אמת
            NewNameBox.TextChanged += (s, e) => ValidateInput();
            
            // התחלה עם כפתור מנוטרל
            OkButton.IsEnabled = false;
        }

        private void ValidateInput()
        {
            var newName = NewNameBox.Text?.Trim();
            var validation = IsValidColumnName(newName);
            
            OkButton.IsEnabled = validation.IsValid;
            
            if (!validation.IsValid && !string.IsNullOrWhiteSpace(newName))
            {
                ErrorMessage.Text = validation.ErrorMessage;
                ErrorMessage.Visibility = Visibility.Visible;
                NewNameBox.Background = Brushes.LightPink;
            }
            else
            {
                ErrorMessage.Visibility = Visibility.Collapsed;
                NewNameBox.Background = string.IsNullOrWhiteSpace(newName) ? 
                    Brushes.White : Brushes.LightGreen;
            }
        }

        private (bool IsValid, string ErrorMessage) IsValidColumnName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return (false, "");
                
            if (string.Equals(name.Trim(), OldName, StringComparison.OrdinalIgnoreCase))
                return (false, "השם החדש חייב להיות שונה מהשם הנוכחי");
                
            if (_existingColumnNames.Contains(name.Trim()))
                return (false, $"השם '{name}' כבר קיים. בחר שם אחר");
                
            var regex = new Regex(@"^[a-zA-Z0-9\u0590-\u05FF_\s]+$");
            if (!regex.IsMatch(name))
                return (false, "השם מכיל תווים לא חוקיים");
                
            if (char.IsDigit(name.Trim()[0]))
                return (false, "השם לא יכול להתחיל במספר");
                
            return (true, "");
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            var newName = NewNameBox.Text?.Trim();
            var validation = IsValidColumnName(newName);
            
            if (!validation.IsValid)
            {
                MessageBox.Show(validation.ErrorMessage, "שם עמודה לא תקין", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                NewNameBox.Focus();
                return;
            }
            
            NewName = newName!;
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void NewNameBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && OkButton.IsEnabled)
            {
                Ok_Click(sender, e);
            }
            else if (e.Key == Key.Escape)
            {
                Cancel_Click(sender, e);
            }
        }
    }   
}