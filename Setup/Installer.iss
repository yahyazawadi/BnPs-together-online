; =====================================================================
; BnP Together ONLINE — Inno Setup Script
; Self-contained installer for Undertale: Bits & Pieces Together Online
; Includes Full Uninstaller & Windows Add/Remove Programs integration
; =====================================================================

#define MyAppName "BnP Together ONLINE"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Yahya Zawadi & BnP Together Online Team"
#define MyAppURL "https://github.com/yahyazawadi/BnPs-together-online"
#define MyAppExeName "BnPRelay.exe"

[Setup]
AppId={{C8E7F3B1-9D24-4B35-8912-3D7E951B40C2}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=C:\Users\CLICK\.gemini\antigravity-ide\scratch\BnPs-together-online\Output
OutputBaseFilename=BnP_Together_ONLINE_Setup
SetupIconFile=C:\Users\CLICK\.gemini\antigravity-ide\scratch\BnPs-together-online\BnPRelay\UI\Assets\heart.ico
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64
SetupMutex=BnPTogetherSetupMutex
CloseApplications=yes
RestartApplications=no
; Uninstaller configuration
UninstallDisplayIcon={app}\UI\Assets\heart.ico
UninstallDisplayName={#MyAppName}
CreateUninstallRegKey=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Standalone single-file published BnPRelay.exe and all dependencies
Source: "C:\Users\CLICK\.gemini\antigravity-ide\scratch\BnPs-together-online\Publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; Fonts are copied directly to app directory (WPF loads them via pack URI and registry without AddFontResource lock errors)
Source: "C:\Users\CLICK\.gemini\antigravity-ide\scratch\BnPs-together-online\BnPRelay\UI\Assets\Fonts\*.ttf"; DestDir: "{app}\UI\Assets\Fonts"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\UI\Assets\heart.ico"
Name: "{autoprograms}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"; IconFilename: "{app}\UI\Assets\heart.ico"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\UI\Assets\heart.ico"; Tasks: desktopicon

[Registry]
; Register bnptogether:// URL deep-link protocol
Root: HKCU; Subkey: "Software\Classes\bnptogether"; ValueType: string; ValueName: ""; ValueData: "URL:BnP Together ONLINE Protocol"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\bnptogether"; ValueType: string; ValueName: "URL Protocol"; ValueData: ""
Root: HKCU; Subkey: "Software\Classes\bnptogether\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"",0"
Root: HKCU; Subkey: "Software\Classes\bnptogether\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""

[Run]
; Auto-launch after installation
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Clean up app data and temp cache on uninstall for fresh restart
Type: filesandordirs; Name: "{localappdata}\BnPTogether"
Type: filesandordirs; Name: "{app}"

[Code]
function InitializeSetup(): Boolean;
var
  ErrorCode: Integer;
begin
  Result := True;
  ShellExec('open', 'taskkill.exe', '/F /IM BnPRelay.exe', '', SW_HIDE, ewWaitUntilTerminated, ErrorCode);
end;

function InitializeUninstall(): Boolean;
var
  ErrorCode: Integer;
begin
  Result := True;
  ShellExec('open', 'taskkill.exe', '/F /IM BnPRelay.exe', '', SW_HIDE, ewWaitUntilTerminated, ErrorCode);
end;
