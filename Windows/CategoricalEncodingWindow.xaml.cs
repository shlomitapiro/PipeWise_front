using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO; 
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media; 
using PipeWiseClient.Services;
using PipeWiseClient.Interfaces;
using PipeWiseClient.Models;

namespace PipeWiseClient.Windows
{
    public partial class CategoricalEncodingWindow : Window
    {
        private readonly IApiClient _apiClient;
        private readonly string _filePath;
        private readonly string _fieldName;
        private readonly ColumnSettings _columnSettings;
        private static readonly SolidColorBrush NormalBrush = new SolidColorBrush(Color.FromRgb(0x34, 0x49, 0x5E));
        private static readonly SolidColorBrush SuccessBrush = new SolidColorBrush(Colors.Green);

        public ObservableCollection<MappingValue> MappingValues { get; set; }
        public CategoricalEncodingConfig? Result { get; private set; }

        public CategoricalEncodingWindow(IApiClient apiClient, string filePath, string fieldName, ColumnSettings columnSettings)
        {
            InitializeComponent();

            _apiClient = apiClient;
            _filePath = filePath;
            _fieldName = fieldName;
            _columnSettings = columnSettings;

            MappingValues = new ObservableCollection<MappingValue>();
            MappingListView.ItemsSource = MappingValues;

            CreateNewFieldRadio.Checked += OnTargetModeChanged;
            ReplaceFieldRadio.Checked += OnTargetModeChanged;

            _columnSettings = columnSettings;
            LoadUniqueValues();
        }

        private async void LoadUniqueValues()
        {
            try
            {
                UpdateLoadingStatus("מתחיל טעינה...");
                OkButton.IsEnabled = false;

                UpdateLoadingStatus($"טוען קובץ '{Path.GetFileName(_filePath)}'...");
                var scanResult = await _apiClient.ScanFieldValuesAsync(_filePath, _fieldName);

                if (!scanResult.FieldExists)
                {
                    ShowError($"השדה '{_fieldName}' לא נמצא בקובץ");
                    return;
                }

                UpdateLoadingStatus($"מעבד שדה '{scanResult.FieldName}'...");

                bool hasUppercase = _columnSettings.Operations.Contains("to_uppercase");
                bool hasLowercase = _columnSettings.Operations.Contains("to_lowercase");

                FieldInfoTextBlock.Text = $"שדה: {scanResult.FieldName}";
                ValuesCountTextBlock.Text = $"{scanResult.UniqueValues.Count} ערכים ייחודיים במקור";

                if (scanResult.Truncated)
                {
                    ValuesCountTextBlock.Text += " (חתוך)";
                }

                UpdateLoadingStatus("יוצר רשימת מיפוי...");

                var uniqueTransformedValues = new HashSet<string>();
                var originalValueExamples = new Dictionary<string, string>();
                
                foreach (var value in scanResult.UniqueValues)
                {
                    string transformedValue = value;
                    
                    if (hasUppercase)
                    {
                        transformedValue = value.ToUpperInvariant();
                    }
                    else if (hasLowercase)
                    {
                        transformedValue = value.ToLowerInvariant();
                    }

                    if (uniqueTransformedValues.Add(transformedValue))
                    {
                        originalValueExamples[transformedValue] = value;
                    }
                }

                ValuesCountTextBlock.Text = $"{scanResult.UniqueValues.Count} ערכים במקור, {uniqueTransformedValues.Count} ערכים ייחודיים אחרי טרנספורמציה";
                ValuesCountTextBlock.Text += $", {scanResult.TotalRows} שורות סה\"כ";

                if (hasUppercase || hasLowercase)
                {
                    var transformType = hasUppercase ? "אותיות גדולות" : "אותיות קטנות";
                    ValuesCountTextBlock.Text += $"\n(מוצג לאחר המרה ל{transformType})";
                    
                    int reduction = scanResult.UniqueValues.Count - uniqueTransformedValues.Count;
                    if (reduction > 0)
                    {
                        ValuesCountTextBlock.Text += $"\n⚠️ שים לב: {reduction} ערכים אוחדו בגלל ההמרה";
                    }
                }

                MappingValues.Clear();
                int counter = 0;

                foreach (var transformedValue in uniqueTransformedValues.OrderBy(v => v))
                {
                    var originalExample = originalValueExamples[transformedValue];
                    
                    var mappingValue = new MappingValue
                    {
                        OriginalValue = transformedValue,
                        EncodedValue = counter.ToString(),
                        IsSpecialValue = originalExample == "NULL" || originalExample == "EMPTY"
                    };

                    MappingValues.Add(mappingValue);
                    counter++;
                }

                NewFieldNameTextBox.Text = $"{_fieldName}_encoded";

                UpdateLoadingStatus("הטעינה הושלמה בהצלחה!", isCompleted: true);

            }
            catch (Exception ex)
            {
                ShowError($"שגיאה בטעינת הנתונים: {ex.Message}");
            }
            finally
            {
                HideLoading();
            }
        }

        private void OnTargetModeChanged(object sender, RoutedEventArgs e)
        {
            bool createNew = CreateNewFieldRadio.IsChecked == true;

            NewFieldPanel.Visibility = createNew ? Visibility.Visible : Visibility.Collapsed;
            DeleteOriginalCheckBox.Visibility = createNew ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (ValidateInput())
            {
                CreateResult();
                DialogResult = true;
                Close();
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private bool ValidateInput()
        {
            if (CreateNewFieldRadio.IsChecked == true)
            {
                if (string.IsNullOrWhiteSpace(NewFieldNameTextBox.Text))
                {
                    ShowError("יש להזין שם לשדה החדש");
                    NewFieldNameTextBox.Focus();
                    return false;
                }

                if (NewFieldNameTextBox.Text.Trim() == _fieldName)
                {
                    ShowError("שם השדה החדש לא יכול להיות זהה לשם השדה המקורי");
                    NewFieldNameTextBox.Focus();
                    return false;
                }
            }

            var encodedValues = new HashSet<string>();
            var invalidValues = new List<string>();

            foreach (var mapping in MappingValues)
            {
                var encodedValue = mapping.EncodedValue?.Trim();

                if (string.IsNullOrWhiteSpace(encodedValue))
                {
                    ShowError($"יש להזין ערך קטגוריאלי עבור '{mapping.OriginalValue}'");
                    return false;
                }

                if (!int.TryParse(encodedValue, out _))
                {
                    invalidValues.Add(mapping.OriginalValue);
                    continue;
                }

                // if (encodedValues.Contains(encodedValue))
                // {
                //     ShowError($"הערך '{encodedValue}' מופיע יותר מפעם אחת");
                //     return false;
                // }

                encodedValues.Add(encodedValue);
            }

            if (invalidValues.Count > 0)
            {
                ShowError($"הערכים הבאים אינם מספרים תקינים: {string.Join(", ", invalidValues)}");
                return false;
            }

            HideError();
            return true;
        }

        private void CreateResult()
        {
            var mapping = new Dictionary<string, int>();

            foreach (var mappingValue in MappingValues)
            {
                if (int.TryParse(mappingValue.EncodedValue?.Trim(), out int encoded))
                {
                    mapping[mappingValue.OriginalValue] = encoded;
                }
            }

            var config = new CategoricalEncodingConfig
            {
                Field = _fieldName,
                Mapping = mapping,
                ReplaceOriginal = ReplaceFieldRadio.IsChecked == true,
                DeleteOriginal = DeleteOriginalCheckBox.IsChecked == true,
                DefaultValue = -1
            };

            if (CreateNewFieldRadio.IsChecked == true)
            {
                config.TargetField = NewFieldNameTextBox.Text.Trim();
            }

            Result = config;
        }

        private void ShowError(string message)
        {
            ErrorMessageTextBlock.Text = message;
            ErrorPanel.Visibility = Visibility.Visible;
        }

        private void HideError()
        {
            ErrorPanel.Visibility = Visibility.Collapsed;
        }

        private void UpdateLoadingStatus(string message, bool isCompleted = false)
        {
            if (FieldInfoTextBlock != null)
            {
                FieldInfoTextBlock.Text = message;

                FieldInfoTextBlock.Foreground = isCompleted ? SuccessBrush : NormalBrush;
            }
        }

        private void HideLoading()
        {
            OkButton.IsEnabled = true;
            if (FieldInfoTextBlock != null)
            {
                FieldInfoTextBlock.Foreground = NormalBrush;
            }
        }
    }

    public class MappingValue : INotifyPropertyChanged
    {
        private string _originalValue = string.Empty;
        private string _encodedValue = string.Empty;
        private bool _isSpecialValue = false;

        public string OriginalValue
        {
            get => _originalValue;
            set => SetProperty(ref _originalValue, value);
        }

        public string EncodedValue
        {
            get => _encodedValue;
            set => SetProperty(ref _encodedValue, value);
        }

        public bool IsSpecialValue
        {
            get => _isSpecialValue;
            set => SetProperty(ref _isSpecialValue, value);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }

    public class CategoricalEncodingConfig
    {
        public string Field { get; set; } = string.Empty;
        public Dictionary<string, int> Mapping { get; set; } = new();
        public string? TargetField { get; set; }
        public bool ReplaceOriginal { get; set; } = true;
        public bool DeleteOriginal { get; set; } = false;
        public int DefaultValue { get; set; } = -1;
    }

    public class BoolToFontWeightConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isSpecial && isSpecial)
                return FontWeights.Bold;

            return FontWeights.Normal;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
