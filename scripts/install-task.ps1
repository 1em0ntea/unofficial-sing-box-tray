param(
    [Parameter(Mandatory = $true)]
    [string]$SingBoxDir,

    [string]$TaskName = "sing-box",
    [string]$ConfigPath,
    [switch]$NoStart
)

$ErrorActionPreference = "Stop"

function Test-Administrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]$identity
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

if (-not (Test-Administrator)) {
    throw "Run this script from an elevated PowerShell window."
}

$Root = Split-Path -Parent $PSScriptRoot
$Dist = Join-Path $Root "dist"
$TraySource = Join-Path $Dist "SingBoxTray.exe"
$IconSource = Join-Path $Dist "sing-box.ico"

if (-not (Test-Path -LiteralPath $TraySource -PathType Leaf)) {
    throw "Missing $TraySource. Run .\scripts\build.ps1 first."
}

if (-not (Test-Path -LiteralPath $SingBoxDir -PathType Container)) {
    throw "sing-box directory not found: $SingBoxDir"
}

$SingBoxDir = (Resolve-Path -LiteralPath $SingBoxDir).Path
$SingBoxExe = Join-Path $SingBoxDir "sing-box.exe"
if (-not (Test-Path -LiteralPath $SingBoxExe -PathType Leaf)) {
    throw "sing-box.exe not found: $SingBoxExe"
}

if (-not $ConfigPath) {
    $ConfigPath = Join-Path $SingBoxDir "config.json"
}

if (-not (Test-Path -LiteralPath $ConfigPath -PathType Leaf)) {
    throw "config file not found: $ConfigPath"
}

$TrayTarget = Join-Path $SingBoxDir "SingBoxTray.exe"
$IconTarget = Join-Path $SingBoxDir "sing-box.ico"
Copy-Item -LiteralPath $TraySource -Destination $TrayTarget -Force
Copy-Item -LiteralPath $IconSource -Destination $IconTarget -Force
Unblock-File -LiteralPath $TrayTarget -ErrorAction SilentlyContinue

try { Stop-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue } catch {}

Get-CimInstance Win32_Process -Filter "name = 'SingBoxTray.exe' or name = 'sing-box.exe'" |
    Where-Object {
        $_.ExecutablePath -and
        $_.ExecutablePath.StartsWith($SingBoxDir, [StringComparison]::OrdinalIgnoreCase)
    } |
    ForEach-Object {
        Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue
    }

$Arguments = @(
    "--workdir", "`"$SingBoxDir`"",
    "--sing-box", "`"$SingBoxExe`"",
    "--config", "`"$ConfigPath`"",
    "--icon", "`"$IconTarget`""
) -join " "

$Action = New-ScheduledTaskAction -Execute $TrayTarget -Argument $Arguments -WorkingDirectory $SingBoxDir
$Trigger = New-ScheduledTaskTrigger -AtLogOn
$Principal = New-ScheduledTaskPrincipal -UserId "$env:USERDOMAIN\$env:USERNAME" -LogonType Interactive -RunLevel Highest
$Settings = New-ScheduledTaskSettingsSet `
    -StartWhenAvailable `
    -RestartCount 3 `
    -RestartInterval (New-TimeSpan -Minutes 1) `
    -ExecutionTimeLimit (New-TimeSpan -Seconds 0)

Register-ScheduledTask `
    -TaskName $TaskName `
    -Action $Action `
    -Trigger $Trigger `
    -Principal $Principal `
    -Settings $Settings `
    -Description "Start unofficial sing-box tray manager at user logon" `
    -Force | Out-Null

if (-not $NoStart) {
    Start-ScheduledTask -TaskName $TaskName
}

Write-Host "Installed scheduled task '$TaskName' for $TrayTarget"
