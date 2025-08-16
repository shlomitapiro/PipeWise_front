using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace PipeWiseClient.Helpers
{
    public static class InputDialogs
    {
        /// <summary>
        /// הצגת דיאלוג לקבלת ערך יחיד מהמשתמש
        /// </summary>
        public static string? ShowSingleValueDialog(string title, string prompt, string defaultValue = "")
        {
            var dialog = new Window
            {
                Title = title,
                Width = 400,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                Owner = Application.Current.MainWindow
            };

            var stackPanel = new StackPanel
            {
                Margin = new Thickness(20)
            };

            // הוספת טקסט ההנחיה
            var promptLabel = new TextBlock
            {
                Text = prompt,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            };
            stackPanel.Children.Add(promptLabel);

            // תיבת טקסט לקלט
            var textBox = new TextBox
            {
                Text = defaultValue,
                Margin = new Thickness(0, 0, 0, 20)
            };
            stackPanel.Children.Add(textBox);

            // כפתורים
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var okButton = new Button
            {
                Content = "אישור",
                Width = 80,
                Height = 30,
                Margin = new Thickness(0, 0, 10, 0),
                IsDefault = true
            };

            var cancelButton = new Button
            {
                Content = "ביטול",
                Width = 80,
                Height = 30,
                IsCancel = true
            };

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            stackPanel.Children.Add(buttonPanel);

            dialog.Content = stackPanel;

            string? result = null;
            okButton.Click += (s, e) => 
            {
                result = textBox.Text;
                dialog.DialogResult = true;
            };

            textBox.KeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Enter)
                {
                    result = textBox.Text;
                    dialog.DialogResult = true;
                }
            };

            // מיקוד בתיבת הטקסט
            textBox.Focus();
            textBox.SelectAll();

            return dialog.ShowDialog() == true ? result : null;
        }

        /// <summary>
        /// הצגת דיאלוג לקבלת שני ערכים מהמשתמש
        /// </summary>
        public static (string? value1, string? value2) ShowTwoValuesDialog(string title, string prompt1, string prompt2, string defaultValue1 = "", string defaultValue2 = "")
        {
            var dialog = new Window
            {
                Title = title,
                Width = 450,
                Height = 250,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                Owner = Application.Current.MainWindow
            };

            var stackPanel = new StackPanel
            {
                Margin = new Thickness(20)
            };

            // השדה הראשון
            var prompt1Label = new TextBlock
            {
                Text = prompt1,
                Margin = new Thickness(0, 0, 0, 5)
            };
            stackPanel.Children.Add(prompt1Label);

            var textBox1 = new TextBox
            {
                Text = defaultValue1,
                Margin = new Thickness(0, 0, 0, 15)
            };
            stackPanel.Children.Add(textBox1);

            // השדה השני
            var prompt2Label = new TextBlock
            {
                Text = prompt2,
                Margin = new Thickness(0, 0, 0, 5)
            };
            stackPanel.Children.Add(prompt2Label);

            var textBox2 = new TextBox
            {
                Text = defaultValue2,
                Margin = new Thickness(0, 0, 0, 20)
            };
            stackPanel.Children.Add(textBox2);

            // כפתורים
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var okButton = new Button
            {
                Content = "אישור",
                Width = 80,
                Height = 30,
                Margin = new Thickness(0, 0, 10, 0),
                IsDefault = true
            };

            var cancelButton = new Button
            {
                Content = "ביטול",
                Width = 80,
                Height = 30,
                IsCancel = true
            };

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            stackPanel.Children.Add(buttonPanel);

            dialog.Content = stackPanel;

            string? result1 = null;
            string? result2 = null;

            okButton.Click += (s, e) => 
            {
                result1 = textBox1.Text;
                result2 = textBox2.Text;
                dialog.DialogResult = true;
            };

            textBox1.Focus();
            textBox1.SelectAll();

            return dialog.ShowDialog() == true ? (result1, result2) : (null, null);
        }

        /// <summary>
        /// הצגת דיאלוג לבחירה מרשימה
        /// </summary>
        public static string? ShowSelectionDialog(string title, string prompt, IEnumerable<string> options)
        {
            var dialog = new Window
            {
                Title = title,
                Width = 400,
                Height = 300,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                Owner = Application.Current.MainWindow
            };

            var stackPanel = new StackPanel
            {
                Margin = new Thickness(20)
            };

            // הוספת טקסט ההנחיה
            var promptLabel = new TextBlock
            {
                Text = prompt,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            };
            stackPanel.Children.Add(promptLabel);

            // רשימת בחירות
            var listBox = new ListBox
            {
                Height = 150,
                Margin = new Thickness(0, 0, 0, 20)
            };

            foreach (var option in options)
            {
                listBox.Items.Add(option);
            }

            if (listBox.Items.Count > 0)
                listBox.SelectedIndex = 0;

            stackPanel.Children.Add(listBox);

            // כפתורים
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var okButton = new Button
            {
                Content = "אישור",
                Width = 80,
                Height = 30,
                Margin = new Thickness(0, 0, 10, 0),
                IsDefault = true
            };

            var cancelButton = new Button
            {
                Content = "ביטול",
                Width = 80,
                Height = 30,
                IsCancel = true
            };

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            stackPanel.Children.Add(buttonPanel);

            dialog.Content = stackPanel;

            string? result = null;
            okButton.Click += (s, e) => 
            {
                result = listBox.SelectedItem?.ToString();
                dialog.DialogResult = true;
            };

            listBox.MouseDoubleClick += (s, e) =>
            {
                result = listBox.SelectedItem?.ToString();
                dialog.DialogResult = true;
            };

            return dialog.ShowDialog() == true ? result : null;
        }
    }
}