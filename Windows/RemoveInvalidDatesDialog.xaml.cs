using System;
using System.Windows;
using System.Windows.Controls;
using System.Globalization; // NEW

namespace PipeWiseClient.Windows
{
    public partial class RemoveInvalidDatesDialog : Window
    {
        public int? MinYear { get; private set; }
        public int? MaxYear { get; private set; }
        public string EmptyAction { get; private set; } = "remove"; // remove | replace
        public string? EmptyReplacement { get; private set; }

        // NEW: תאריכי קצה בפורמט ISO לשרת (yyyy-MM-dd)
        public string? MinDateIso { get; private set; } // NEW
        public string? MaxDateIso { get; private set; } // NEW

        public RemoveInvalidDatesDialog(string columnName)
        {
            InitializeComponent();
            this.Title = $"הסרת תאריכים לא חוקיים – '{columnName}'";

            EmptyReplaceRadio.Checked += (_, __) => EmptyReplacementBox.IsEnabled = true;
            EmptyRemoveRadio.Checked  += (_, __) => EmptyReplacementBox.IsEnabled = false;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            // --- שנים (כמו שהיה) ---
            if (int.TryParse(MinYearBox.Text?.Trim(), out var minY)) MinYear = minY; else MinYear = null;
            if (int.TryParse(MaxYearBox.Text?.Trim(), out var maxY)) MaxYear = maxY; else MaxYear = null;

            if (MinYear != null && MaxYear != null && MinYear > MaxYear)
            {
                MessageBox.Show(this, "טווח שנים לא תקין: המינימום גדול מהמקסימום.", "שגיאת קלט", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // --- NEW: תאריכי קצה (min_date / max_date) ---
            // דורש שתי TextBox ב-XAML: MinDateBox, MaxDateBox (או תחליף ב-DatePicker)
            MinDateIso = ParseToIsoDate(MinDateBox?.Text);
            MaxDateIso = ParseToIsoDate(MaxDateBox?.Text);

            if (MinDateIso != null && MaxDateIso != null &&
                string.CompareOrdinal(MinDateIso, MaxDateIso) > 0)
            {
                MessageBox.Show(this, "טווח תאריכים לא תקין: תאריך המינימום גדול מהמקסימום.", "שגיאת קלט", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // --- פעולה עבור ערכים ריקים ---
            if (EmptyReplaceRadio.IsChecked == true)
            {
                EmptyAction = "replace";
                var s = EmptyReplacementBox.Text?.Trim();
                if (string.IsNullOrWhiteSpace(s))
                {
                    MessageBox.Show(this, "בחרת 'השלם בתאריך', אך לא הזנת תאריך.", "שגיאת קלט", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // NEW: אם ניתן לפרסר לתאריך – ננרמל ל-ISO כדי למנוע אי-התאמות פורמט מול השרת
                var iso = ParseToIsoDate(s);
                EmptyReplacement = iso ?? s; // אם לא זוהה, נשאיר טקסט כפי שהוזן (השרת עדיין ינסה לפרסר)
            }
            else
            {
                EmptyAction = "remove";
                EmptyReplacement = null;
            }

            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

        // NEW: פרסינג גמיש לתאריך והחזרה כ-ISO (yyyy-MM-dd) או null אם ריק/לא תקין
        private static string? ParseToIsoDate(string? raw)
        {
            var s = raw?.Trim();
            if (string.IsNullOrEmpty(s)) return null;

            // קודם נסה פרסר כללי של המערכת (כולל פורמטים מקומיים)
            if (DateTime.TryParse(s, out var dt1))
                return dt1.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            // נסה פורמטים נפוצים שמקבילים לשרת
            string[] fmts = new[]
            {
                "dd-MM-yyyy","dd/MM/yyyy","dd.MM.yyyy",
                "yyyy-MM-dd","yyyy/MM/dd","MM/dd/yyyy","MM-dd-yyyy",
                "dd-MM-yy","dd/MM/yy","dd.MM.yy","yy-MM-dd","yy/MM/dd","MM/dd/yy",
                // עם זמן
                "dd-MM-yyyy HH:mm:ss","dd/MM/yyyy HH:mm:ss","dd.MM.yyyy HH:mm:ss",
                "yyyy-MM-dd HH:mm:ss","yyyy-MM-dd'T'HH:mm:ss","MM/dd/yyyy HH:mm:ss"
            };
            if (DateTime.TryParseExact(s, fmts, CultureInfo.InvariantCulture,
                                       DateTimeStyles.AllowWhiteSpaces, out var dt2))
                return dt2.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            // תמיכה ב-ISO עם Z
            if (DateTime.TryParse(s.Replace("Z", ""), out var dt3))
                return dt3.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            return null; // השרת עוד ינסה לפרסר; זה רק לנירמול מראש כשאפשר
        }
    }
}
