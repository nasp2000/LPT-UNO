LPT-UNO Installer (Inno Setup)

This folder contains an Inno Setup script and a PowerShell helper script to build a Windows installer for the native `LPTUnoApp`.

Prerequisites
- .NET SDK (to run `dotnet publish`) — required to produce the files to package
- Inno Setup (ISCC.exe) — optional; required if you want to build the .exe installer

How to build
1. From repository root, publish the app and build the installer:

   powershell -ExecutionPolicy Bypass -File .\installer\build_installer.ps1

2. If Inno Setup is installed and on PATH, the script will run ISCC and produce the installer under `installer\output`.
3. If Inno Setup is not available, the script will still run `dotnet publish` and leave the published files in `LPTUnoApp\publish` for manual packaging.

Notes
- The Inno Setup script (`LPT-UNO-Installer.iss`) copies all files from `LPTUnoApp\publish` into the installed folder and optionally creates a startup shortcut (user-selectable during install).
- For reproducible builds, use the same `-r` (runtime) in the PowerShell script and on your CI.
