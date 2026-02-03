# LPT-UNO Native App (WPF) - Prototype

This is a native Windows WPF prototype for the LPT-UNO project.

## Goals
- Replace the existing web interface with a native Windows executable
- Start minimized to tray and support Autostart
- Persist configuration in `%APPDATA%/LPT-UNO/config.json`
- Provide same features as web interface (AutoSave, AutoPrint, Data folder, serial comms)

## Requirements
- .NET SDK (net8.0 or compatible) installed to build
- `dotnet build` and `dotnet run` will build and start the application

## Build (developer)
1. Install .NET SDK: https://dotnet.microsoft.com/download
2. From repository root: `dotnet build ./LPTUnoApp`
3. To run: `dotnet run --project ./LPTUnoApp`

## Notes
- Tray icon placeholder: `LPTUnoApp/TrayIcon.ico` (replace with real icon)
- AutoPrint / Serial support to be implemented in next iterations (prototype has UI & config)
- This is an initial commit for the `LPT-UNO-Wifi` branch; all changes should be reviewed before merging to `main`.
