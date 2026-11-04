# Compila Perfiles AISC para Revit 2023 a 2027.
# Los paquetes quedan en .\dist\Revit<versión>\ listos para copiar a la carpeta Addins.
#   .\build-revit.ps1                 -> todas las versiones
#   .\build-revit.ps1 -Versions 2025  -> solo Revit 2025
param([string[]]$Versions = @("2023", "2024", "2025", "2026", "2027"))
$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$p = "PerfilesAisc"

foreach ($v in $Versions) {
    Write-Host "== $p - Revit $v ==" -ForegroundColor Cyan
    dotnet build (Join-Path $root "src\$p\$p.csproj") -c Release -p:RevitVersion=$v
    if ($LASTEXITCODE -ne 0) { throw "Falló la compilación de $p para Revit $v" }

    $out = Join-Path $root "src\$p\bin\Release\Revit$v"
    $dist = Join-Path $root "dist\Revit$v"
    New-Item -ItemType Directory -Force -Path (Join-Path $dist $p) | Out-Null
    Copy-Item (Join-Path $out "$p.dll") (Join-Path $dist $p) -Force
    Copy-Item (Join-Path $out "$p.Core.dll") (Join-Path $dist $p) -Force
    Copy-Item (Join-Path $out "$p.addin") $dist -Force
}
Write-Host "Listo. Paquetes en $root\dist" -ForegroundColor Green
