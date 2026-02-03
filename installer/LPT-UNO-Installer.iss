; Inno Setup script for LPT-UNO Installer
#define MyAppName "LPT-UNO"
#define MyAppVersion "0.1"
#define MyAppPublisher "LPT-UNO Project"

[Setup]
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={pf}\{#MyAppName}
DefaultGroupName={#MyAppName}
OutputDir=output
OutputBaseFilename={#MyAppName}_Setup_{#MyAppVersion}
Compression=lzma
SolidCompression=yes

[Files]
; Include published app files (produced by dotnet publish -r win-x64 --self-contained)
Source: "..\LPTUnoApp\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\LPTUnoApp.exe"
Name: "{userstartup}\{#MyAppName}"; Filename: "{app}\LPTUnoApp.exe"; Tasks: startup

[Tasks]
Name: "startup"; Description: "Create a shortcut in the Startup folder"; GroupDescription: "Additional tasks:"; Flags: unchecked

[Run]
Filename: "{app}\LPTUnoApp.exe"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent
