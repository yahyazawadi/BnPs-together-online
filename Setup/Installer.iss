; =====================================================================
; BnP Together ONLINE — Inno Setup Script
; True Single-Executable Installer with Full Auto-Cleanup
; =====================================================================

#define MyAppName "BnP Together ONLINE"
#define MyAppVersion "1.2.16"
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
DisableDirPage=auto
DirExistsWarning=no
PrivilegesRequired=lowest
OutputDir=C:\Users\CLICK\.gemini\antigravity-ide\scratch\BnPs-together-online\Output
OutputBaseFilename=BnP_Together_ONLINE_Setup
SetupIconFile=C:\Users\CLICK\.gemini\antigravity-ide\scratch\BnPs-together-online\BnPRelay\UI\Assets\heart.ico
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64
CloseApplications=force
RestartApplications=no
UninstallDisplayIcon={app}\UI\Assets\heart.ico
UninstallDisplayName={#MyAppName}
CreateUninstallRegKey=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Application single-file executable
Source: "C:\Users\CLICK\.gemini\antigravity-ide\scratch\BnPs-together-online\Publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs restartreplace
; Heart icon
Source: "C:\Users\CLICK\.gemini\antigravity-ide\scratch\BnPs-together-online\BnPRelay\UI\Assets\heart.ico"; DestDir: "{app}"; Flags: ignoreversion
; Fonts
Source: "C:\Users\CLICK\.gemini\antigravity-ide\scratch\BnPs-together-online\BnPRelay\UI\Assets\Fonts\*.ttf"; DestDir: "{app}\UI\Assets\Fonts"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\heart.ico"
Name: "{autoprograms}\{#MyAppName}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"; IconFilename: "{app}\heart.ico"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\heart.ico"; Tasks: desktopicon

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
  IsUninstallTriggered: Boolean;
  DeleteCheckboxIndex: Integer;
  HasAddedDeleteCheckbox: Boolean;
  InstallSuccessful: Boolean;

// Thorough kill of any process holding the relay file, DLLs, or old setup instances
procedure ForceKillAllProcesses();
var
  ErrorCode: Integer;
begin
  Exec('taskkill.exe', '/F /T /IM BnPRelay.exe', '', SW_HIDE, ewWaitUntilTerminated, ErrorCode);
  Exec('taskkill.exe', '/F /T /IM UNDERTALE.exe', '', SW_HIDE, ewWaitUntilTerminated, ErrorCode);
  Exec('taskkill.exe', '/F /T /IM UNDERTALEBNP.exe', '', SW_HIDE, ewWaitUntilTerminated, ErrorCode);
  Exec('powershell.exe', '-NoProfile -Command "Get-Process -Name BnPRelay,UNDERTALE,UNDERTALEBNP -ErrorAction SilentlyContinue | Stop-Process -Force"', '', SW_HIDE, ewWaitUntilTerminated, ErrorCode);
  Exec('cmd.exe', '/c ping 127.0.0.1 -n 2 > nul', '', SW_HIDE, ewWaitUntilTerminated, ErrorCode);
end;

// Delete the installer executable itself after completion
procedure DeleteSelfInstaller();
var
  CurrentExe: String;
  ErrorCode: Integer;
begin
  CurrentExe := ExpandConstant('{srcexe}');
  if FileExists(CurrentExe) then
  begin
    Exec('cmd.exe', '/c ping 127.0.0.1 -n 2 > nul & del /f /q "' + CurrentExe + '"', '', SW_HIDE, ewNoWait, ErrorCode);
  end;
end;

// If any file or old DLL is locked, rename it away so the install folder is completely cleared
procedure SafePurgeDirectory(TargetDir: String);
var
  FindRec: TFindRec;
  FilePath: String;
  OldPath: String;
begin
  if DirExists(TargetDir) then
  begin
    if FindFirst(TargetDir + '\*.*', FindRec) then
    begin
      try
        repeat
          if (FindRec.Name <> '.') and (FindRec.Name <> '..') then
          begin
            FilePath := TargetDir + '\' + FindRec.Name;
            if not (FindRec.Attributes and FILE_ATTRIBUTE_DIRECTORY <> 0) then
            begin
              if not DeleteFile(FilePath) then
              begin
                OldPath := FilePath + '.old_' + GetDateTimeString('yyyymmddhhnnss', #0, #0);
                RenameFile(FilePath, OldPath);
                DeleteFile(OldPath);
              end;
            end;
          end;
        until not FindNext(FindRec);
      finally
        FindClose(FindRec);
      end;
    end;
  end;
end;

// Searches Downloads folder, Telegram Desktop folder, and Desktop for older setup duplicates and removes them
procedure CleanFolderOfOldSetups(Folder: String; CurrentExeName: String);
var
  FindRec: TFindRec;
begin
  if (Folder <> '') and DirExists(Folder) then
  begin
    if FindFirst(Folder + '\*BnP*Together*ONLINE*Setup*.exe', FindRec) then
    begin
      try
        repeat
          if (FindRec.Name <> CurrentExeName) then
            DeleteFile(Folder + '\' + FindRec.Name);
        until not FindNext(FindRec);
      finally
        FindClose(FindRec);
      end;
    end;
  end;
end;

procedure CleanLegacyInstallerFiles();
var
  CurrentExeName: String;
begin
  CurrentExeName := ExtractFileName(ExpandConstant('{srcexe}'));

  // 1. Standard Downloads folder
  CleanFolderOfOldSetups(ExpandConstant('{userdocs}\..\Downloads'), CurrentExeName);
  // 2. Telegram Desktop downloads folder
  CleanFolderOfOldSetups(ExpandConstant('{userdocs}\..\Downloads\Telegram Desktop'), CurrentExeName);
  // 3. Desktop folder
  CleanFolderOfOldSetups(ExpandConstant('{autodesktop}'), CurrentExeName);
  // 4. Current installer directory
  CleanFolderOfOldSetups(ExtractFileDir(ExpandConstant('{srcexe}')), CurrentExeName);
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
    ForceKillAllProcesses();

    if (InstalledAppDir <> '') and DirExists(InstalledAppDir) then
    begin
      SafePurgeDirectory(InstalledAppDir);
      DelTree(InstalledAppDir, True, True, True);
    end;

    if DirExists(ExpandConstant('{localappdata}\BnPTogether')) then
      DelTree(ExpandConstant('{localappdata}\BnPTogether'), True, True, True);

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
    IsUninstallTriggered := True;
    WizardForm.Close;
  end;
end;

procedure CancelButtonClick(CurPageID: Integer; var Cancel, Confirm: Boolean);
begin
  if IsUninstallTriggered then
  begin
    Confirm := False;
    Cancel := True;
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
  DeleteCheckboxIndex := -1;
  HasAddedDeleteCheckbox := False;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = wpFinished then
  begin
    if not HasAddedDeleteCheckbox and (WizardForm.RunList <> nil) then
    begin
      DeleteCheckboxIndex := WizardForm.RunList.AddCheckBox('Delete installer after installation', '', 0, True, True, False, False, nil);
      HasAddedDeleteCheckbox := True;
    end;
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  TargetAppDir: String;
begin
  ForceKillAllProcesses();
  TargetAppDir := ExpandConstant('{app}');
  SafePurgeDirectory(TargetAppDir);
  Result := '';
end;

function InitializeSetup(): Boolean;
begin
  Result := True;
  ForceKillAllProcesses();
end;

function InitializeUninstall(): Boolean;
begin
  Result := True;
  ForceKillAllProcesses();
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssDone then
    InstallSuccessful := True;
end;

procedure DeinitializeSetup();
begin
  if InstallSuccessful and HasAddedDeleteCheckbox and (WizardForm.RunList <> nil) and (DeleteCheckboxIndex >= 0) and (DeleteCheckboxIndex < WizardForm.RunList.Items.Count) then
  begin
    if WizardForm.RunList.Checked[DeleteCheckboxIndex] then
    begin
      DeleteSelfInstaller();
    end;
  end;
end;
