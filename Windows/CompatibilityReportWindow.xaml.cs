using System.Linq;
using System.Text;
using System.Windows;
using PipeWiseClient.Models;

namespace PipeWiseClient.Windows
{
    public partial class CompatibilityReportWindow : Window
    {
        private readonly CompatResult _result;

        public CompatibilityReportWindow(CompatResult result)
        {
            InitializeComponent();
            _result = result;

            IssuesGrid.ItemsSource = _result.Issues;

            var errors = _result.Issues.Count(i => i.Severity == IssueSeverity.Error);
            var warns  = _result.Issues.Count(i => i.Severity == IssueSeverity.Warning);

            SummaryText.Text = _result.IsCompatible
                ? "בדיקה הושלמה: הקונפיגורציה תואמת לקובץ."
                : "הבדיקה מצאה בעיות התאמה. יש לתקן לפני הרצה.";

            if (errors > 0)
            {
                ErrorsBadge.Visibility = Visibility.Visible;
                ErrorsText.Text = $"{errors} שגיאות";
            }

            if (warns > 0)
            {
                WarningsBadge.Visibility = Visibility.Visible;
                WarningsText.Text = $"{warns} אזהרות";
            }
        }

        private void CopyBtn_Click(object sender, RoutedEventArgs e)
        {
            var sb = new StringBuilder();
            sb.AppendLine("דוח תאימות:");
            sb.AppendLine(SummaryText.Text);
            foreach (var i in _result.Issues)
            {
                sb.AppendLine($"- [{i.Severity}] {i.Code} | עמודה: {i.Column ?? "-"} | {i.Message}");
            }
            Clipboard.SetText(sb.ToString());
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = !_result.Issues.Any(x => x.Severity == IssueSeverity.Error);
            this.Close();
        }
    }
}
