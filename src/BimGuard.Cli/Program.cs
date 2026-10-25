using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using BimGuard.Core;
using BimGuard.Infrastructure;
using Microsoft.Data.Sqlite;

namespace BimGuard.Cli;

/// <summary>Консольный вход: аудит JSON-снимков без установленного Revit.</summary>
public static class Program
{
    /// <summary>Возвращает 0 при успехе, 1 при ошибке ввода/хранения, 2 при найденных ошибках с fail-on-errors.</summary>
    /// <param name="args">Команда audit или history и её параметры.</param>
    public static int Main(string[] args)
    {
        Console.OutputEncoding = new UTF8Encoding(false);
        if (args.Length == 0 || args[0] is "--help" or "-h")
        {
            Console.WriteLine("""
                BIM Guard — проверка качества BIM-модели
                audit <model.json> [--db history.db] [--json report.json] [--csv report.csv] [--fail-on-errors]
                history [--db history.db] [--document document-key]
                Коды выхода: 0 — выполнено; 1 — ошибка; 2 — ошибки модели при --fail-on-errors.
                """);
            return 0;
        }
        try
        {
            if (args[0] is not ("audit" or "history")) throw new ArgumentException("Неизвестная команда. Используйте --help.");
            var audit = args[0] == "audit";
            if (audit && (args.Length < 2 || args[1].StartsWith("--", StringComparison.Ordinal)))
                throw new ArgumentException("Укажите JSON-снимок после audit.");
            var options = ParseOptions(args, audit ? 2 : 1, audit);
            var db = options.GetValueOrDefault("--db") ?? AppPaths.Database;
            if (!audit)
            {
                Print(new SqliteAuditRepository(db).Recent(options.GetValueOrDefault("--document")));
                return 0;
            }
            // Проверяем коллизии ДО открытия SQLite и до любой записи в файловую систему.
            var paths = new[] { args[1], db, options.GetValueOrDefault("--json"), options.GetValueOrDefault("--csv") }
                .Where(x => x is not null).Select(x => Path.GetFullPath(x!)).ToArray();
            if (paths.Distinct(StringComparer.OrdinalIgnoreCase).Count() != paths.Length)
                throw new ArgumentException("Пути источника, БД и файлов отчётов не должны совпадать.");
            var model = ReportFiles.ReadModel(args[1]);
            var report = new AuditService(new SqliteAuditRepository(db)).Run(model);
            if (options.TryGetValue("--json", out var json)) ReportFiles.WriteJson(report, json!);
            if (options.TryGetValue("--csv", out var csv)) ReportFiles.WriteCsv(report, csv!);
            Print(new
            {
                reportId = report.Id,
                document = report.Title,
                elements = report.ElementCount,
                errors = report.ErrorCount,
                warnings = report.WarningCount,
                database = Path.GetFullPath(db)
            });
            return options.ContainsKey("--fail-on-errors") && report.ErrorCount > 0 ? 2 : 0;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException
            or ArgumentException or SqliteException or InvalidOperationException)
        {
            Console.Error.WriteLine("Ошибка: " + exception.Message);
            return 1;
        }
    }

    private static Dictionary<string, string?> ParseOptions(string[] args, int start, bool audit)
    {
        var options = new Dictionary<string, string?>(StringComparer.Ordinal);
        for (var i = start; i < args.Length; i++)
        {
            var key = args[i];
            var allowed = key == "--db" || (audit ? key is "--json" or "--csv" or "--fail-on-errors" : key == "--document");
            if (!allowed || options.ContainsKey(key)) throw new ArgumentException("Неизвестный или повторный параметр: " + key);
            if (key == "--fail-on-errors") { options.Add(key, null); continue; }
            if (++i >= args.Length || args[i].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException("Отсутствует значение параметра " + key);
            options.Add(key, args[i]);
        }
        return options;
    }

    private static void Print<T>(T value) => Console.WriteLine(JsonSerializer.Serialize(value,
        new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }));
}
