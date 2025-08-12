using System;
using System.Windows;

namespace PipeWiseClient
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                base.OnStartup(e);
                
                // הצגת החלון הראשי
                var mainWindow = new MainWindow();
                mainWindow.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"שגיאה בהפעלת האפליקציה: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}", 
                               "שגיאה קריטית", 
                               MessageBoxButton.OK, 
                               MessageBoxImage.Error);
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                base.OnExit(e);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"שגיאה בסגירת האפליקציה: {ex.Message}", 
                               "שגיאה", 
                               MessageBoxButton.OK, 
                               MessageBoxImage.Warning);
            }
        }
    }
}