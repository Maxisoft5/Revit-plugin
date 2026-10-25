[CmdletBinding()]
param([string]$ModelPath)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$previousLocation = Get-Location
$previousRollForward = $env:DOTNET_ROLL_FORWARD
try {
    Set-Location -LiteralPath $projectRoot
    $env:DOTNET_ROLL_FORWARD = 'Major'
    $arguments = @()
    if ($ModelPath) {
        if ($ModelPath -eq '--open') { $arguments += '--open' }
        else { $arguments += [IO.Path]::GetFullPath($ModelPath) }
    }
    dotnet run --project src/BimGuard.Demo -- @arguments
    if ($LASTEXITCODE -ne 0) { throw 'Не удалось запустить демоприложение.' }
}
finally {
    $env:DOTNET_ROLL_FORWARD = $previousRollForward
    Set-Location -LiteralPath $previousLocation
}
