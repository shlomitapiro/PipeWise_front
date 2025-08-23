using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
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
                MessageBox.Show($"שגיאה בטעינת פייפליינים: {ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e) => await LoadAsync();

        private void Choose_Click(object sender, RoutedEventArgs e)
        {
            if (PipelinesList.SelectedItem is not PipelineSummary sel)
            {
                MessageBox.Show("בחר פייפליין מהרשימה.", "מידע", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            SelectedPipeline = sel;
            DialogResult = true;
            Close();
        }

        private void PipelinesList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (PipelinesList.SelectedItem is PipelineSummary)
                Choose_Click(sender!, e);
        }

        private async void DeleteItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string id && !string.IsNullOrWhiteSpace(id))
            {
                var name = (btn.DataContext as PipelineSummary)?.name ?? "פריט";
                if (MessageBox.Show($"למחוק את '{name}'?", "אישור מחיקה",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                    return;

                try
                {
                    await _api.DeletePipelineAsync(id);
                    await RefreshAsync(); // רענן את הרשימה אחרי מחיקה
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"מחיקה נכשלה: {ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

    }
}
