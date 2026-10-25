using System.Runtime.ExceptionServices;
using System.Threading;
using BimGuard.Core;
using BimGuard.Wpf;
using Xunit;

namespace BimGuard.Integration.Tests;

public sealed class WpfWindowTests
{
    [Fact]
    public void Real_report_window_loads_xaml_and_readonly_date_bindings_on_sta_thread()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var report = new AuditEngine().Run(new("test", "Проверка окна", []));
                var window = new ReportWindow(report, new EmptyRepository(), false);
                try
                {
                    var history = (System.Windows.Controls.DataGrid)window.FindName("HistoryTable");
                    history.Measure(new System.Windows.Size(1000, 500));
                    history.Arrange(new System.Windows.Rect(0, 0, 1000, 500));
                    history.UpdateLayout();
                    Assert.NotNull(history.ItemContainerGenerator.ContainerFromIndex(0));
                    Assert.NotNull(window.DataContext);
                }
                finally { window.Close(); }
            }
            catch (Exception exception) { failure = exception; }
        });
        // WPF требует STA. Тест не показывает окно и не отправляет ввод на рабочий стол.
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)), "WPF initialization timed out.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private sealed class EmptyRepository : IAuditRepository
    {
        public void Save(AuditReport report) { }
        public AuditReport? Get(Guid id) => null;
        public IReadOnlyList<AuditRunSummary> Recent(string? documentKey = null, int limit = 50)
            => [new(Guid.NewGuid(), "test", "Проверка окна", DateTimeOffset.UtcNow, 0, 0, 0)];
    }
}
