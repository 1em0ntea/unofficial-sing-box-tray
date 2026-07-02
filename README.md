# Unofficial sing-box Tray

An unofficial Windows tray helper for running [`sing-box`](https://github.com/SagerNet/sing-box) without a visible console window.

It starts `sing-box.exe run -c config.json` in the background, writes output to a log file, and exposes a tray menu for Start, Stop, Restart, Open log, Open folder, Edit config, and Exit.

This project is not affiliated with or endorsed by the sing-box project.

## Features

- No console window on startup
- Tray status icon
- Start, Stop, and Restart from the tray menu
- Logs stdout/stderr to `logs/sing-box.log`
- Optional Windows Scheduled Task installer for auto-start at logon
- Single-file C# WinForms app, built with the .NET Framework compiler included on many Windows systems

## Quick Start

Download `SingBoxTray-windows.zip` from the latest GitHub Release, or build locally:

```powershell
.\scripts\build.ps1
```

Copy `dist\SingBoxTray.exe` and `dist\sing-box.ico` into the same folder as your `sing-box.exe` and `config.json`, then run:

```powershell
.\SingBoxTray.exe
```

## Auto-start at Logon

Run PowerShell as Administrator:

```powershell
.\scripts\build.ps1
.\scripts\install-task.ps1 -SingBoxDir "C:\Tools\sing-box"
```

The installer copies `SingBoxTray.exe` and `sing-box.ico` into your sing-box directory and creates a Scheduled Task named `sing-box`.

To uninstall:

```powershell
.\scripts\uninstall-task.ps1 -TaskName "sing-box" -SingBoxDir "C:\Tools\sing-box"
```

## Command Line Options

```text
SingBoxTray.exe [--workdir <dir>] [--sing-box <path>] [--config <path>] [--log <path>] [--icon <path>]
```

Defaults:

- `--workdir`: the folder containing `SingBoxTray.exe`
- `--sing-box`: `<workdir>\sing-box.exe`
- `--config`: `<workdir>\config.json`
- `--log`: `<workdir>\logs\sing-box.log`
- `--icon`: `<workdir>\sing-box.ico`

## License

GPL-3.0-or-later. See [LICENSE](LICENSE) and [NOTICE.md](NOTICE.md).
