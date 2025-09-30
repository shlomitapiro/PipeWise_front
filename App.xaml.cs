using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using PipeWiseClient.Interfaces;
using PipeWiseClient.Services;
using PipeWiseClient.Models;
using PipeWiseClient.Services.Validators;

namespace PipeWiseClient
{
    public partial class App : Application
    {
        private ServiceProvider? _serviceProvider;

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
                    MessageBox.Show("שגיאת מערכת: " + ex.Message,
                        "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };

            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (s, args3) =>
            {
                MessageBox.Show("שגיאת Task לא נצפתה: " + args3.Exception?.Message,
                    "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
                args3.SetObserved();
            };

            var services = new ServiceCollection();

            // Final registration order
            services.AddSingleton<IApiClient, ApiClient>();
            services.AddSingleton<INotificationService, NotificationService>();
            services.AddSingleton<IFileService, FileService>();
            services.AddSingleton<IPipelineRepository, FilePipelineRepository>();
            services.AddSingleton<IPipelineService, PipelineService>();

            // Validators
            services.AddSingleton<PipeWiseClient.Interfaces.IValidator<PipelineConfig>, PipeWiseClient.Services.Validators.PipelineConfigValidator>();
            services.AddSingleton<PipeWiseClient.Interfaces.IValidator<ColumnSettings>, PipeWiseClient.Services.Validators.ColumnSettingsValidator>();
            services.AddSingleton<PipeWiseClient.Interfaces.IValidator<SourceConfig>, PipeWiseClient.Services.Validators.SourceConfigValidator>();

            // Patterns
            services.AddSingleton<ColumnOperationRegistry>();
            services.AddSingleton<PipeWiseClient.Interfaces.IColumnOperationStrategy, PipeWiseClient.Services.ColumnOperations.RemoveColumnStrategy>();
            services.AddSingleton<PipeWiseClient.Interfaces.IColumnOperationStrategy, PipeWiseClient.Services.ColumnOperations.ReplaceEmptyValuesStrategy>();
            services.AddSingleton<PipeWiseClient.Interfaces.IColumnOperationStrategy, PipeWiseClient.Services.ColumnOperations.ReplaceNullValuesStrategy>();
            services.AddSingleton<PipeWiseClient.Interfaces.IColumnOperationStrategy, PipeWiseClient.Services.ColumnOperations.SetNumericRangeStrategy>();
            services.AddSingleton<PipeWiseClient.Interfaces.IColumnOperationStrategy, PipeWiseClient.Services.ColumnOperations.SetDateFormatStrategy>();
            services.AddSingleton<PipeWiseClient.Interfaces.IColumnOperationStrategy, PipeWiseClient.Services.ColumnOperations.CategoricalEncodingStrategy>();
            services.AddSingleton<PipeWiseClient.Interfaces.IColumnOperationStrategy, PipeWiseClient.Services.ColumnOperations.MergeColumnsStrategy>();
            services.AddSingleton<PipeWiseClient.Interfaces.IColumnOperationStrategy, PipeWiseClient.Services.ColumnOperations.SplitFieldStrategy>();
            services.AddSingleton<PipeWiseClient.Interfaces.IColumnOperationStrategy, PipeWiseClient.Services.ColumnOperations.NormalizeNumericStrategy>();
            services.AddSingleton<PipeWiseClient.Interfaces.IColumnOperationStrategy, PipeWiseClient.Services.ColumnOperations.CastTypeStrategy>();
            services.AddSingleton<PipeWiseClient.Interfaces.IColumnOperationStrategy, PipeWiseClient.Services.ColumnOperations.RenameFieldStrategy>();
            services.AddSingleton<PipeWiseClient.Interfaces.IColumnOperationStrategy, PipeWiseClient.Services.ColumnOperations.RemoveInvalidIdentifierStrategy>();
            services.AddSingleton<PipeWiseClient.Interfaces.IColumnOperationStrategy, PipeWiseClient.Services.ColumnOperations.ToUppercaseStrategy>();
            services.AddSingleton<PipeWiseClient.Interfaces.IColumnOperationStrategy, PipeWiseClient.Services.ColumnOperations.ToLowercaseStrategy>();
            // Aggregations (registered via factories with different names)
            services.AddSingleton<PipeWiseClient.Interfaces.IColumnOperationStrategy>(sp => new PipeWiseClient.Services.ColumnOperations.AggregationOperationStrategy("sum", "סכום", "סיכום ערכים"));
            services.AddSingleton<PipeWiseClient.Interfaces.IColumnOperationStrategy>(sp => new PipeWiseClient.Services.ColumnOperations.AggregationOperationStrategy("average", "ממוצע", "ממוצע ערכים"));
            services.AddSingleton<PipeWiseClient.Interfaces.IColumnOperationStrategy>(sp => new PipeWiseClient.Services.ColumnOperations.AggregationOperationStrategy("min", "מינימום", "ערך מינימלי"));
            services.AddSingleton<PipeWiseClient.Interfaces.IColumnOperationStrategy>(sp => new PipeWiseClient.Services.ColumnOperations.AggregationOperationStrategy("max", "מקסימום", "ערך מקסימלי"));
            services.AddSingleton<PipeWiseClient.Interfaces.IColumnOperationStrategy>(sp => new PipeWiseClient.Services.ColumnOperations.AggregationOperationStrategy("median", "חציון", "חציון"));
            services.AddSingleton<PipeWiseClient.Interfaces.IColumnOperationStrategy>(sp => new PipeWiseClient.Services.ColumnOperations.AggregationOperationStrategy("std", "סטיית תקן", "סטיית תקן"));
            services.AddSingleton<PipeWiseClient.Interfaces.IColumnOperationStrategy>(sp => new PipeWiseClient.Services.ColumnOperations.AggregationOperationStrategy("variance", "שונות", "שונות"));
            services.AddSingleton<PipeWiseClient.Interfaces.IColumnOperationStrategy>(sp => new PipeWiseClient.Services.ColumnOperations.AggregationOperationStrategy("range", "טווח", "טווח"));
            services.AddSingleton<PipeWiseClient.Interfaces.IColumnOperationStrategy>(sp => new PipeWiseClient.Services.ColumnOperations.AggregationOperationStrategy("count_valid", "ספירת תקינים", "ספירת ערכים תקינים"));
            services.AddSingleton<PipeWiseClient.Interfaces.IColumnOperationStrategy>(sp => new PipeWiseClient.Services.ColumnOperations.AggregationOperationStrategy("count_distinct", "ספירת ייחודיים", "ספירת ערכים ייחודיים"));
            services.AddSingleton<PipeWiseClient.Interfaces.IColumnOperationStrategy>(sp => new PipeWiseClient.Services.ColumnOperations.AggregationOperationStrategy("most_common", "הנפוץ ביותר", "הערך הנפוץ"));

            // Dialogs: use factory methods (no DI registration for concrete windows)
            services.AddSingleton<IDialogFactory, DialogFactory>();

            services.AddTransient<MainWindow>();

            _serviceProvider = services.BuildServiceProvider();

            // Populate operation registry
            var registry = _serviceProvider.GetRequiredService<ColumnOperationRegistry>();
            var strategies = _serviceProvider.GetServices<PipeWiseClient.Interfaces.IColumnOperationStrategy>();
            foreach (var s in strategies) registry.Register(s);

            var win = _serviceProvider.GetRequiredService<MainWindow>();
            this.MainWindow = win;
            win.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try { base.OnExit(e); }
            catch (Exception ex)
            {
                MessageBox.Show($"שגיאה בעת סיום האפליקציה: {ex.Message}",
                    "שגיאה", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                _serviceProvider?.Dispose();
            }
        }
    }
}
