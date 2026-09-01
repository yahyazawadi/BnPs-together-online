; =====================================================================
; BnP Together ONLINE — Inno Setup Script
; Self-contained installer for Undertale: Bits & Pieces Together Online
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

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Standalone single-file published BnPRelay.exe and dependencies
Source: "C:\Users\CLICK\.gemini\antigravity-ide\scratch\BnPs-together-online\Publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; Embed fonts
Source: "C:\Users\CLICK\.gemini\antigravity-ide\scratch\BnPs-together-online\BnPRelay\UI\Assets\Fonts\*.ttf"; DestDir: "{autofonts}"; FontInstall: "Determination Mono Web"; Flags: ignoreversion onlyifdoesntexist uninsneveruninstall

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\UI\Assets\heart.ico"
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

[Code]
// Custom friendly Undertale-style error handling
function InitializeSetup(): Boolean;
var
  ErrorCode: Integer;
begin
  Result := True;
  
  // Close any running instance of BnPRelay automatically before installing
  ShellExec('open', 'taskkill.exe', '/F /IM BnPRelay.exe', '', SW_HIDE, ewWaitUntilTerminated, ErrorCode);
end;
