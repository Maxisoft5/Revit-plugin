[CmdletBinding()]
param([switch]$SkipTests)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$previousLocation = Get-Location
try {
    Set-Location -LiteralPath $projectRoot
    if (-not $SkipTests) { & (Join-Path $PSScriptRoot 'Test.ps1') -Configuration Release }
    $packagePath = Join-Path $projectRoot 'artifacts/revit-2026'
    New-Item -ItemType Directory -Path $packagePath -Force | Out-Null
    dotnet publish src/BimGuard.Revit/BimGuard.Revit.csproj -c Release -r win-x64 --self-contained false -o $packagePath
    if ($LASTEXITCODE -ne 0) { throw 'Не удалось собрать пакет Revit.' }
    # Autodesk API предоставляется процессом Revit. Его DLL не должны попасть в дистрибутив.
    $forbidden = @('RevitAPI.dll','RevitAPIUI.dll')
    foreach ($name in $forbidden) {
        if (Test-Path -LiteralPath (Join-Path $packagePath $name)) { throw "В пакете обнаружена лишняя DLL Autodesk: $name" }
    }
    if (-not (Test-Path -LiteralPath (Join-Path $packagePath 'e_sqlite3.dll'))) {
        throw 'Отсутствует нативная x64-библиотека SQLite.'
    }
    Copy-Item -LiteralPath (Join-Path $projectRoot 'src/BimGuard.Revit/BimGuard.addin') -Destination $packagePath -Force
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Install-Addin.ps1') -Destination $packagePath -Force
    Copy-Item -LiteralPath (Join-Path $projectRoot 'docs/REVIT-ACCEPTANCE.md') -Destination $packagePath -Force
    Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md') -Destination $packagePath -Force
    Copy-Item -LiteralPath (Join-Path $projectRoot 'CHANGELOG.md') -Destination $packagePath -Force
    Copy-Item -LiteralPath (Join-Path $projectRoot 'docs') -Destination $packagePath -Recurse -Force
    $zipPath = Join-Path $projectRoot 'artifacts/BimGuard-Revit2026.zip'
    Compress-Archive -Path (Join-Path $packagePath '*') -DestinationPath $zipPath -Force
    Write-Output "Пакет: $zipPath"
}
finally { Set-Location -LiteralPath $previousLocation }
