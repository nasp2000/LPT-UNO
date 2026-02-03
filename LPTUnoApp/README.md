# LPT-UNO Native App (WPF) - Prototype

This is a native Windows WPF prototype for the LPT-UNO project.

## Goals
- Replace the existing web interface with a native Windows executable
- Start minimized to tray and support Autostart
- Persist configuration in `%APPDATA%/LPT-UNO/config.json`
- Provide same features as web interface (AutoSave, AutoPrint, Data folder, serial comms)

## Serial (Arduino)
- The app detects available COM ports. Use the dropdown and 'Conectar' to connect to the emulator (115200 baud).
- Incoming serial data is logged to the UI. When *AutoPrint* is ON, the app will save incoming data to `%APPDATA%/LPT-UNO/DATA` as timestamped files to be processed later for printing.

## AutoPrint & Printing
- The app monitors `%APPDATA%/LPT-UNO/DATA` for new `*.txt` files and automatically sends them to the configured printer when AutoPrint is enabled.
- Printed files are moved to `%APPDATA%/LPT-UNO/DATA/printed` and on failure to `%APPDATA%/LPT-UNO/DATA/error`.
- Use the `Imprimir Já` button to schedule the latest file for printing manually.

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
