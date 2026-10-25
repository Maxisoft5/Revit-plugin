using BimGuard.Core;
using Xunit;
namespace BimGuard.Unit.Tests;

public sealed class IssueFilterTests
{
    private static readonly AuditIssue[] Issues =
    [
        new("BG001", IssueSeverity.Error, 100, "u1", "Doors", "Дверь Д-01", "Не заполнена марка"),
        new("BG003", IssueSeverity.Warning, 200, "u2", "Walls", "Стена", "Нет уровня")
    ];

    [Theory]
    [InlineData(null, 2)]
    [InlineData("", 2)]
    [InlineData("   ", 2)]
    [InlineData("дверь", 1)]
    [InlineData(" BG001 ", 1)]
    [InlineData("walls", 1)]
    [InlineData("200", 1)]
    [InlineData("уровня", 1)]
    [InlineData("unknown", 0)]
    public void Search_uses_meaningful_fields(string? query, int count)
        => Assert.Equal(count, IssueFilter.Apply(Issues, query, null).Count);

    [Fact]
    public void Severity_and_search_are_combined()
    {
        Assert.Empty(IssueFilter.Apply(Issues, "дверь", IssueSeverity.Warning));
        Assert.Equal(100, Assert.Single(IssueFilter.Apply(Issues, null, IssueSeverity.Error)).ElementId);
    }

    [Fact]
    public void Filter_does_not_change_original_report()
    {
        IssueFilter.Apply(Issues, "не найдено", null);
        Assert.Equal(2, Issues.Length);
    }
}
