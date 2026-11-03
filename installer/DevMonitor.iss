; Built by scripts/installer.ps1, which passes AppVersion and SourceDir.
#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\build\publish\installer"
#endif

[Setup]
AppId={{689C82D1-7CDA-448D-BC4E-C4B1F59DFF69}
AppName=Dev Monitor
AppVersion={#AppVersion}
AppVerName=Dev Monitor {#AppVersion}
AppPublisher=Dominik Ficek
DefaultDirName={autopf}\Dev Monitor
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\build\installer
OutputBaseFilename=DevMonitor-{#AppVersion}-setup
SetupIconFile=..\src\DevMonitor.App\Assets\Icon.ico
UninstallDisplayIcon={app}\DevMonitor.exe
UninstallDisplayName=Dev Monitor
WizardStyle=modern
Compression=lzma2/max
SolidCompression=yes
CloseApplications=force
RestartApplications=no

[Tasks]
Name: autostart; Description: "Start Dev Monitor when I sign in"
Name: pawnio; Description: "Install the PawnIO driver for CPU temperature sensors"; Check: not PawnIOInstalled
Name: desktopicon; Description: "Create a desktop shortcut"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Dev Monitor"; Filename: "{app}\DevMonitor.exe"
Name: "{autodesktop}\Dev Monitor"; Filename: "{app}\DevMonitor.exe"; Tasks: desktopicon

[Run]
Filename: "{cmd}"; Parameters: "/c winget install --id namazso.PawnIO --exact --silent --accept-package-agreements --accept-source-agreements"; StatusMsg: "Installing the PawnIO driver..."; Flags: runhidden waituntilterminated; Tasks: pawnio
Filename: "{app}\DevMonitor.exe"; Parameters: "--enable-autostart"; StatusMsg: "Registering the startup task..."; Flags: runhidden waituntilterminated; Tasks: autostart
Filename: "{app}\DevMonitor.exe"; Parameters: "--disable-autostart"; Flags: runhidden waituntilterminated; Tasks: not autostart
Filename: "{app}\DevMonitor.exe"; Description: "Start Dev Monitor"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{sys}\taskkill.exe"; Parameters: "/IM DevMonitor.exe /F"; Flags: runhidden; RunOnceId: "StopApp"
Filename: "{app}\DevMonitor.exe"; Parameters: "--disable-autostart"; Flags: runhidden waituntilterminated; RunOnceId: "RemoveStartupTask"

[Code]
function PawnIOInstalled: Boolean;
begin
  Result := RegKeyExists(HKLM, 'SYSTEM\CurrentControlSet\Services\PawnIO');
end;
