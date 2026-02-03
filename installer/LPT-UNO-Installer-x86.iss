; Inno Setup script for LPT-UNO Installer (x86)
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
OutputBaseFilename={#MyAppName}_Setup_{#MyAppVersion}_x86
Compression=lzma
SolidCompression=yes

[Files]
; Include published app files for x86 runtime
Source: "..\LPTUnoApp\publish-x86\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\LPTUnoApp.exe"
Name: "{userstartup}\{#MyAppName}"; Filename: "{app}\LPTUnoApp.exe"; Tasks: startup

[Tasks]
Name: "startup"; Description: "Create a shortcut in the Startup folder"; GroupDescription: "Additional tasks:"; Flags: unchecked

[Run]
Filename: "{app}\LPTUnoApp.exe"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent
