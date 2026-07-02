param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$Root = Split-Path -Parent $PSScriptRoot
$Src = Join-Path $Root "src\SingBoxTray.cs"
$Assets = Join-Path $Root "assets"
$Dist = Join-Path $Root "dist"
$OutExe = Join-Path $Dist "SingBoxTray.exe"
$Icon = Join-Path $Assets "sing-box.ico"

New-Item -ItemType Directory -Force -Path $Dist | Out-Null

$CscCandidates = @(
    "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe",
    "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe"
)

$Csc = $CscCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $Csc) {
    throw "Could not find the .NET Framework C# compiler. Install .NET Framework 4.x developer tools or Visual Studio Build Tools."
}

& $Csc `
    /nologo `
    /target:winexe `
    /platform:anycpu `
    /optimize+ `
    /reference:System.Windows.Forms.dll `
    /reference:System.Drawing.dll `
    /win32icon:"$Icon" `
    /out:"$OutExe" `
    "$Src"

Copy-Item -LiteralPath $Icon -Destination (Join-Path $Dist "sing-box.ico") -Force

Write-Host "Built $OutExe"
