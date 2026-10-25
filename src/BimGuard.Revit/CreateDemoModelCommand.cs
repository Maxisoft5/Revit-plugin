using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace BimGuard.Revit;

/// <summary>Создаёт небольшой фрагмент для демонстрации проверок в новом пустом проекте.</summary>
[Transaction(TransactionMode.Manual)]
public sealed class CreateDemoModelCommand : IExternalCommand
{
    /// <inheritdoc />
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        try
        {
            var document = commandData.Application.ActiveUIDocument?.Document;
            if (document is null || document.IsFamilyDocument || document.IsReadOnly) return Result.Cancelled;
            using var existing = new FilteredElementCollector(document);
            if (existing.WherePasses(new ElementMulticategoryFilter(RevitSnapshotReader.Categories.Keys.ToList()))
                .WhereElementIsNotElementType().Any())
            {
                TaskDialog.Show("BIM Guard", "Откройте новый пустой проект: команда не добавляет демо-данные в существующую архитектурную модель.");
                return Result.Cancelled;
            }
            var answer = TaskDialog.Show("BIM Guard — демо-фрагмент",
                "В активном проекте будут созданы уровень, четыре стены и два помещения с намеренными ошибками. Продолжить?",
                TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No);
            if (answer != TaskDialogResult.Yes) return Result.Cancelled;

            using var types = new FilteredElementCollector(document);
            var wallType = types.OfClass(typeof(WallType)).Cast<WallType>().FirstOrDefault(x => x.Kind == WallKind.Basic)
                ?? throw new InvalidOperationException("В шаблоне отсутствует базовый тип стены.");
            using var transaction = new Transaction(document, "BIM Guard: создать демо-фрагмент");
            if (transaction.Start() != TransactionStatus.Started) throw new InvalidOperationException("Не удалось начать транзакцию.");
            var level = Level.Create(document, 0);
            level.Name = "BG DEMO — 01";
            double Feet(double meters) => UnitUtils.ConvertToInternalUnits(meters, UnitTypeId.Meters);
            var points = new[] { new XYZ(0, 0, 0), new XYZ(Feet(9), 0, 0),
                new XYZ(Feet(9), Feet(6), 0), new XYZ(0, Feet(6), 0) };
            var marks = new[] { "BG-W01", "BG-W01", "", "BG-W04" };
            for (var i = 0; i < points.Length; i++)
            {
                var wall = Wall.Create(document, Line.CreateBound(points[i], points[(i + 1) % points.Length]),
                    wallType.Id, level.Id, Feet(3), 0, false, false);
                wall.get_Parameter(BuiltInParameter.ALL_MODEL_MARK).Set(marks[i]);
            }
            // Границы помещений рассчитываются после появления стен в документе.
            document.Regenerate();
            var room = document.Create.NewRoom(level, new UV(Feet(4.5), Feet(3)));
            room.Number = "101";
            room.Name = "BG — Холл";
            var phase = document.Phases.Cast<Phase>().Last();
            var unplaced = document.Create.NewRoom(phase);
            unplaced.Number = "102";
            unplaced.Name = "BG — Не размещено";
            if (transaction.Commit() != TransactionStatus.Committed)
                throw new InvalidOperationException("Revit не подтвердил создание демо-фрагмента.");
            TaskDialog.Show("BIM Guard", "Созданы 4 стены и 2 помещения. Нажмите «Проверить модель»: ожидаются пустая марка, два дубликата и неразмещённое помещение.");
            return Result.Succeeded;
        }
        catch (Exception exception) { message = CommandErrors.Record(exception); return Result.Failed; }
    }
}
