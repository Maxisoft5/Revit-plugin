using System.Globalization;
namespace BimGuard.Core;

/// <summary>Независимая от WPF фильтрация замечаний для интерфейса отчёта.</summary>
public static class IssueFilter
{
    /// <summary>Ищет по ID, правилу, категории, имени и описанию с учётом важности.</summary>
    /// <param name="issues">Полный список замечаний.</param>
    /// <param name="query">Поисковая строка; пустая строка означает отсутствие поиска.</param>
    /// <param name="severity">Важность; null означает все замечания.</param>
    /// <returns>Отфильтрованный список с сохранением исходного порядка.</returns>
    public static IReadOnlyList<AuditIssue> Apply(IReadOnlyList<AuditIssue> issues, string? query, IssueSeverity? severity)
    {
        ArgumentNullException.ThrowIfNull(issues);
        var text = query?.Trim() ?? "";
        return issues.Where(x => (severity is null || x.Severity == severity)
            && (text.Length == 0 || new[] { x.RuleId, x.Category, x.ElementName, x.Message,
                x.ElementId.ToString(CultureInfo.InvariantCulture) }
                .Any(field => field.Contains(text, StringComparison.OrdinalIgnoreCase)))).ToArray();
    }
}
