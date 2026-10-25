using BimGuard.Core;
using BimGuard.Presentation;
using Xunit;
namespace BimGuard.Unit.Tests;

public sealed class ReportViewModelTests
{
    private static AuditReport Report() => new AuditEngine().Run(new("model", "Model",
        [new(1, "u1", "Walls", "Стена", null, null)]));

    [Fact]
    public void Search_and_severity_filter_visible_issues()
    {
        var view = new ReportViewModel(Report(), new MemoryRepository(), true) { SeverityIndex = 1 };
        Assert.Equal("BG001", Assert.Single(view.VisibleIssues).RuleId);
        view.Query = "Нет совпадений";
        Assert.Empty(view.VisibleIssues);
        Assert.Equal(2, view.Report.Issues.Count);
    }

    [Fact]
    public void Changing_filter_clears_selection_and_notifies_view()
    {
        var view = new ReportViewModel(Report(), new MemoryRepository(), true);
        view.SelectedIssue = view.Report.Issues[0];
        var changed = new List<string?>();
        view.PropertyChanged += (_, args) => changed.Add(args.PropertyName);
        view.Query = "unknown";
        Assert.Null(view.SelectedIssue);
        Assert.False(view.CanNavigate);
        Assert.Contains("VisibleIssues", changed);
    }

    [Fact]
    public void Demo_disables_navigation_even_with_selection()
    {
        var report = Report();
        var view = new ReportViewModel(report, new MemoryRepository(), false) { SelectedIssue = report.Issues[0] };
        Assert.False(view.CanNavigate);
    }

    [Fact]
    public void Host_allows_navigation_for_selected_issue()
    {
        var report = Report();
        var view = new ReportViewModel(report, new MemoryRepository(), true) { SelectedIssue = report.Issues[0] };
        Assert.True(view.CanNavigate);
    }

    [Fact]
    public void Loading_history_replaces_report_and_resets_filters()
    {
        var report = Report();
        var previous = report with { Id = Guid.NewGuid(), Issues = [] };
        var store = new MemoryRepository();
        store.Save(previous);
        var view = new ReportViewModel(report, store, true) { Query = "BG001", SeverityIndex = 1 };
        view.SelectedHistory = Assert.Single(view.History);
        view.LoadSelectedHistory();
        Assert.Equal(previous.Id, view.Report.Id);
        Assert.Empty(view.VisibleIssues);
        Assert.Equal("", view.Query);
        Assert.Equal(0, view.SeverityIndex);
    }

    [Fact]
    public void Missing_history_keeps_current_report_and_reports_problem()
    {
        var report = Report();
        var view = new ReportViewModel(report, new MemoryRepository(), true);
        view.SelectedHistory = new(Guid.NewGuid(), "model", "Missing", DateTimeOffset.UtcNow, 0, 0, 0);
        view.LoadSelectedHistory();
        Assert.Equal(report.Id, view.Report.Id);
        Assert.Contains("не найден", view.Status);
    }

    private sealed class MemoryRepository : IAuditRepository
    {
        private readonly Dictionary<Guid, AuditReport> reports = [];
        public void Save(AuditReport report) => reports.Add(report.Id, report);
        public AuditReport? Get(Guid id) => reports.GetValueOrDefault(id);
        public IReadOnlyList<AuditRunSummary> Recent(string? documentKey = null, int limit = 50)
            => reports.Values.Where(x => x.DocumentKey == documentKey).Select(x =>
                new AuditRunSummary(x.Id, x.DocumentKey, x.Title, x.CreatedAt, x.ElementCount, x.ErrorCount, x.WarningCount)).ToArray();
    }
}
