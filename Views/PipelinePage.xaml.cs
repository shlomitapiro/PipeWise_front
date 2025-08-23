using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using PipeWiseClient.Models;
using PipeWiseClient.Services;
using System.Windows.Input; 

namespace PipeWiseClient.Views
{
    public partial class PipelinePage : Page
    {
        private readonly ApiClient _api = new();
        private readonly ObservableCollection<PipelineSummary> _items = new();
        private string? _runFilePath;

        public PipelinePage()
        {
            InitializeComponent();
            PipelinesList.ItemsSource = _items;
            this.Loaded += async (_, __) => await LoadAsync();
            this.Unloaded += (_, __) => _api.Dispose();
        }

        private async Task LoadAsync()
        {
            try
            {
                var q = string.IsNullOrWhiteSpace(SearchBox.Text) ? null : SearchBox.Text.Trim();
                var res = await _api.ListPipelinesAsync(q, limit: 100);
                _items.Clear();
                foreach (var p in res.pipelines) _items.Add(p);

                Notify("רשימה", $"נטענו {res.total_count} פייפליינים");
            }
            catch (Exception ex)
            {
                NotifyError("שגיאה בטעינה", ex.Message);
            }
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e) => await LoadAsync();

        private void ChooseRunFile_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "CSV (*.csv)|*.csv|JSON (*.json)|*.json|Excel (*.xlsx;*.xls)|*.xlsx;*.xls|XML (*.xml)|*.xml|All Files (*.*)|*.*",
                Title = "בחר קובץ נתונים"
            };
            if (dlg.ShowDialog() == true)
            {
                _runFilePath = dlg.FileName;
                RunFilePathBox.Text = _runFilePath;
            }
        }

        private async void RunSelected_Click(object sender, RoutedEventArgs e)
        {
            if (PipelinesList.SelectedItem is not PipelineSummary sel)
            {
                MessageBox.Show("בחר פייפליין מהרשימה", "מידע", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                // אם בחרת קובץ – נעביר אותו לשרת; אחרת השרת יריץ בלי קובץ (אם מותר)
                var result = await _api.RunPipelineByIdAsync(sel.id, filePath: _runFilePath);
                Notify("הרצה", $"Pipeline '{sel.name}' הופעל", result?.message);
            }
            catch (Exception ex)
            {
                NotifyError("שגיאה בהרצה", ex.Message);
            }
        }

        private async void DeleteSelected_Click(object sender, RoutedEventArgs e)
        {
            if (PipelinesList.SelectedItem is not PipelineSummary sel)
            {
                MessageBox.Show("בחר פייפליין למחיקה", "מידע", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (MessageBox.Show($"למחוק את '{sel.name}'?", "אישור מחיקה", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            try
            {
                await _api.DeletePipelineAsync(sel.id);
                await LoadAsync();
                Notify("מחיקה", $"Pipeline '{sel.name}' נמחק");
            }
            catch (Exception ex)
            {
                NotifyError("שגיאה במחיקה", ex.Message);
            }
        }

        private void Notify(string title, string msg, string? details = null)
        {
            if (Application.Current.MainWindow is MainWindow mw)
                mw.AddInfoNotification(title, msg, details);
        }

        private void NotifyError(string title, string details)
        {
            if (Application.Current.MainWindow is MainWindow mw)
                mw.AddErrorNotification(title, "בדוק את פרטי השגיאה", details);
        }
        
        private void PipelinesList_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
                DeleteSelected_Click(sender, e);
        }
    }
}