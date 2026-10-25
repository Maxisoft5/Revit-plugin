namespace BimGuard.Core;

internal static class RuleScope
{
    private static readonly HashSet<string> Categories = ["Walls", "Floors", "Doors", "Windows", "Rooms"];
    internal static IEnumerable<ElementSnapshot> Elements(ModelSnapshot model)
        => model.Elements.Where(x => Categories.Contains(x.Category));

    internal static AuditIssue Issue(ElementSnapshot element, string rule, IssueSeverity severity, string message)
        => new(rule, severity, element.Id, element.UniqueId, element.Category, element.Name, message);
}

internal sealed class RequiredMarkRule : IAuditRule
{
    public string Id => "BG001";
    public IEnumerable<AuditIssue> Evaluate(ModelSnapshot model) => RuleScope.Elements(model)
        .Where(x => string.IsNullOrWhiteSpace(x.Mark))
        .Select(x => RuleScope.Issue(x, Id, IssueSeverity.Error,
            x.Category == "Rooms" ? "Не заполнен номер помещения." : "Не заполнена марка экземпляра."));
}

internal sealed class DuplicateMarkRule : IAuditRule
{
    public string Id => "BG002";
    public IEnumerable<AuditIssue> Evaluate(ModelSnapshot model)
    {
        // Марки сравниваются внутри категории: марка двери не конфликтует с маркой стены.
        // Группировка даёт линейный обход вместо попарного сравнения всех элементов.
        var groups = RuleScope.Elements(model).Where(x => !string.IsNullOrWhiteSpace(x.Mark))
            .GroupBy(x => (x.Category, Mark: x.Mark!.Trim().ToUpperInvariant()));
        foreach (var group in groups.Where(x => x.Count() > 1))
            foreach (var element in group)
                yield return RuleScope.Issue(element, Id, IssueSeverity.Warning,
                    $"Марка «{element.Mark!.Trim()}» повторяется у {group.Count()} элементов категории {element.Category}.");
    }
}

internal sealed class MissingLevelRule : IAuditRule
{
    public string Id => "BG003";
    public IEnumerable<AuditIssue> Evaluate(ModelSnapshot model) => RuleScope.Elements(model)
        .Where(x => string.IsNullOrWhiteSpace(x.Level))
        .Select(x => RuleScope.Issue(x, Id, IssueSeverity.Warning, "Не определён уровень элемента."));
}

internal sealed class RoomAreaRule : IAuditRule
{
    public string Id => "BG004";
    public IEnumerable<AuditIssue> Evaluate(ModelSnapshot model) => RuleScope.Elements(model)
        .Where(x => x.Category == "Rooms" && (x.AreaSquareMeters is not { } area || !double.IsFinite(area) || area <= 0))
        .Select(x => RuleScope.Issue(x, Id, IssueSeverity.Error,
            "Нет положительной площади: проверьте размещение и замкнутость границ помещения."));
}
