using System.IO;
using System.Text;
using System.Text.Json;
using BimGuard.Core;
using BimGuard.Infrastructure;
using Microsoft.Data.Sqlite;
using Xunit;

namespace BimGuard.Integration.Tests;

// Каждый тест получает отдельный файл настоящей SQLite, включая проверки ограничений и отката.
public sealed class StorageTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "BimGuard-tests", Guid.NewGuid().ToString("N"));
    private string Db => Path.Combine(folder, "history.db");
    private SqliteAuditRepository Store() => new(Db);
    private static AuditReport Report(string key = "model-1") => new AuditEngine().Run(new(key, "Корпус А; 'test'",
        [new(1, "uid-1", "Walls", "=SUM(A1)", null, null)]));

    public StorageTests() => Directory.CreateDirectory(folder);
    public void Dispose() => Directory.Delete(folder, true);

    [Fact]
    public void Saves_and_loads_report_with_cyrillic_and_all_issues()
    {
        var report = Report();
        Store().Save(report);
        var loaded = Store().Get(report.Id);
        Assert.NotNull(loaded);
        Assert.Equal(report.Title, loaded.Title);
        Assert.Equal(report.CreatedAt, loaded.CreatedAt);
        Assert.Equal(report.Issues, loaded.Issues);
        Assert.Equal(report.ErrorCount, loaded.ErrorCount);
    }

    [Fact]
    public void Unknown_report_returns_null() => Assert.Null(Store().Get(Guid.NewGuid()));

    [Fact]
    public void Reopening_store_does_not_erase_history()
    {
        var report = Report();
        Store().Save(report);
        Assert.Single(Store().Recent());
        Assert.NotNull(Store().Get(report.Id));
    }

    [Fact]
    public void Recent_filters_document_and_orders_by_time()
    {
        var store = Store();
        var first = Report() with { CreatedAt = DateTimeOffset.UtcNow.AddDays(-1) };
        var last = Report();
        store.Save(first);
        store.Save(Report("other"));
        store.Save(last);
        var rows = store.Recent("model-1", 1);
        Assert.Equal(last.Id, Assert.Single(rows).Id);
        Assert.Equal(2, store.Recent("model-1").Count);
        Assert.Equal(3, store.Recent().Count);
    }

    [Fact]
    public void Duplicate_report_id_is_rejected_without_changing_data()
    {
        var report = Report();
        var store = Store();
        store.Save(report);
        Assert.Throws<SqliteException>(() => store.Save(report));
        Assert.Single(store.Recent());
        Assert.Equal(report.Issues.Count, store.Get(report.Id)!.Issues.Count);
    }

    [Fact]
    public void Failure_in_issue_insert_rolls_back_entire_run()
    {
        var store = Store();
        var report = Report();
        // Второе замечание нарушает NOT NULL после успешной записи первого.
        var invalid = report with { Issues = [report.Issues[0], report.Issues[1] with { Message = null! }] };
        Assert.Throws<SqliteException>(() => store.Save(invalid));
        Assert.Null(store.Get(report.Id));
        Assert.Empty(store.Recent());
    }

    [Fact]
    public void Empty_report_roundtrips_without_issues()
    {
        var store = Store();
        var report = new AuditEngine().Run(new("empty", "Empty", []));
        store.Save(report);
        Assert.Empty(store.Get(report.Id)!.Issues);
        Assert.Equal(0, Assert.Single(store.Recent()).ElementCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(501)]
    public void Invalid_history_limit_is_rejected(int limit)
        => Assert.Throws<ArgumentOutOfRangeException>(() => Store().Recent(limit: limit));

    [Fact]
    public void Database_has_migration_and_foreign_key_constraint()
    {
        Store();
        using var connection = new SqliteConnection($"Data Source={Db};Pooling=False;Foreign Keys=True");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version";
        Assert.Equal(1L, command.ExecuteScalar());
        command.CommandText = "INSERT INTO Issues VALUES ('missing', 0, 'BG001', 1, 1, 'u', 'Walls', 'Wall', 'Error')";
        Assert.Throws<SqliteException>(() => command.ExecuteNonQuery());
    }

    [Fact]
    public void Newer_database_schema_is_not_downgraded()
    {
        Store();
        using (var connection = new SqliteConnection($"Data Source={Db};Pooling=False"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA user_version = 99";
            command.ExecuteNonQuery();
        }
        Assert.Throws<InvalidOperationException>(() => Store());
    }

    [Fact]
    public void Service_persists_engine_result()
    {
        var store = Store();
        var report = new AuditService(store).Run(new("service", "Model", []));
        Assert.NotNull(store.Get(report.Id));
    }

    [Fact]
    public void Json_report_contains_readable_cyrillic_and_rule_ids()
    {
        var path = Path.Combine(folder, "report.json");
        ReportFiles.WriteJson(Report(), path);
        using var json = JsonDocument.Parse(File.ReadAllText(path));
        Assert.Equal(2, json.RootElement.GetProperty("issues").GetArrayLength());
        Assert.Contains("Корпус", File.ReadAllText(path));
    }

    [Fact]
    public void Model_snapshot_roundtrips_for_offline_reproduction()
    {
        var path = Path.Combine(folder, "snapshot.json");
        var model = new ModelSnapshot("stable-key", "Тестовая модель", [
            new(123, "real-uid", "Rooms", "Помещение", "101", "Этаж 1", 21.5)]);
        ReportFiles.WriteModel(model, path);
        var loaded = ReportFiles.ReadModel(path);
        Assert.Equal(model.DocumentKey, loaded.DocumentKey);
        Assert.Equal(model.Elements, loaded.Elements);
    }

    [Fact]
    public void Csv_quotes_delimiters_and_neutralizes_formula_injection()
    {
        var path = Path.Combine(folder, "report.csv");
        var report = Report() with { Issues = [new("BG001", IssueSeverity.Error, 1, "u", "Walls", "=SUM(A1)", "Проверка; \"марки\"")] };
        ReportFiles.WriteCsv(report, path);
        var bytes = File.ReadAllBytes(path);
        Assert.Equal(Encoding.UTF8.GetPreamble(), bytes.Take(3).ToArray());
        var csv = File.ReadAllText(path);
        Assert.Contains("\"'=SUM(A1)\"", csv);
        Assert.Contains("\"Проверка; \"\"марки\"\"\"", csv);
        Assert.Equal(2, File.ReadAllLines(path).Length);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"documentKey\":\"a\",\"title\":\"b\",\"elements\":null}")]
    [InlineData("invalid json")]
    public void Invalid_snapshot_is_rejected(string content)
    {
        var path = Path.Combine(folder, "invalid.json");
        File.WriteAllText(path, content);
        Assert.Throws<InvalidDataException>(() => ReportFiles.ReadModel(path));
    }
}
