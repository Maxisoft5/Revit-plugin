using System.Diagnostics;
using System.Text.Json;
using BimGuard.Cli;
using BimGuard.Infrastructure;
using Xunit;

namespace BimGuard.E2E.Tests;

// Это отдельный процесс CLI и реальная файловая БД, не вызов замоканного сервиса.
// Эти проверки не подменяют приёмку загрузки плагина и навигации внутри Revit.
public sealed class CliFlowTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "BimGuard-e2e", Guid.NewGuid().ToString("N"));
    private string Db => Path.Combine(folder, "history.db");
    private string Model => Path.Combine(folder, "model.json");

    public CliFlowTests() => Directory.CreateDirectory(folder);
    public void Dispose() => Directory.Delete(folder, true);

    private async Task<(int Code, string Out, string Error)> Execute(params string[] args)
    {
        var start = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.Environment["DOTNET_ROLL_FORWARD"] = "Major";
        // Runtime/deps тестового проекта содержат CLI и все его зависимости.
        start.ArgumentList.Add("exec");
        start.ArgumentList.Add("--runtimeconfig");
        start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "BimGuard.E2E.Tests.runtimeconfig.json"));
        start.ArgumentList.Add("--depsfile");
        start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "BimGuard.E2E.Tests.deps.json"));
        start.ArgumentList.Add(typeof(Program).Assembly.Location);
        foreach (var arg in args) start.ArgumentList.Add(arg);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch { process.Kill(true); throw; }
        return (process.ExitCode, await output, await error);
    }

    [Fact]
    public async Task Audit_exports_report_persists_history_and_returns_quality_gate_status()
    {
        File.WriteAllText(Model, """{"documentKey":"demo","title":"Корпус А","elements":[{"id":1,"uniqueId":"u1","category":"Walls","name":"Стена","mark":null,"level":"01"}]}""");
        var json = Path.Combine(folder, "out", "report.json");
        var csv = Path.Combine(folder, "out", "report.csv");
        var result = await Execute("audit", Model, "--db", Db, "--json", json, "--csv", csv, "--fail-on-errors");
        Assert.Equal(2, result.Code);
        Assert.Empty(result.Error);
        using var summary = JsonDocument.Parse(result.Out);
        Assert.Equal(1, summary.RootElement.GetProperty("errors").GetInt32());
        using var report = JsonDocument.Parse(File.ReadAllText(json));
        var id = report.RootElement.GetProperty("id").GetGuid();
        Assert.NotNull(new SqliteAuditRepository(Db).Get(id));
        Assert.Contains("BG001", File.ReadAllText(csv));
        var history = await Execute("history", "--db", Db, "--document", "demo");
        Assert.Equal(0, history.Code);
        using var runs = JsonDocument.Parse(history.Out);
        Assert.Equal(id, runs.RootElement[0].GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Valid_model_passes_quality_gate()
    {
        File.WriteAllText(Model, """{"documentKey":"clean","title":"Clean","elements":[{"id":1,"uniqueId":"u1","category":"Walls","name":"Wall","mark":"W01","level":"01"}]}""");
        var result = await Execute("audit", Model, "--db", Db, "--fail-on-errors");
        Assert.Equal(0, result.Code);
        using var json = JsonDocument.Parse(result.Out);
        Assert.Equal(0, json.RootElement.GetProperty("errors").GetInt32());
        Assert.Equal(0, json.RootElement.GetProperty("warnings").GetInt32());
    }

    [Fact]
    public async Task Invalid_input_fails_before_creating_database()
    {
        File.WriteAllText(Model, "{}");
        var result = await Execute("audit", Model, "--db", Db);
        Assert.Equal(1, result.Code);
        Assert.Contains("Ошибка", result.Error);
        Assert.False(File.Exists(Db));
    }

    [Fact]
    public async Task Colliding_output_paths_do_not_overwrite_source_or_create_database()
    {
        var original = """{"documentKey":"demo","title":"Demo","elements":[]}""";
        File.WriteAllText(Model, original);
        var result = await Execute("audit", Model, "--db", Db, "--json", Model);
        Assert.Equal(1, result.Code);
        Assert.Contains("совпад", result.Error);
        Assert.Equal(original, File.ReadAllText(Model));
        Assert.False(File.Exists(Db));
    }
}
