using System.Reflection;
using Autodesk.Revit.UI;

namespace BimGuard.Revit;

/// <summary>Регистрирует команды BIM Guard на ленте Revit.</summary>
public sealed class Application : IExternalApplication
{
    /// <inheritdoc />
    public Result OnStartup(UIControlledApplication application)
    {
        var panel = application.CreateRibbonPanel("BIM Guard");
        var assembly = Assembly.GetExecutingAssembly().Location;
        Add(panel, assembly, "BimGuard.Audit", "Проверить\nмодель", typeof(AuditCommand),
            "Проверить марки, дубликаты, уровни и помещения. Открыть отчёт и историю.");
        Add(panel, assembly, "BimGuard.Snapshot", "Экспорт\nснимка", typeof(ExportSnapshotCommand),
            "Выгрузить поддерживаемые категории в JSON для воспроизведения проверки без Revit.");
        Add(panel, assembly, "BimGuard.Demo", "Создать\nдемо-фрагмент", typeof(CreateDemoModelCommand),
            "В пустом проекте создать тестовый фрагмент с намеренными ошибками. Модель будет изменена.");
        return Result.Succeeded;
    }

    /// <inheritdoc />
    public Result OnShutdown(UIControlledApplication application) => Result.Succeeded;

    private static void Add(RibbonPanel panel, string assembly, string id, string title, Type command, string tooltip)
    {
        var data = new PushButtonData(id, title, assembly, command.FullName!)
        {
            ToolTip = tooltip,
            AvailabilityClassName = typeof(ProjectCommandAvailability).FullName
        };
        panel.AddItem(data);
    }
}

/// <summary>Включает команды только при открытом проекте, исключая редактор семейств.</summary>
public sealed class ProjectCommandAvailability : IExternalCommandAvailability
{
    /// <inheritdoc />
    public bool IsCommandAvailable(UIApplication applicationData, Autodesk.Revit.DB.CategorySet selectedCategories)
        => applicationData.ActiveUIDocument is { Document.IsFamilyDocument: false };
}
