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
DisableWelcomePage=yes
DisableDirPage=yes
DisableProgramGroupPage=yes
DisableReadyPage=yes
DisableFinishedPage=yes
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
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\heart.ico"

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

procedure CurStepChanged(CurStep: TSetupStep);
var
  ErrorCode: Integer;
  AppExe: String;
begin
  if CurStep = ssPostInstall then
  begin
    AppExe := ExpandConstant('{app}\{#MyAppExeName}');
    if FileExists(AppExe) then
    begin
      Exec(AppExe, '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
    end;
  end;
  if CurStep = ssDone then
    InstallSuccessful := True;
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

procedure DeinitializeSetup();
begin
  if InstallSuccessful then
  begin
    CleanLegacyInstallerFiles();
    DeleteSelfInstaller();
  end;
end;
