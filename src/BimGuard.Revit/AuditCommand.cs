using System.Diagnostics;
using System.IO;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BimGuard.Core;
using BimGuard.Infrastructure;
using BimGuard.Wpf;

namespace BimGuard.Revit;

/// <summary>Проверяет активный документ и показывает модальный WPF-отчёт.</summary>
[Transaction(TransactionMode.ReadOnly)]
public sealed class AuditCommand : IExternalCommand
{
    /// <inheritdoc />
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        try
        {
            var uiDocument = commandData.Application.ActiveUIDocument;
            if (uiDocument is null || uiDocument.Document.IsFamilyDocument) return Result.Cancelled;
            var model = RevitSnapshotReader.Read(uiDocument.Document);
            var repository = new SqliteAuditRepository(AppPaths.Database);
            var report = new AuditService(repository).Run(model);
            var window = new ReportWindow(report, repository, true);
            new WindowInteropHelper(window).Owner = commandData.Application.MainWindowHandle;
            window.ShowDialog();

            if (window.RequestedIssue is { } requested)
            {
                // Для старых отчётов числовой ID может указывать на другой элемент.
                // UniqueId проверяется в исходном документе после закрытия модального окна.
                var element = uiDocument.Document.GetElement(requested.UniqueId);
                if (element is null)
                    TaskDialog.Show("BIM Guard", "Элемент больше не существует. Запустите новую проверку.");
                else
                {
                    uiDocument.Selection.SetElementIds(new[] { element.Id });
                    try { uiDocument.ShowElements(element.Id); }
                    catch (Autodesk.Revit.Exceptions.ArgumentException)
                    {
                        TaskDialog.Show("BIM Guard", "Элемент выбран, но не может быть показан на виде. Возможно, помещение не размещено.");
                    }
                }
            }
            return Result.Succeeded;
        }
        catch (Exception exception)
        {
            message = CommandErrors.Record(exception);
            return Result.Failed;
        }
    }
}

/// <summary>Сохраняет диагностический стек отдельно от короткого сообщения Revit.</summary>
internal static class CommandErrors
{
    internal static string Record(Exception exception)
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BimGuard");
        var text = "BIM Guard: " + exception.Message;
        try
        {
            Directory.CreateDirectory(folder);
            File.AppendAllText(Path.Combine(folder, "errors.log"), $"{DateTimeOffset.Now:O}\n{exception}\n\n");
            text += "\nПодробности: " + Path.Combine(folder, "errors.log");
        }
        catch (Exception logException) when (logException is IOException or UnauthorizedAccessException)
        {
            Trace.TraceError("BIM Guard: {0}; ошибка записи журнала: {1}", exception, logException);
        }
        return text.Length > 1000 ? text[..1000] : text;
    }
}
