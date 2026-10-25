[CmdletBinding()]
param([ValidateSet('Debug','Release')][string]$Configuration = 'Release')

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$previousLocation = Get-Location
$previousRollForward = $env:DOTNET_ROLL_FORWARD
try {
    Set-Location -LiteralPath $projectRoot
    # На машине с SDK 10 допускается запуск net8-тестов через установленный более новый runtime.
    # Если .NET 8 установлен, выбирается он. В Revit всегда используется runtime самого Revit.
    $env:DOTNET_ROLL_FORWARD = 'Major'
    dotnet restore BimGuard.slnx
    if ($LASTEXITCODE -ne 0) { throw 'Не удалось восстановить зависимости.' }
    dotnet build BimGuard.slnx -c $Configuration --no-restore --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Сборка завершилась с ошибкой.' }
    dotnet test BimGuard.slnx -c $Configuration --no-build --no-restore --nologo --logger trx --results-directory artifacts/test-results
    if ($LASTEXITCODE -ne 0) { throw 'Обнаружены падающие тесты.' }
}
finally {
    $env:DOTNET_ROLL_FORWARD = $previousRollForward
    Set-Location -LiteralPath $previousLocation
}
