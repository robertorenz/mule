; Inno Setup script for M.U.L.E. Colony.
; Build with: "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer\mule.iss
; Expects a self-contained folder publish at ..\publish\app (see scripts\publish.ps1).

#define MyAppName "M.U.L.E. Colony"
#define MyAppVersion "1.0"
#define MyAppPublisher "Renz"
#define MyAppExeName "Mule.Game.exe"

[Setup]
AppId={{7C3F2A18-4E9B-4A2C-9F1D-4A5E9B0C1D2E}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\MULE Colony
DefaultGroupName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}
OutputDir=..\publish\installer
OutputBaseFilename=MULE-Colony-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
DisableProgramGroupPage=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "..\publish\app\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent
