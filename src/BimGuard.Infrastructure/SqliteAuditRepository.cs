using System.Globalization;
using BimGuard.Core;
using Microsoft.Data.Sqlite;

namespace BimGuard.Infrastructure;

/// <summary>SQLite-хранилище истории с версионируемой схемой и атомарной записью.</summary>
public sealed class SqliteAuditRepository : IAuditRepository
{
    private readonly string connectionString;

    /// <summary>Создаёт БД при необходимости и применяет миграцию версии 1.</summary>
    /// <param name="path">Путь к файлу SQLite. Родительские каталоги создаются автоматически.</param>
    /// <exception cref="InvalidOperationException">Версия схемы новее поддерживаемой.</exception>
    public SqliteAuditRepository(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = fullPath,
            ForeignKeys = true,
            Pooling = false,
            DefaultTimeout = 10
        }.ToString();
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        using var version = Command(connection, transaction, "PRAGMA user_version");
        var current = Convert.ToInt32(version.ExecuteScalar(), CultureInfo.InvariantCulture);
        if (current > 1)
            throw new InvalidOperationException("Версия БД новее поддерживаемой. Обновите BIM Guard.");
        if (current == 0)
        {
            using var migration = Command(connection, transaction, """
                CREATE TABLE AuditRuns (
                    Id TEXT PRIMARY KEY NOT NULL,
                    DocumentKey TEXT NOT NULL,
                    Title TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    ElementCount INTEGER NOT NULL CHECK (ElementCount >= 0)
                );
                CREATE TABLE Issues (
                    RunId TEXT NOT NULL REFERENCES AuditRuns(Id) ON DELETE CASCADE,
                    Ordinal INTEGER NOT NULL,
                    RuleId TEXT NOT NULL,
                    Severity INTEGER NOT NULL CHECK (Severity IN (0, 1)),
                    ElementId INTEGER NOT NULL,
                    UniqueId TEXT NOT NULL,
                    Category TEXT NOT NULL,
                    ElementName TEXT NOT NULL,
                    Message TEXT NOT NULL,
                    PRIMARY KEY (RunId, Ordinal)
                );
                CREATE INDEX IX_AuditRuns_Document_Time ON AuditRuns(DocumentKey, CreatedAt DESC);
                PRAGMA user_version = 1;
                """);
            migration.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    /// <inheritdoc />
    public void Save(AuditReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        using var connection = Open();
        // Если запись любого замечания не удалась, в истории не останется частичного запуска.
        using var transaction = connection.BeginTransaction();
        using var run = Command(connection, transaction,
            "INSERT INTO AuditRuns VALUES ($id, $document, $title, $created, $count)");
        run.Parameters.AddWithValue("$id", report.Id.ToString());
        run.Parameters.AddWithValue("$document", report.DocumentKey);
        run.Parameters.AddWithValue("$title", report.Title);
        run.Parameters.AddWithValue("$created", report.CreatedAt.ToUniversalTime().ToString("O"));
        run.Parameters.AddWithValue("$count", report.ElementCount);
        run.ExecuteNonQuery();
        using var issue = Command(connection, transaction,
            "INSERT INTO Issues VALUES ($run, $ordinal, $rule, $severity, $element, $unique, $category, $name, $message)");
        issue.Parameters.AddWithValue("$run", report.Id.ToString());
        foreach (var name in new[] { "$ordinal", "$severity", "$element" })
            issue.Parameters.Add(name, SqliteType.Integer);
        foreach (var name in new[] { "$rule", "$unique", "$category", "$name", "$message" })
            issue.Parameters.Add(name, SqliteType.Text);
        for (var i = 0; i < report.Issues.Count; i++)
        {
            var item = report.Issues[i];
            issue.Parameters["$ordinal"].Value = i;
            issue.Parameters["$rule"].Value = item.RuleId;
            issue.Parameters["$severity"].Value = (int)item.Severity;
            issue.Parameters["$element"].Value = item.ElementId;
            issue.Parameters["$unique"].Value = item.UniqueId;
            issue.Parameters["$category"].Value = item.Category;
            issue.Parameters["$name"].Value = item.ElementName;
            issue.Parameters["$message"].Value = item.Message is null ? DBNull.Value : item.Message;
            issue.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    /// <inheritdoc />
    public AuditReport? Get(Guid id)
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        using var run = Command(connection, transaction,
            "SELECT DocumentKey, Title, CreatedAt, ElementCount FROM AuditRuns WHERE Id = $id");
        run.Parameters.AddWithValue("$id", id.ToString());
        string document;
        string title;
        DateTimeOffset created;
        int count;
        using (var reader = run.ExecuteReader())
        {
            if (!reader.Read()) return null;
            document = reader.GetString(0);
            title = reader.GetString(1);
            created = DateTimeOffset.Parse(reader.GetString(2), CultureInfo.InvariantCulture);
            count = reader.GetInt32(3);
        }
        using var query = Command(connection, transaction, """
            SELECT RuleId, Severity, ElementId, UniqueId, Category, ElementName, Message
            FROM Issues WHERE RunId = $id ORDER BY Ordinal
            """);
        query.Parameters.AddWithValue("$id", id.ToString());
        var issues = new List<AuditIssue>();
        using (var reader = query.ExecuteReader())
            while (reader.Read())
                issues.Add(new(reader.GetString(0), (IssueSeverity)reader.GetInt32(1), reader.GetInt64(2),
                    reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.GetString(6)));
        transaction.Commit();
        return new(id, document, title, created, count, issues.AsReadOnly());
    }

    /// <inheritdoc />
    public IReadOnlyList<AuditRunSummary> Recent(string? documentKey = null, int limit = 50)
    {
        if (limit is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(limit));
        using var connection = Open();
        using var query = connection.CreateCommand();
        query.CommandText = """
            SELECT r.Id, r.DocumentKey, r.Title, r.CreatedAt, r.ElementCount,
              COALESCE(SUM(CASE WHEN i.Severity = 1 THEN 1 ELSE 0 END), 0),
              COALESCE(SUM(CASE WHEN i.Severity = 0 THEN 1 ELSE 0 END), 0)
            FROM AuditRuns r LEFT JOIN Issues i ON i.RunId = r.Id
            WHERE ($document IS NULL OR r.DocumentKey = $document)
            GROUP BY r.Id ORDER BY r.CreatedAt DESC, r.Id DESC LIMIT $limit
            """;
        query.Parameters.AddWithValue("$document", (object?)documentKey ?? DBNull.Value);
        query.Parameters.AddWithValue("$limit", limit);
        var rows = new List<AuditRunSummary>();
        using var reader = query.ExecuteReader();
        while (reader.Read())
            rows.Add(new(Guid.Parse(reader.GetString(0)), reader.GetString(1), reader.GetString(2),
                DateTimeOffset.Parse(reader.GetString(3), CultureInfo.InvariantCulture),
                reader.GetInt32(4), reader.GetInt32(5), reader.GetInt32(6)));
        return rows.AsReadOnly();
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(connectionString);
        try { connection.Open(); return connection; }
        catch { connection.Dispose(); throw; }
    }

    private static SqliteCommand Command(SqliteConnection connection, SqliteTransaction transaction, string sql)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        return command;
    }
}
