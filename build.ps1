# Prueba y compila TeklaPiezas en .\dist\TeklaPiezas
param([string]$TeklaVersion = "2025.0.0")
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot
dotnet test tests/TeklaPiezas.Tests/TeklaPiezas.Tests.csproj -c Release
dotnet build src/TeklaPiezas/TeklaPiezas.csproj -c Release -o dist/TeklaPiezas -p:TeklaVersion=$TeklaVersion
Write-Host "Listo: $PSScriptRoot\dist\TeklaPiezas\TeklaPiezas.exe"
