[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$PackagePath,
    [string]$AddinsRoot = (Join-Path $env:APPDATA 'Autodesk/Revit/Addins/2026'),
    [string]$InstallPath = (Join-Path $env:LOCALAPPDATA 'BimGuard/Revit2026')
)

$ErrorActionPreference = 'Stop'
if (-not $PackagePath) {
    # Скрипт работает как из репозитория, так и из распакованного ZIP.
    if (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'BimGuard.Revit.dll')) { $PackagePath = $PSScriptRoot }
    else { $PackagePath = Join-Path (Split-Path -Parent $PSScriptRoot) 'artifacts/revit-2026' }
}
$sourcePath = [IO.Path]::GetFullPath($PackagePath)
$targetPath = [IO.Path]::GetFullPath($InstallPath)
$manifestRoot = [IO.Path]::GetFullPath($AddinsRoot)
foreach ($file in @('BimGuard.Revit.dll','BimGuard.addin','e_sqlite3.dll')) {
    if (-not (Test-Path -LiteralPath (Join-Path $sourcePath $file))) { throw "В пакете отсутствует $file. Сначала запустите Package.ps1." }
}
if (Get-Process -Name Revit -ErrorAction SilentlyContinue) { throw 'Закройте Revit перед установкой.' }
if ($sourcePath.TrimEnd('\') -eq $targetPath.TrimEnd('\')) { throw 'Каталог установки должен отличаться от каталога пакета.' }
$manifestPath = Join-Path $manifestRoot 'BimGuard.addin'
if ($PSCmdlet.ShouldProcess("$targetPath; $manifestPath", 'Установить BIM Guard для Revit 2026')) {
    New-Item -ItemType Directory -Path $targetPath -Force | Out-Null
    New-Item -ItemType Directory -Path $manifestRoot -Force | Out-Null
    # Сохраняем весь набор управляемых и нативных зависимостей рядом с плагином.
    Copy-Item -Path (Join-Path $sourcePath '*') -Destination $targetPath -Recurse -Force
    [xml]$manifest = Get-Content -LiteralPath (Join-Path $sourcePath 'BimGuard.addin') -Raw -Encoding UTF8
    $manifest.RevitAddIns.AddIn.Assembly = Join-Path $targetPath 'BimGuard.Revit.dll'
    $manifest.Save($manifestPath)
    Write-Output "Установлено: $manifestPath"
}
