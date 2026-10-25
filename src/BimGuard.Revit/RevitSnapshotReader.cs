using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using BimGuard.Core;

namespace BimGuard.Revit;

/// <summary>Преобразует живую модель в DTO, не передавая объекты Revit в бизнес-логику.</summary>
internal static class RevitSnapshotReader
{
    internal static readonly Dictionary<BuiltInCategory, string> Categories = new()
    {
        [BuiltInCategory.OST_Walls] = "Walls",
        [BuiltInCategory.OST_Floors] = "Floors",
        [BuiltInCategory.OST_Doors] = "Doors",
        [BuiltInCategory.OST_Windows] = "Windows",
        [BuiltInCategory.OST_Rooms] = "Rooms"
    };

    internal static ModelSnapshot Read(Document document)
    {
        // Весь Revit API вызывается синхронно внутри Execute; API не является потокобезопасным.
        // Quick filter выполняется до материализации элементов и исключает типы семейств.
        using var collector = new FilteredElementCollector(document);
        var elements = collector.WherePasses(new ElementMulticategoryFilter(Categories.Keys.ToList()))
            .WhereElementIsNotElementType().Select(element =>
            {
                var category = Categories[(BuiltInCategory)element.Category.Id.Value];
                var levelId = element.LevelId;
                if (levelId == ElementId.InvalidElementId)
                {
                    var parameter = element.get_Parameter(BuiltInParameter.FAMILY_LEVEL_PARAM);
                    if (parameter?.StorageType == StorageType.ElementId) levelId = parameter.AsElementId();
                }
                var level = document.GetElement(levelId) as Level;
                var room = element as Room;
                var mark = room is not null ? room.Number : element.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.AsString();
                double? area = room is null ? null : UnitUtils.ConvertFromInternalUnits(room.Area, UnitTypeId.SquareMeters);
                return new ElementSnapshot(element.Id.Value, element.UniqueId, category, element.Name,
                    mark, level?.Name, area);
            }).ToArray();

        // Название файла не подходит для ключа: оно меняется при переименовании.
        return new(document.CreationGUID.ToString(), document.Title, elements);
    }
}
