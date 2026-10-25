using System.IO;
using System.Windows;
using BimGuard.Core;
using BimGuard.Infrastructure;
using BimGuard.Wpf;
using Microsoft.Win32;

namespace BimGuard.Demo;

/// <summary>Запускает тот же WPF-отчёт на JSON-снимке без установленного Revit.</summary>
public partial class App : Application
{
    /// <inheritdoc />
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            string input;
            if (e.Args.Length == 1 && e.Args[0] == "--open")
            {
                var dialog = new OpenFileDialog { Filter = "BIM-снимок (*.json)|*.json" };
                if (dialog.ShowDialog() != true) { Shutdown(0); return; }
                input = dialog.FileName;
            }
            else input = e.Args.FirstOrDefault() ?? Path.Combine(AppContext.BaseDirectory, "Samples", "model-with-issues.json");
            var model = ReportFiles.ReadModel(input);
            var repository = new SqliteAuditRepository(AppPaths.Database);
            var report = new AuditService(repository).Run(model);
            var window = new ReportWindow(report, repository, false) { Title = "BIM Guard · Демонстрация без Revit" };
            window.ShowDialog();
            Shutdown(0);
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "BIM Guard — ошибка запуска", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }
}
