using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace PipeWiseClient.Helpers
{
    public static class InputDialogs
    {
        public static string? ShowSingleValueDialog(string title, string prompt, string defaultValue = "")
        {
            var dialog = new Window
            {
                Title = title,
                Width = 400,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var promptLabel = new TextBlock
            {
                Text = prompt,
                Margin = new Thickness(10),
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetRow(promptLabel, 0);
            grid.Children.Add(promptLabel);

            var inputTextBox = new TextBox
            {
                Text = defaultValue,
                Margin = new Thickness(10),
                Height = 25
            };
            Grid.SetRow(inputTextBox, 1);
            grid.Children.Add(inputTextBox);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(10)
            };

            var okButton = new Button
            {
                Content = "אישור",
                Width = 75,
                Height = 25,
                Margin = new Thickness(5, 0, 0, 0),
                IsDefault = true
            };

            var cancelButton = new Button
            {
                Content = "ביטול",
                Width = 75,
                Height = 25,
                IsCancel = true
            };

            buttonPanel.Children.Add(cancelButton);
            buttonPanel.Children.Add(okButton);

            Grid.SetRow(buttonPanel, 2);
            grid.Children.Add(buttonPanel);

            dialog.Content = grid;

            string? result = null;
            okButton.Click += (s, e) =>
            {
                result = inputTextBox.Text;
                dialog.DialogResult = true;
            };

            inputTextBox.Focus();
            inputTextBox.SelectAll();

            return dialog.ShowDialog() == true ? result : null;
        }

        public static string? ShowMultiValueDialog(string title, string prompt, string defaultValues = "")
        {
            var dialog = new Window
            {
                Title = title,
                Width = 450,
                Height = 300,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var promptLabel = new TextBlock
            {
                Text = prompt,
                Margin = new Thickness(10),
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetRow(promptLabel, 0);
            grid.Children.Add(promptLabel);

            var inputTextBox = new TextBox
            {
                Text = defaultValues,
                Margin = new Thickness(10),
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            Grid.SetRow(inputTextBox, 1);
            grid.Children.Add(inputTextBox);

            var helpLabel = new TextBlock
            {
                Text = "הפרד בין ערכים עם פסיק (,)",
                Margin = new Thickness(10, 0, 10, 10),
                FontStyle = FontStyles.Italic,
                Foreground = System.Windows.Media.Brushes.Gray
            };
            Grid.SetRow(helpLabel, 2);
            grid.Children.Add(helpLabel);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(10)
            };

            var okButton = new Button
            {
                Content = "אישור",
                Width = 75,
                Height = 25,
                Margin = new Thickness(5, 0, 0, 0),
                IsDefault = true
            };

            var cancelButton = new Button
            {
                Content = "ביטול",
                Width = 75,
                Height = 25,
                IsCancel = true
            };

            buttonPanel.Children.Add(cancelButton);
            buttonPanel.Children.Add(okButton);

            Grid.SetRow(buttonPanel, 3);
            grid.Children.Add(buttonPanel);

            dialog.Content = grid;

            string? result = null;
            okButton.Click += (s, e) =>
            {
                result = inputTextBox.Text;
                dialog.DialogResult = true;
            };

            inputTextBox.Focus();

            return dialog.ShowDialog() == true ? result : null;
        }

        public static Dictionary<string, string>? ShowValueMappingDialog(string title, string columnName)
        {
            var dialog = new Window
            {
                Title = title,
                Width = 500,
                Height = 400,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.CanResize
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var promptLabel = new TextBlock
            {
                Text = $"הגדר החלפת ערכים עבור העמודה '{columnName}':",
                Margin = new Thickness(10),
                FontWeight = FontWeights.Bold
            };
            Grid.SetRow(promptLabel, 0);
            grid.Children.Add(promptLabel);

            var instructionLabel = new TextBlock
            {
                Text = "הכנס זוג ערכים בכל שורה: ערך_ישן=ערך_חדש",
                Margin = new Thickness(10, 0, 10, 10),
                FontStyle = FontStyles.Italic,
                Foreground = System.Windows.Media.Brushes.Gray
            };
            Grid.SetRow(instructionLabel, 1);
            grid.Children.Add(instructionLabel);

            var inputTextBox = new TextBox
            {
                Text = "זכר=M\nנקבה=F\nכן=1\nלא=0",
                Margin = new Thickness(10),
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                FontFamily = new System.Windows.Media.FontFamily("Consolas")
            };
            Grid.SetRow(inputTextBox, 2);
            grid.Children.Add(inputTextBox);

            var exampleLabel = new TextBlock
            {
                Text = "דוגמאות:\nזכר=M\nנקבה=F\nכן=True\nלא=False",
                Margin = new Thickness(10, 0, 10, 10),
                FontStyle = FontStyles.Italic,
                Foreground = System.Windows.Media.Brushes.DarkGreen,
                FontSize = 11
            };
            Grid.SetRow(exampleLabel, 3);
            grid.Children.Add(exampleLabel);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(10)
            };

            var okButton = new Button
            {
                Content = "אישור",
                Width = 75,
                Height = 25,
                Margin = new Thickness(5, 0, 0, 0),
                IsDefault = true
            };

            var cancelButton = new Button
            {
                Content = "ביטול",
                Width = 75,
                Height = 25,
                IsCancel = true
            };

            buttonPanel.Children.Add(cancelButton);
            buttonPanel.Children.Add(okButton);

            Grid.SetRow(buttonPanel, 4);
            grid.Children.Add(buttonPanel);

            dialog.Content = grid;

            Dictionary<string, string>? result = null;
            okButton.Click += (s, e) =>
            {
                try
                {
                    result = ParseValueMapping(inputTextBox.Text);
                    if (result != null && result.Count > 0)
                    {
                        dialog.DialogResult = true;
                    }
                    else
                    {
                        MessageBox.Show("נא להכניס לפחות זוג ערכים אחד בפורמט: ערך_ישן=ערך_חדש", 
                                      "שגיאה", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"שגיאה בפורמט: {ex.Message}\n\nהשתמש בפורמט: ערך_ישן=ערך_חדש", 
                                  "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };

            inputTextBox.Focus();

            return dialog.ShowDialog() == true ? result : null;
        }

        private static Dictionary<string, string> ParseValueMapping(string input)
        {
            var mapping = new Dictionary<string, string>();
            
            if (string.IsNullOrWhiteSpace(input))
                return mapping;

            var lines = input.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                if (string.IsNullOrEmpty(trimmedLine))
                    continue;

                var parts = trimmedLine.Split('=');
                if (parts.Length == 2)
                {
                    var oldValue = parts[0].Trim();
                    var newValue = parts[1].Trim();
                    
                    if (!string.IsNullOrEmpty(oldValue))
                    {
                        mapping[oldValue] = newValue;
                    }
                }
                else
                {
                    throw new FormatException($"שורה לא תקינה: '{trimmedLine}'. השתמש בפורמט: ערך_ישן=ערך_חדש");
                }
            }

            return mapping;
        }
    }
}