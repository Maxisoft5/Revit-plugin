# Разработка

## Среда

- Windows x64.
- .NET SDK 10.0.400, зафиксированный в `global.json`.
- .NET 8 Runtime / Desktop Runtime для выполнения под целевой версией.
- Revit 2026 для проверки команд внутри приложения.

Все проекты целятся в `net8.0`; WPF и Revit — в `net8.0-windows`. Скрипты разрешают Major roll-forward, если установлен только более новый runtime. Для воспроизведения среды Revit проверку следует выполнять также под .NET 8.

## Сборка

```powershell
dotnet restore BimGuard.slnx
dotnet build BimGuard.slnx -c Release --no-restore
```

Зависимости фиксируются в `packages.lock.json` каждого проекта.

Revit API подключается из установленного Revit 2026. Если локальная установка не найдена, используются reference-пакеты `Nice3point.Revit.Api.RevitAPI` и `Nice3point.Revit.Api.RevitAPIUI` версии 2026.4.10.

Явное указание каталога API:

```powershell
dotnet build src/BimGuard.Revit -p:RevitApiPath="C:\Program Files\Autodesk\Revit 2026"
```

Reference-пакеты содержат API-ссылки для компиляции. Для загрузки плагина нужен установленный Revit. Его API DLL не копируются в дистрибутив.

## Тесты

```powershell
.\scripts\Test.ps1
```

Скрипт восстанавливает зависимости, собирает Release и запускает весь набор. Результаты TRX сохраняются в `artifacts/test-results/`.

| Уровень | Проект | Покрытие |
| --- | --- | --- |
| Юнит | `BimGuard.Unit.Tests` | Правила, валидация, идентичность элементов, поиск и MVVM |
| Интеграционные | `BimGuard.Integration.Tests` | SQLite, ограничения и транзакции, миграции, сериализация, CSV, WPF/XAML на STA-потоке |
| E2E | `BimGuard.E2E.Tests` | Отдельный процесс CLI, БД, выгрузки и коды завершения |

Интеграционные тесты используют отдельные временные SQLite-файлы. WPF-тест материализует строку истории и проверяет загрузку привязок без показа окна. CLI-тесты запускают отдельный процесс приложения.

Изменения поведения разрабатываются через Red → Green → Refactor. После завершения изменения выполняется полный прогон. Загрузку плагина и работу с RVT проверяют отдельно по [сценарию Revit](REVIT-ACCEPTANCE.md).

## CLI

### Аудит

```powershell
$env:DOTNET_ROLL_FORWARD = 'Major'
dotnet run --project src/BimGuard.Cli -- audit samples/model-with-issues.json --db artifacts/demo.db --json artifacts/report.json --csv artifacts/report.csv --fail-on-errors
```

| Параметр | Назначение |
| --- | --- |
| `--db` | Файл истории SQLite; по умолчанию пользовательский каталог BIM Guard |
| `--json` | Путь полного JSON-отчёта |
| `--csv` | Путь полного CSV-отчёта |
| `--fail-on-errors` | Завершить команду кодом 2 при наличии ошибок модели |

История:

```powershell
dotnet run --project src/BimGuard.Cli -- history --db artifacts/demo.db --document demo-residential-a
```

Без `--document` возвращаются последние проверки всех документов выбранной БД.

Коды завершения:

| Код | Значение |
| --- | --- |
| 0 | Команда выполнена |
| 1 | Ошибка ввода, файловой системы или БД |
| 2 | Найдены ошибки модели при `--fail-on-errors` |

Предупреждения сами по себе не дают код 2. Без `--fail-on-errors` команда аудита возвращает 0 и при найденных замечаниях. Исходный файл, БД и файлы выгрузок должны иметь разные пути.

### Формат снимка

```json
{
  "documentKey": "building-a",
  "title": "Корпус А",
  "elements": [
    {
      "id": 101,
      "uniqueId": "element-101",
      "category": "Walls",
      "name": "Стена",
      "mark": "W-01",
      "level": "Первый этаж",
      "areaSquareMeters": null
    }
  ]
}
```

ID должны быть положительными и уникальными. UniqueId не должны повторяться. Поддерживаемые категории: `Walls`, `Floors`, `Doors`, `Windows`, `Rooms`. Для помещений `mark` содержит номер, `areaSquareMeters` — площадь в м².

## Упаковка

```powershell
.\scripts\Package.ps1
```

Если полный прогон уже выполнен для текущего состояния:

```powershell
.\scripts\Package.ps1 -SkipTests
```

Публикация выполняется для `win-x64`, без собственного .NET runtime. Скрипт проверяет наличие `e_sqlite3.dll` и отсутствие RevitAPI.dll/RevitAPIUI.dll в пакете.

## CI

`.github/workflows/ci.yml` запускается при push и pull request на Windows. Устанавливаются SDK 10 и 8, выполняются сборка, тесты и упаковка. ZIP плагина и TRX сохраняются как артефакты.

## Документация API

- [Autodesk: внешние команды](https://help.autodesk.com/cloudhelp/2026/ENU/Revit-API/files/Revit_API_Developers_Guide/Introduction/Add_In_Integration/Revit_API_Revit_API_Developers_Guide_Introduction_Add_In_Integration_External_Commands_html.html)
- [Autodesk: регистрация .addin](https://help.autodesk.com/cloudhelp/2026/ENU/Revit-API/files/Revit_API_Developers_Guide/Introduction/Add_In_Integration/Revit_API_Revit_API_Developers_Guide_Introduction_Add_In_Integration_Add_in_Registration_html.html)
- [Autodesk: Document](https://help.autodesk.com/cloudhelp/2026/ENU/Revit-API-MainReference/files/html/db03274b-a107-aa32-9034-f3e0df4bb1ec.htm)
- [Reference-пакеты Nice3point](https://github.com/Nice3point/RevitApi)
