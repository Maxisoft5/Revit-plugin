using BimGuard.Core;
using Xunit;

namespace BimGuard.Unit.Tests;

public sealed class AuditEngineTests
{
    private readonly AuditEngine engine = new();

    private static ElementSnapshot Element(long id = 1, string category = "Walls", string? mark = "W-01",
        string? level = "01", double? area = null) => new(id, $"uid-{id}", category, $"Element {id}", mark, level, area);

    private AuditReport Run(params ElementSnapshot[] elements) => engine.Run(new("model-1", "Жилой корпус", elements));

    [Fact]
    public void Valid_model_has_no_issues() => Assert.Empty(Run(Element()).Issues);

    [Fact]
    public void Empty_model_is_valid() => Assert.Empty(Run().Issues);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Blank_mark_produces_error(string? mark)
    {
        var issue = Assert.Single(Run(Element(mark: mark)).Issues);
        Assert.Equal("BG001", issue.RuleId);
        Assert.Equal(IssueSeverity.Error, issue.Severity);
    }

    [Theory]
    [InlineData("Walls")]
    [InlineData("Floors")]
    [InlineData("Doors")]
    [InlineData("Windows")]
    [InlineData("Rooms")]
    public void Required_mark_covers_all_supported_categories(string category)
        => Assert.Contains(Run(Element(category: category, mark: null, area: 10)).Issues, x => x.RuleId == "BG001");

    [Theory]
    [InlineData("W-01", "W-01")]
    [InlineData("w-01", "W-01")]
    [InlineData(" W-01 ", "W-01")]
    public void Duplicate_marks_are_normalized_and_flag_both_elements(string first, string second)
    {
        var issues = Run(Element(1, mark: first), Element(2, mark: second)).Issues;
        Assert.Equal(2, issues.Count);
        Assert.All(issues, x => Assert.Equal("BG002", x.RuleId));
        Assert.All(issues, x => Assert.Equal(IssueSeverity.Warning, x.Severity));
    }

    [Fact]
    public void Same_mark_in_different_categories_is_not_duplicate()
        => Assert.Empty(Run(Element(1), Element(2, "Doors")).Issues);

    [Fact]
    public void Blank_marks_do_not_create_duplicate_warnings()
        => Assert.DoesNotContain(Run(Element(1, mark: ""), Element(2, mark: " ")).Issues, x => x.RuleId == "BG002");

    [Theory]
    [InlineData("Walls")]
    [InlineData("Floors")]
    [InlineData("Doors")]
    [InlineData("Windows")]
    [InlineData("Rooms")]
    public void Missing_level_is_reported(string category)
        => Assert.Contains(Run(Element(category: category, level: null, area: 10)).Issues, x => x.RuleId == "BG003");

    [Theory]
    [InlineData(null)]
    [InlineData(0d)]
    [InlineData(-1d)]
    public void Room_without_positive_area_is_error(double? area)
        => Assert.Contains(Run(Element(category: "Rooms", area: area)).Issues, x => x.RuleId == "BG004");

    [Fact]
    public void Placed_room_is_valid() => Assert.Empty(Run(Element(category: "Rooms", area: 12.5)).Issues);

    [Fact]
    public void Area_rule_does_not_apply_to_walls() => Assert.Empty(Run(Element(area: 0)).Issues);

    [Fact]
    public void Unsupported_category_is_ignored()
        => Assert.Empty(Run(Element(category: "Furniture", mark: null, level: null)).Issues);

    [Fact]
    public void Checked_count_excludes_unsupported_categories()
        => Assert.Equal(1, Run(Element(1), Element(2, category: "Furniture")).ElementCount);

    [Fact]
    public void Ambiguous_unique_ids_are_rejected()
        => Assert.Throws<ArgumentException>(() => Run(Element(1), Element(2) with { UniqueId = "uid-1" }));

    [Fact]
    public void Issue_preserves_element_identity()
    {
        var issue = Assert.Single(Run(Element(42, mark: null)).Issues);
        Assert.Equal(42, issue.ElementId);
        Assert.Equal("uid-42", issue.UniqueId);
        Assert.Equal("Element 42", issue.ElementName);
    }

    [Fact]
    public void Report_preserves_model_metadata_and_counts()
    {
        var report = Run(Element(1, mark: null), Element(2, level: null));
        Assert.Equal("model-1", report.DocumentKey);
        Assert.Equal("Жилой корпус", report.Title);
        Assert.Equal(2, report.ElementCount);
        Assert.Equal(1, report.ErrorCount);
        Assert.Equal(1, report.WarningCount);
        Assert.NotEqual(Guid.Empty, report.Id);
        Assert.InRange(report.CreatedAt, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(1));
    }

    [Fact]
    public void Run_does_not_modify_snapshot()
    {
        var element = Element(mark: "  ");
        Run(element);
        Assert.Equal("  ", element.Mark);
    }

    [Fact]
    public void Duplicate_element_ids_are_rejected()
        => Assert.Throws<ArgumentException>(() => Run(Element(1), Element(1)));

    [Fact]
    public void Blank_document_key_is_rejected()
        => Assert.Throws<ArgumentException>(() => engine.Run(new(" ", "Model", [])));

    [Fact]
    public void Missing_model_is_rejected()
        => Assert.Throws<ArgumentNullException>(() => engine.Run(null!));

    [Fact]
    public void Issue_order_is_deterministic()
    {
        var first = Run(Element(2, mark: null), Element(1, level: null));
        var second = Run(Element(1, level: null), Element(2, mark: null));
        Assert.Equal(first.Issues, second.Issues);
    }
}
