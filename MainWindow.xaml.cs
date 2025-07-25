using System;

using Microsoft.Win32;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows;

namespace PipeWiseClient
{
    public partial class MainWindow : Window
    {
        private readonly HttpClient _httpClient = new HttpClient();

        public MainWindow()
        {
            InitializeComponent();
        }

        private void BrowseFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog();
            if (dialog.ShowDialog() == true)
            {
                FilePathTextBox.Text = dialog.FileName;
            }
        }

        private async void RunPipeline_Click(object sender, RoutedEventArgs e)
        {
            string filePath = FilePathTextBox.Text;
            string configJson = ConfigTextBox.Text;

            if (string.IsNullOrWhiteSpace(filePath) || string.IsNullOrWhiteSpace(configJson))
            {
                MessageBox.Show("נא לבחור קובץ ולהזין קונפיגורציה", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var content = new MultipartFormDataContent();
            content.Add(new ByteArrayContent(File.ReadAllBytes(filePath)), "file", Path.GetFileName(filePath));
            content.Add(new StringContent(configJson, Encoding.UTF8, "application/json"), "config");

            try
            {
                var response = await _httpClient.PostAsync("http://127.0.0.1:8000/run-pipeline", content);
                string result = await response.Content.ReadAsStringAsync();
                ResultTextBlock.Text = $"✅ תוצאה:\n{result}";
            }
            catch (Exception ex)
            {
                ResultTextBlock.Text = $"❌ שגיאה: {ex.Message}";
            }
        }
    }
}
