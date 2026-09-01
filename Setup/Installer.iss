; =====================================================================
; BnP Together ONLINE — Inno Setup Script
; With Clean In-Place Reset and Process Termination
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
UninstallDisplayIcon={app}\UI\Assets\heart.ico
UninstallDisplayName={#MyAppName}
CreateUninstallRegKey=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Standalone published files
Source: "C:\Users\CLICK\.gemini\antigravity-ide\scratch\BnPs-together-online\Publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; Fonts
Source: "C:\Users\CLICK\.gemini\antigravity-ide\scratch\BnPs-together-online\BnPRelay\UI\Assets\Fonts\*.ttf"; DestDir: "{app}\UI\Assets\Fonts"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\UI\Assets\heart.ico"
Name: "{autoprograms}\{#MyAppName}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"; IconFilename: "{app}\UI\Assets\heart.ico"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\UI\Assets\heart.ico"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Classes\bnptogether"; ValueType: string; ValueName: ""; ValueData: "URL:BnP Together ONLINE Protocol"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\bnptogether"; ValueType: string; ValueName: "URL Protocol"; ValueData: ""
Root: HKCU; Subkey: "Software\Classes\bnptogether\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"",0"
Root: HKCU; Subkey: "Software\Classes\bnptogether\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall shellexec

[UninstallDelete]
Type: filesandordirs; Name: "{localappdata}\BnPTogether"

[Code]
var
  UninstallButton: TNewButton;
  IsAlreadyInstalled: Boolean;
  InstalledAppDir: String;

// Stop any running instance of BnPRelay
procedure StopRunningProcesses();
var
  ErrorCode: Integer;
begin
  Exec('taskkill.exe', '/F /IM BnPRelay.exe', '', SW_HIDE, ewWaitUntilTerminated, ErrorCode);
  Exec('cmd.exe', '/c ping 127.0.0.1 -n 2 > nul', '', SW_HIDE, ewWaitUntilTerminated, ErrorCode);
end;

// Detect existing installation directory from registry
function GetExistingInstallDir(): String;
var
  RegKey: String;
  ResultStr: String;
begin
  RegKey := 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{C8E7F3B1-9D24-4B35-8912-3D7E951B40C2}_is1';
  if RegQueryStringValue(HKCU, RegKey, 'InstallLocation', ResultStr) then
    Result := ResultStr
  else if RegQueryStringValue(HKLM, RegKey, 'InstallLocation', ResultStr) then
    Result := ResultStr
  else
    Result := '';
end;

procedure OnUninstallClick(Sender: TObject);
var
  DesktopShortcut: String;
  StartMenuFolder: String;
begin
  if MsgBox('Are you sure you want to completely remove BnP Together ONLINE and reset all caches?', mbConfirmation, MB_YESNO) = IDYES then
  begin
    StopRunningProcesses();

    // 1. Delete installed application directory
    if (InstalledAppDir <> '') and DirExists(InstalledAppDir) then
      DelTree(InstalledAppDir, True, True, True);

    // 2. Delete LocalAppData logs/cache
    if DirExists(ExpandConstant('{localappdata}\BnPTogether')) then
      DelTree(ExpandConstant('{localappdata}\BnPTogether'), True, True, True);

    // 3. Delete Desktop shortcut
    DesktopShortcut := ExpandConstant('{autodesktop}\{#MyAppName}.lnk');
    if FileExists(DesktopShortcut) then
      DeleteFile(DesktopShortcut);

    // 4. Delete Start Menu folder
    StartMenuFolder := ExpandConstant('{autoprograms}\{#MyAppName}');
    if DirExists(StartMenuFolder) then
      DelTree(StartMenuFolder, True, True, True);

    // 5. Clean registry uninstall keys & protocol
    RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{C8E7F3B1-9D24-4B35-8912-3D7E951B40C2}_is1');
    RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Classes\bnptogether');

    MsgBox('BnP Together ONLINE has been completely removed and reset.', mbInformation, MB_OK);
    WizardForm.Close;
  end;
end;

procedure InitializeWizard();
begin
  InstalledAppDir := GetExistingInstallDir();
  IsAlreadyInstalled := (InstalledAppDir <> '') and DirExists(InstalledAppDir);

  if IsAlreadyInstalled then
  begin
    UninstallButton := TNewButton.Create(WizardForm);
    UninstallButton.Parent := WizardForm;
    UninstallButton.Caption := 'Uninstall / Fresh Reset';
    UninstallButton.Width := ScaleX(145);
    UninstallButton.Height := WizardForm.CancelButton.Height;
    UninstallButton.Left := WizardForm.ClientWidth - WizardForm.CancelButton.Width - ScaleX(155);
    UninstallButton.Top := WizardForm.CancelButton.Top;
    UninstallButton.OnClick := @OnUninstallClick;
  end;
end;

function InitializeSetup(): Boolean;
begin
  Result := True;
  StopRunningProcesses();
end;

function InitializeUninstall(): Boolean;
begin
  Result := True;
  StopRunningProcesses();
end;
