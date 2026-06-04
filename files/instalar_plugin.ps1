# BIM FIRE HIDRO CALC — Script de instalação interno
# Executado automaticamente pelo instalador .exe

param([string]$InstallDir = $PSScriptRoot)

$ErrorActionPreference = "Stop"
$addinsDir = "$env:APPDATA\Autodesk\Revit\Addins\2027"

function Log($msg) {
    $timestamp = Get-Date -Format "HH:mm:ss"
    Write-Output "[$timestamp] $msg"
}

# ── 1. WebView2 ──────────────────────────────────────────────
Log "Verificando WebView2 Runtime..."
$wv2 = Get-ItemProperty "HKLM:\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}" -ErrorAction SilentlyContinue
if (-not $wv2) {
    $wv2 = Get-ItemProperty "HKCU:\Software\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}" -ErrorAction SilentlyContinue
}
if (-not $wv2) {
    Log "WebView2 nao encontrado. Baixando..."
    $wv2Dest = "$env:TEMP\webview2setup.exe"
    Invoke-WebRequest "https://go.microsoft.com/fwlink/p/?LinkId=2124703" -OutFile $wv2Dest -UseBasicParsing
    Start-Process $wv2Dest -ArgumentList "/silent /install" -Wait
    Log "WebView2 instalado."
} else {
    Log "WebView2 ja instalado. OK"
}

# ── 2. .NET 10 SDK ───────────────────────────────────────────
Log "Verificando .NET 10 SDK..."
$dotnetOk = $false
try {
    $sdks = & dotnet --list-sdks 2>$null
    if ($sdks -match "^10\.") { $dotnetOk = $true }
} catch {}

if (-not $dotnetOk) {
    Log ".NET 10 SDK nao encontrado. Baixando..."
    $netDest = "$env:TEMP\dotnet-sdk-10-win-x64.exe"
    Invoke-WebRequest "https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.100/dotnet-sdk-10.0.100-win-x64.exe" -OutFile $netDest -UseBasicParsing
    Start-Process $netDest -ArgumentList "/quiet /norestart" -Wait
    $env:PATH += ";$env:ProgramFiles\dotnet"
    Log ".NET 10 SDK instalado."
} else {
    Log ".NET 10 SDK ja instalado. OK"
}

# ── 3. Compilar plugin ───────────────────────────────────────
Log "Compilando plugin..."
$srcDir   = Join-Path $InstallDir "src"
$buildDir = Join-Path $InstallDir "build"
$revitDll = "C:\Program Files\Autodesk\Revit 2027\RevitAPI.dll"

if ((Test-Path "$srcDir\BimFireHidroCalc.csproj") -and (Test-Path $revitDll)) {
    & dotnet build "$srcDir\BimFireHidroCalc.csproj" -c Release -o $buildDir --nologo -v quiet
    if ($LASTEXITCODE -ne 0) { throw "Falha na compilacao do plugin." }
    Log "Plugin compilado com sucesso."
} else {
    Log "Revit 2027 nao encontrado — usando DLL pre-compilada."
}

# ── 4. Copiar para pasta do Revit ────────────────────────────
Log "Instalando na pasta do Revit 2027..."
if (-not (Test-Path $addinsDir)) { New-Item -ItemType Directory -Path $addinsDir -Force | Out-Null }

$dlls = @("BimFireHidroCalc.dll","Microsoft.Web.WebView2.Core.dll",
          "Microsoft.Web.WebView2.Wpf.dll","WebView2Loader.dll","Newtonsoft.Json.dll")

foreach ($dll in $dlls) {
    $origem = Join-Path $buildDir $dll
    if (Test-Path $origem) {
        Copy-Item $origem "$addinsDir\$dll" -Force
        Log "Copiado: $dll"
    }
}

# ── 5. Criar .addin ──────────────────────────────────────────
$addinContent = @"
<?xml version="1.0" encoding="utf-8"?>
<RevitAddIns>
  <AddIn Type="Application">
    <Name>BIM FIRE HIDRO CALC</Name>
    <Assembly>$addinsDir\BimFireHidroCalc.dll</Assembly>
    <AddInId>A1B2C3D4-E5F6-7890-ABCD-EF1234567890</AddInId>
    <FullClassName>BimFireHidroCalc.App</FullClassName>
    <VendorId>BIMFIRE</VendorId>
    <VendorDescription>BIM FIRE HIDRO CALC</VendorDescription>
  </AddIn>
</RevitAddIns>
"@
$addinContent | Out-File "$addinsDir\BimFireHidroCalc.addin" -Encoding UTF8
Log "Arquivo .addin criado."
Log "INSTALACAO_CONCLUIDA"
