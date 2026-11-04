# Instala Perfiles AISC (compilado en .\dist) en las versiones de Revit indicadas que estén instaladas.
#   .\install-revit.ps1                  -> 2023..2027 (las que existan en %AppData%\Autodesk\Revit\Addins)
#   .\install-revit.ps1 -Versions 2025   -> solo Revit 2025
# Requiere haber ejecutado antes .\build-revit.ps1 (o descargar el paquete de GitHub Actions en .\dist).
param([string[]]$Versions = @("2023", "2024", "2025", "2026", "2027"))
$ErrorActionPreference = "Stop"

foreach ($v in $Versions) {
    $src = Join-Path $PSScriptRoot "dist\Revit$v"
    $addins = Join-Path $env:APPDATA "Autodesk\Revit\Addins\$v"
    if (-not (Test-Path $src)) { Write-Host "Revit ${v}: no hay paquete en $src (ejecute build-revit.ps1)" -ForegroundColor Yellow; continue }
    if (-not (Test-Path $addins)) { Write-Host "Revit ${v}: no está instalado, se omite" -ForegroundColor DarkGray; continue }

    New-Item -ItemType Directory -Force -Path (Join-Path $addins "PerfilesAisc") | Out-Null
    Copy-Item (Join-Path $src "PerfilesAisc\*.dll") (Join-Path $addins "PerfilesAisc") -Force
    Copy-Item (Join-Path $src "PerfilesAisc.addin") $addins -Force
    Write-Host "Revit ${v}: Perfiles AISC instalado" -ForegroundColor Green
}
