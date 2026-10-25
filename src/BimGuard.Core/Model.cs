namespace BimGuard.Core;

/// <summary>Неизменяемый снимок экземпляра элемента, не зависящий от Revit API.</summary>
/// <param name="Id">Числовой идентификатор элемента внутри документа.</param>
/// <param name="UniqueId">Устойчивый идентификатор Revit.</param>
/// <param name="Category">Нормализованная категория: Walls, Floors, Doors, Windows или Rooms.</param>
/// <param name="Name">Имя элемента для отчёта.</param>
/// <param name="Mark">Марка экземпляра; для помещений — номер.</param>
/// <param name="Level">Название уровня; null означает отсутствие привязки.</param>
/// <param name="AreaSquareMeters">Площадь помещения в м²; null для остальных категорий.</param>
public sealed record ElementSnapshot(long Id, string UniqueId, string Category, string Name,
    string? Mark, string? Level, double? AreaSquareMeters = null);

/// <summary>Данные одного документа, полученные одним проходом по модели.</summary>
/// <param name="DocumentKey">Идентификатор источника для группировки истории.</param>
/// <param name="Title">Отображаемое имя документа.</param>
/// <param name="Elements">Экземпляры поддерживаемых категорий.</param>
public sealed record ModelSnapshot(string DocumentKey, string Title, IReadOnlyList<ElementSnapshot> Elements);

/// <summary>Важность замечания: ошибка блокирует готовность модели, предупреждение требует проверки.</summary>
public enum IssueSeverity { Warning, Error }

/// <summary>Замечание к конкретному элементу и проверенному бизнес-правилу.</summary>
public sealed record AuditIssue(string RuleId, IssueSeverity Severity, long ElementId,
    string UniqueId, string Category, string ElementName, string Message);

/// <summary>Результат проверки со снимком замечаний для сохранения в истории.</summary>
public sealed record AuditReport(Guid Id, string DocumentKey, string Title, DateTimeOffset CreatedAt,
    int ElementCount, IReadOnlyList<AuditIssue> Issues)
{
    /// <summary>Число ошибок во всём отчёте, до применения фильтров интерфейса.</summary>
    public int ErrorCount => Issues.Count(x => x.Severity == IssueSeverity.Error);
    /// <summary>Число предупреждений во всём отчёте.</summary>
    public int WarningCount => Issues.Count(x => x.Severity == IssueSeverity.Warning);
}

/// <summary>Контракт расширяемой проверки BIM-данных.</summary>
public interface IAuditRule
{
    /// <summary>Стабильный код правила в БД и выгрузках.</summary>
    string Id { get; }
    /// <summary>Проверяет снимок модели без изменения исходных данных.</summary>
    /// <param name="model">Снимок проверяемого документа.</param>
    /// <returns>Замечания данного правила.</returns>
    IEnumerable<AuditIssue> Evaluate(ModelSnapshot model);
}

/// <summary>Запускает правила в определённом порядке и формирует отчёт.</summary>
public sealed class AuditEngine
{
    private readonly IAuditRule[] rules = [new RequiredMarkRule(), new DuplicateMarkRule(), new MissingLevelRule(), new RoomAreaRule()];

    /// <summary>Выполняет проверку поддерживаемых элементов.</summary>
    /// <param name="model">Данные документа.</param>
    /// <returns>Полный отчёт без сохранения в БД.</returns>
    /// <exception cref="ArgumentException">Снимок не содержит ключа документа или содержит повторные ID.</exception>
    public AuditReport Run(ModelSnapshot model)
    {
        ModelValidation.Validate(model);

        // Порядок отчёта не зависит от порядка обхода Revit: это упрощает сравнение запусков.
        var issues = rules.SelectMany(rule => rule.Evaluate(model))
            .OrderBy(x => x.RuleId, StringComparer.Ordinal).ThenBy(x => x.ElementId).ToArray();
        return new(Guid.NewGuid(), model.DocumentKey, model.Title, DateTimeOffset.UtcNow,
            RuleScope.Elements(model).Count(), Array.AsReadOnly(issues));
    }
}
