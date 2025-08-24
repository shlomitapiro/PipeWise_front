using System.Windows;

namespace PipeWiseClient.Windows
{
    public partial class PipelineNameDialog : Window
    {
        public string? PipelineName { get; private set; }

        public PipelineNameDialog(string? suggestedName = null)
        {
            InitializeComponent();
            if (!string.IsNullOrWhiteSpace(suggestedName))
                NameBox.Text = suggestedName;
            Loaded += (_, __) => NameBox.Focus();
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            var name = NameBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show("אנא הזן שם לפייפליין.", "שם חסר",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                NameBox.Focus();
                return;
            }
            PipelineName = name;
            DialogResult = true;
            Close();
        }
    }
}
