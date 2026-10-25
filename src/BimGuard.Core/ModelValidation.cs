namespace BimGuard.Core;

/// <summary>Общие структурные ограничения входных данных независимо от источника.</summary>
public static class ModelValidation
{
    /// <summary>Проверяет метаданные и идентичность элементов до запуска правил.</summary>
    /// <param name="model">Входной снимок.</param>
    /// <exception cref="ArgumentException">Не заполнены обязательные поля либо ID повторяется.</exception>
    public static void Validate(ModelSnapshot model)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentException.ThrowIfNullOrWhiteSpace(model.DocumentKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(model.Title);
        ArgumentNullException.ThrowIfNull(model.Elements);
        var ids = new HashSet<long>();
        var uniqueIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in model.Elements)
        {
            if (element is null || element.Id <= 0 || !ids.Add(element.Id))
                throw new ArgumentException("Элементы должны иметь уникальные положительные ID.", nameof(model));
            ArgumentException.ThrowIfNullOrWhiteSpace(element.UniqueId);
            if (!uniqueIds.Add(element.UniqueId))
                throw new ArgumentException("UniqueId элементов не должны повторяться.", nameof(model));
            ArgumentException.ThrowIfNullOrWhiteSpace(element.Category);
            ArgumentNullException.ThrowIfNull(element.Name);
        }
    }
}
