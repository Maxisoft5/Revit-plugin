namespace BimGuard.Core;

/// <summary>Краткие сведения о сохранённой проверке.</summary>
public sealed record AuditRunSummary(Guid Id, string DocumentKey, string Title, DateTimeOffset CreatedAt,
    int ElementCount, int ErrorCount, int WarningCount);

/// <summary>Хранилище отчётов; бизнес-логика не зависит от конкретной БД.</summary>
public interface IAuditRepository
{
    /// <summary>Атомарно сохраняет запуск вместе со всеми замечаниями.</summary>
    /// <param name="report">Полный отчёт с уникальным ID запуска.</param>
    void Save(AuditReport report);
    /// <summary>Читает полный отчёт или возвращает null, если ID не найден.</summary>
    /// <param name="id">ID запуска проверки.</param>
    AuditReport? Get(Guid id);
    /// <summary>Возвращает последние проверки в обратном хронологическом порядке.</summary>
    /// <param name="documentKey">Ограничение по документу; null означает все документы.</param>
    /// <param name="limit">Максимальное количество записей от 1 до 500.</param>
    IReadOnlyList<AuditRunSummary> Recent(string? documentKey = null, int limit = 50);
}

/// <summary>Сценарий проверки и сохранения истории, общий для CLI и Revit.</summary>
public sealed class AuditService(IAuditRepository repository)
{
    /// <summary>Проверяет снимок и сохраняет результат до передачи интерфейсу.</summary>
    /// <param name="model">Данные проверяемой модели.</param>
    /// <returns>Сохранённый отчёт.</returns>
    public AuditReport Run(ModelSnapshot model)
    {
        var report = new AuditEngine().Run(model);
        repository.Save(report);
        return report;
    }
}
