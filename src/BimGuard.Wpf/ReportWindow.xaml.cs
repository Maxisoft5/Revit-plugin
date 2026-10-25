using System.IO;
using System.Windows;
using BimGuard.Core;
using BimGuard.Infrastructure;
using BimGuard.Presentation;
using Microsoft.Win32;

namespace BimGuard.Wpf;

/// <summary>Общий модальный WPF-интерфейс для Revit и самостоятельной демонстрации.</summary>
public partial class ReportWindow : Window
{
    private readonly ReportViewModel viewModel;

    /// <summary>Создаёт окно без обращения к Revit API.</summary>
    /// <param name="report">Сохранённый результат проверки.</param>
    /// <param name="repository">Хранилище истории.</param>
    /// <param name="hostAvailable">Разрешена ли навигация после закрытия окна.</param>
    public ReportWindow(AuditReport report, IAuditRepository repository, bool hostAvailable)
    {
        InitializeComponent();
        viewModel = new(report, repository, hostAvailable);
        DataContext = viewModel;
    }

    /// <summary>Элемент для навигации после закрытия модального окна или null.</summary>
    public AuditIssue? RequestedIssue { get; private set; }

    private void Navigate(object sender, RoutedEventArgs args)
    {
        if (!viewModel.CanNavigate) return;
        RequestedIssue = viewModel.SelectedIssue;
        // Revit API вызывается командой ПОСЛЕ выхода из ShowDialog, в разрешённом API-контексте.
        DialogResult = true;
    }

    private void CloseWindow(object sender, RoutedEventArgs args) => Close();
    private void ExportCsv(object sender, RoutedEventArgs args) => Export(false);
    private void ExportJson(object sender, RoutedEventArgs args) => Export(true);

    private void Export(bool json)
    {
        var dialog = new SaveFileDialog
        {
            FileName = "bim-guard-report-" + viewModel.Report.CreatedAt.ToString("yyyyMMdd-HHmm"),
            Filter = json ? "JSON (*.json)|*.json" : "CSV (*.csv)|*.csv",
            AddExtension = true,
            DefaultExt = json ? ".json" : ".csv",
            OverwritePrompt = true
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            if (json) ReportFiles.WriteJson(viewModel.Report, dialog.FileName);
            else ReportFiles.WriteCsv(viewModel.Report, dialog.FileName);
            MessageBox.Show(this, "Полный отчёт сохранён.", "BIM Guard", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, exception.Message, "Не удалось сохранить отчёт", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LoadHistory(object sender, RoutedEventArgs args)
    {
        try { viewModel.LoadSelectedHistory(); }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "Не удалось открыть историю", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
