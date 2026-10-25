using BimGuard.Core;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BimGuard.Presentation;

/// <summary>Состояние окна отчёта: фильтры, выбранное замечание и история документа.</summary>
public sealed class ReportViewModel : INotifyPropertyChanged
{
    private readonly IAuditRepository repository;
    private readonly bool hostAvailable;
    private string query = "";
    private int severityIndex;
    private AuditIssue? selectedIssue;
    private AuditReport report;
    private IReadOnlyList<AuditIssue> visibleIssues;
    private string status = "Проверка завершена. Модель не изменена.";

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Создаёт состояние окна и читает историю только текущего документа.</summary>
    /// <param name="report">Уже сохранённый результат проверки.</param>
    /// <param name="repository">Хранилище исторических отчётов.</param>
    /// <param name="hostAvailable">Доступен ли Revit для навигации к элементу.</param>
    public ReportViewModel(AuditReport report, IAuditRepository repository, bool hostAvailable)
    {
        this.report = report;
        this.repository = repository;
        this.hostAvailable = hostAvailable;
        visibleIssues = report.Issues;
        History = repository.Recent(report.DocumentKey);
    }

    /// <summary>Текущий полный отчёт; поиск не меняет итоговые счётчики.</summary>
    public AuditReport Report => report;
    /// <summary>Замечания после применения фильтров, в исходном порядке.</summary>
    public IReadOnlyList<AuditIssue> VisibleIssues => visibleIssues;
    /// <summary>Поисковая строка по ID, правилу и тексту замечания.</summary>
    public string Query
    {
        get => query;
        set { if (query == value) return; query = value ?? ""; Notify(); RefreshFilter(); }
    }
    /// <summary>0 — всё, 1 — ошибки, 2 — предупреждения.</summary>
    public int SeverityIndex
    {
        get => severityIndex;
        set
        {
            if (value is < 0 or > 2) return;
            if (severityIndex == value) return;
            severityIndex = value;
            Notify();
            RefreshFilter();
        }
    }
    /// <summary>Замечание для навигации или просмотра подробностей.</summary>
    public AuditIssue? SelectedIssue
    {
        get => selectedIssue;
        set { selectedIssue = value; Notify(); Notify(nameof(CanNavigate)); }
    }
    /// <summary>Последние 50 проверок текущего документа.</summary>
    public IReadOnlyList<AuditRunSummary> History { get; }
    /// <summary>Выбранная строка истории.</summary>
    public AuditRunSummary? SelectedHistory { get; set; }
    /// <summary>Навигация доступна только в Revit и при выбранном замечании.</summary>
    public bool CanNavigate => hostAvailable && SelectedIssue is not null;
    /// <summary>Текущий результат действий с отчётом.</summary>
    public string Status => status;

    /// <summary>Загружает исторический отчёт; модель повторно не сканируется.</summary>
    public void LoadSelectedHistory()
    {
        if (SelectedHistory is null) return;
        var loaded = repository.Get(SelectedHistory.Id);
        if (loaded is null || loaded.DocumentKey != report.DocumentKey)
        {
            status = "Отчёт не найден для текущего документа.";
            Notify(nameof(Status));
            return;
        }
        report = loaded;
        query = "";
        severityIndex = 0;
        status = "Открыт сохранённый отчёт. Состояние модели могло измениться.";
        RefreshFilter();
        Notify(nameof(Report));
        Notify(nameof(Query));
        Notify(nameof(SeverityIndex));
        Notify(nameof(Status));
    }

    private void RefreshFilter()
    {
        visibleIssues = IssueFilter.Apply(report.Issues, query, severityIndex switch
        {
            1 => IssueSeverity.Error,
            2 => IssueSeverity.Warning,
            _ => null
        });
        // Фильтрация не должна оставлять скрытый элемент активным для навигации.
        SelectedIssue = null;
        Notify(nameof(VisibleIssues));
    }

    private void Notify([CallerMemberName] string? property = null)
        => PropertyChanged?.Invoke(this, new(property));
}
