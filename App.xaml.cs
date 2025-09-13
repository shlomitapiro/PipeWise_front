using System;
using System.Windows;

namespace PipeWiseClient
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            this.DispatcherUnhandledException += (s, args) =>
            {
                MessageBox.Show("שגיאה לא מטופלת: " + args.Exception.Message,
                    "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
            };

            AppDomain.CurrentDomain.UnhandledException += (s, args2) =>
            {
                if (args2.ExceptionObject is Exception ex)
                {
                    MessageBox.Show("שגיאה כללית: " + ex.Message,
                        "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };

            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (s, args3) =>
            {
                MessageBox.Show("שגיאה מתוך Task: " + args3.Exception?.Message,
                    "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
                args3.SetObserved();
            };

            var win = new MainWindow();
            this.MainWindow = win;
            win.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try { base.OnExit(e); }
            catch (Exception ex)
            {
                MessageBox.Show($"שגיאה בסגירת האפליקציה: {ex.Message}",
                    "שגיאה", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
