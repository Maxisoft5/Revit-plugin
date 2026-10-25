using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BimGuard.Infrastructure;
using Microsoft.Win32;

namespace BimGuard.Revit;

/// <summary>Экспортирует входные данные проверки в JSON без изменения модели.</summary>
[Transaction(TransactionMode.ReadOnly)]
public sealed class ExportSnapshotCommand : IExternalCommand
{
    /// <inheritdoc />
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        try
        {
            var document = commandData.Application.ActiveUIDocument?.Document;
            if (document is null || document.IsFamilyDocument) return Result.Cancelled;
            var dialog = new SaveFileDialog
            {
                FileName = "bim-guard-snapshot",
                Filter = "JSON (*.json)|*.json",
                DefaultExt = ".json",
                AddExtension = true,
                OverwritePrompt = true
            };
            if (dialog.ShowDialog() != true) return Result.Cancelled;
            ReportFiles.WriteModel(RevitSnapshotReader.Read(document), dialog.FileName);
            TaskDialog.Show("BIM Guard", "Снимок сохранён. Его можно проверить через CLI или открыть в демоприложении.");
            return Result.Succeeded;
        }
        catch (Exception exception) { message = CommandErrors.Record(exception); return Result.Failed; }
    }
}
