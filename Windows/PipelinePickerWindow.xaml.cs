using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PipeWiseClient.Models;
using PipeWiseClient.Services;

namespace PipeWiseClient.Windows
{
    public partial class PipelinePickerWindow : Window
    {
        private readonly ApiClient _api = new();
        private readonly ObservableCollection<PipelineSummary> _items = new();
        public PipelineSummary? SelectedPipeline { get; private set; }

        public PipelinePickerWindow()
        {
            InitializeComponent();
            PipelinesList.ItemsSource = _items;
            Loaded += async (_, __) => await LoadAsync();
            Unloaded += (_, __) => _api.Dispose();
        }

        private async Task LoadAsync()
        {
            try
            {
                var q = string.IsNullOrWhiteSpace(SearchBox.Text) ? null : SearchBox.Text.Trim();
                var res = await _api.ListPipelinesAsync(q, limit: 100);
                _items.Clear();
                foreach (var p in res.pipelines)
                    _items.Add(p);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"שגיאה בטעינת פייפליינים: {ex.Message}", "שגיאה",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private Task RefreshAsync() => LoadAsync();

        private async void Refresh_Click(object sender, RoutedEventArgs e) => await LoadAsync();

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) _ = LoadAsync();
        }

        private void Choose_Click(object sender, RoutedEventArgs e)
        {
            if (PipelinesList.SelectedItem is not PipelineSummary sel)
            {
                MessageBox.Show("בחר פייפליין מהרשימה.", "מידע",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            SelectedPipeline = sel;
            DialogResult = true;
            Close();
        }

        private void RunSelected_Click(object sender, RoutedEventArgs e) => Choose_Click(sender, e);

        private async void DeleteSelected_Click(object sender, RoutedEventArgs e)
        {
            if (PipelinesList.SelectedItem is not PipelineSummary sel)
            {
                MessageBox.Show("בחר פייפליין למחיקה.", "מידע",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (MessageBox.Show($"למחוק את '{sel.name}'?", "אישור מחיקה",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            try
            {
                await _api.DeletePipelineAsync(sel.id);
                await RefreshAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"מחיקה נכשלה: {ex.Message}", "שגיאה",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void RenameSelected_Click(object sender, RoutedEventArgs e)
        {
            if (PipelinesList.SelectedItem is not PipelineSummary sel)
            {
                MessageBox.Show("בחר פייפליין לשינוי שם.", "מידע",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dlg = new PipelineNameDialog(sel.name)
            {
                Owner = this,
                Title = "שינוי שם פייפליין"
            };
            
            var ok = dlg.ShowDialog() == true;
            if (!ok || string.IsNullOrWhiteSpace(dlg.PipelineName))
                return;

            if (dlg.PipelineName.Trim() == sel.name)
            {
                MessageBox.Show("השם החדש זהה לשם הקיים.", "מידע",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                await _api.UpdatePipelineNameAsync(sel.id, dlg.PipelineName.Trim());
                MessageBox.Show($"שם הפייפליין שונה בהצלחה ל-'{dlg.PipelineName}'", "הצלחה",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                await RefreshAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"שינוי השם נכשל: {ex.Message}", "שגיאה",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PipelinesList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (PipelinesList.SelectedItem is PipelineSummary)
                Choose_Click(sender, e);
        }

        private void PipelinesList_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                Choose_Click(sender, e);
            else if (e.Key == Key.Delete)
                DeleteSelected_Click(sender, e);
        }
    }
}
