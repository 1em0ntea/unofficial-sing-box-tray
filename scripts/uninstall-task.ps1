param(
    [string]$TaskName = "sing-box",
    [string]$SingBoxDir,
    [switch]$KeepRunning
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

try { Stop-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue } catch {}
Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false -ErrorAction SilentlyContinue

if (-not $KeepRunning) {
    $processes = Get-CimInstance Win32_Process -Filter "name = 'SingBoxTray.exe' or name = 'sing-box.exe'"
    if ($SingBoxDir) {
        $SingBoxDir = (Resolve-Path -LiteralPath $SingBoxDir).Path
        $processes = $processes | Where-Object {
            $_.ExecutablePath -and
            $_.ExecutablePath.StartsWith($SingBoxDir, [StringComparison]::OrdinalIgnoreCase)
        }
    }

    $processes | ForEach-Object {
        Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue
    }
}

Write-Host "Uninstalled scheduled task '$TaskName'"
