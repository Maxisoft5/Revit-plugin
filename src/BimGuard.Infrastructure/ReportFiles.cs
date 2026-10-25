using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using BimGuard.Core;

namespace BimGuard.Infrastructure;

/// <summary>Чтение снимков и переносимые выгрузки результатов.</summary>
public static class ReportFiles
{
    /// <summary>Сохраняет снимок модели для воспроизведения проверки вне Revit.</summary>
    /// <param name="model">Структурно корректные входные данные.</param>
    /// <param name="path">Целевой JSON-файл.</param>
    public static void WriteModel(ModelSnapshot model, string path)
    {
        ModelValidation.Validate(model);
        File.WriteAllText(PreparePath(path), JsonSerializer.Serialize(model, JsonOptions), new UTF8Encoding(false));
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>Читает JSON-снимок с обязательными метаданными и уникальными ID элементов.</summary>
    /// <param name="path">Путь к JSON-файлу.</param>
    /// <returns>Структурно корректный снимок.</returns>
    /// <exception cref="InvalidDataException">JSON или структура снимка некорректны.</exception>
    public static ModelSnapshot ReadModel(string path)
    {
        try
        {
            var model = JsonSerializer.Deserialize<ModelSnapshot>(File.ReadAllText(path), JsonOptions)
                ?? throw new InvalidDataException("JSON не содержит снимок модели.");
            ModelValidation.Validate(model);
            return model;
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        {
            throw new InvalidDataException("Некорректный снимок модели: " + exception.Message, exception);
        }
    }

    /// <summary>Сохраняет полный JSON-отчёт. Вывод не предназначен для вставки в HTML.</summary>
    /// <param name="report">Результат проверки.</param>
    /// <param name="path">Целевой файл.</param>
    public static void WriteJson(AuditReport report, string path)
        => File.WriteAllText(PreparePath(path), JsonSerializer.Serialize(report, JsonOptions), new UTF8Encoding(false));

    /// <summary>Создаёт CSV для Excel с UTF-8 BOM, разделителем «;» и защитой от формул.</summary>
    /// <param name="report">Полный отчёт; фильтры интерфейса не влияют на выгрузку.</param>
    /// <param name="path">Целевой файл.</param>
    public static void WriteCsv(AuditReport report, string path)
    {
        using var writer = new StreamWriter(PreparePath(path), false, new UTF8Encoding(true));
        writer.WriteLine("Правило;Важность;ID элемента;UniqueId;Категория;Элемент;Описание");
        foreach (var issue in report.Issues)
            writer.WriteLine(string.Join(";", new[] { issue.RuleId, issue.Severity.ToString(),
                issue.ElementId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                issue.UniqueId, issue.Category, issue.ElementName, issue.Message }.Select(Cell)));
    }

    private static string Cell(string value)
    {
        // Кавычки CSV не останавливают вычисление формулы в Excel; апостроф делает значение текстом.
        var trimmed = value.TrimStart();
        if ((trimmed.Length > 0 && "=+-@".Contains(trimmed[0])) || value.StartsWith('\t') || value.StartsWith('\r'))
            value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private static string PreparePath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        return fullPath;
    }
}
