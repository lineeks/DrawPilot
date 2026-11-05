#define AppName "DrawPilot"

#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif

#ifndef StageDir
  #define StageDir "..\dist\installer-stage"
#endif

[Setup]
AppId={{7F0E5C0B-6C1E-4A57-9E0A-2D5B1F6A3C21}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=DrawPilot
DefaultDirName={userappdata}\DrawPilot
DisableDirPage=yes
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=..\dist
OutputBaseFilename=DrawPilot-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\app\DrawPilot.Host.exe
InfoBeforeFile=README.txt
LanguageDetectionMethod=uilanguage

[Languages]
Name: "chs"; MessagesFile: "languages\ChineseSimplified.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "{#StageDir}\app\*"; DestDir: "{app}\app"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#StageDir}\DrawPilot.gms"; DestDir: "{app}"; Flags: ignoreversion
Source: "README.txt"; DestDir: "{app}"; Flags: ignoreversion

[Registry]
Root: HKCU; Subkey: "Software\DrawPilot"; ValueType: string; ValueName: "Version"; ValueData: "{#AppVersion}"; Flags: uninsdeletekey

[UninstallDelete]
Type: filesandordirs; Name: "{app}\app"
Type: files; Name: "{app}\DrawPilot.gms"
Type: files; Name: "{app}\README.txt"

[Code]
var
  InstallTargets: TStringList;

function TargetName(const Entry: String): String;
var
  P: Integer;
begin
  P := Pos('|', Entry);
  if P > 0 then
    Result := Copy(Entry, 1, P - 1)
  else
    Result := Entry;
end;

function TargetRoot(const Entry: String): String;
var
  P: Integer;
begin
  P := Pos('|', Entry);
  if P > 0 then
    Result := Copy(Entry, P + 1, Length(Entry))
  else
    Result := '';
end;

function IsCorelProductName(const Name: String): Boolean;
var
  Tail: String;
begin
  Result := (Pos('CorelDRAW ', Name) = 1) and (Pos(' Suite ', Name) > 0);
  if not Result then
    Exit;

  if Length(Name) < 4 then
  begin
    Result := False;
    Exit;
  end;

  Tail := Copy(Name, Length(Name) - 3, 4);
  Result := StrToIntDef(Tail, 0) >= 2000;
end;

procedure AddTarget(const DisplayName: String);
var
  GmsRoot: String;
  I: Integer;
begin
  for I := 0 to InstallTargets.Count - 1 do
  begin
    if TargetName(InstallTargets.Strings[I]) = DisplayName then
      Exit;
  end;

  GmsRoot := ExpandConstant('{userappdata}') + '\Corel\' + DisplayName + '\Draw\GMS';
  if not DirExists(GmsRoot) then
  begin
    if not ForceDirectories(GmsRoot) then
      Exit;
  end;

  InstallTargets.Add(DisplayName + '|' + GmsRoot);
end;

procedure DetectTargets(const RootKey: Integer);
var
  Names: TArrayOfString;
  I: Integer;
  Key: String;
  DisplayName: String;
  InstallLocation: String;
begin
  Key := 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall';
  if not RegGetSubkeyNames(RootKey, Key, Names) then
    Exit;

  for I := 0 to GetArrayLength(Names) - 1 do
  begin
    if RegQueryStringValue(RootKey, Key + '\' + Names[I], 'DisplayName', DisplayName) then
    begin
      if IsCorelProductName(DisplayName) then
      begin
        if RegQueryStringValue(RootKey, Key + '\' + Names[I], 'InstallLocation', InstallLocation) then
        begin
          InstallLocation := RemoveBackslashUnlessRoot(InstallLocation);
          if InstallLocation <> '' then
          begin
            if FileExists(InstallLocation + '\Programs64\CorelDRW.exe') then
              AddTarget(DisplayName);
          end;
        end;
      end;
    end;
  end;
end;

function InitializeSetup(): Boolean;
begin
  InstallTargets := TStringList.Create;

  DetectTargets(HKLM64);
  DetectTargets(HKLM32);

  if InstallTargets.Count = 0 then
  begin
    MsgBox('未检测到已安装的 CorelDRAW 2022 或更高版本。' + #13#10 +
           '请先安装并至少启动一次 CorelDRAW，再运行本安装程序。',
           mbError, MB_OK);
    Result := False;
    Exit;
  end;

  Result := True;
end;

procedure DeinitializeSetup();
begin
  InstallTargets.Free;
end;

procedure CopyDirectory(const SourceDir, DestDir: String);
var
  FindRec: TFindRec;
  SourcePath: String;
  DestPath: String;
begin
  if not ForceDirectories(DestDir) then
    Exit;

  if FindFirst(SourceDir + '\*', FindRec) then
  begin
    try
      repeat
        if (FindRec.Name <> '.') and (FindRec.Name <> '..') then
        begin
          SourcePath := SourceDir + '\' + FindRec.Name;
          DestPath := DestDir + '\' + FindRec.Name;
          if (FindRec.Attributes and $10) <> 0 then
            CopyDirectory(SourcePath, DestPath)
          else
            CopyFile(SourcePath, DestPath, False);
        end;
      until not FindNext(FindRec);
    finally
      FindClose(FindRec);
    end;
  end;
end;

procedure DeployToTargets();
var
  I: Integer;
  Entry: String;
  GmsRoot: String;
  TargetApp: String;
begin
  for I := 0 to InstallTargets.Count - 1 do
  begin
    Entry := InstallTargets.Strings[I];
    GmsRoot := TargetRoot(Entry);
    TargetApp := GmsRoot + '\DrawPilot\app';

    WizardForm.StatusLabel.Caption := '正在部署到 ' + TargetName(Entry) + ' ...';

    DelTree(TargetApp, True, True, True);
    CopyDirectory(ExpandConstant('{app}\app'), TargetApp);
    CopyFile(ExpandConstant('{app}\DrawPilot.gms'), GmsRoot + '\DrawPilot.gms', False);

    if not FileExists(TargetApp + '\DrawPilot.Host.exe') then
      MsgBox('部署到以下目录失败：' + #13#10 + GmsRoot, mbError, MB_OK);
  end;

  RegWriteStringValue(HKCU, 'Software\DrawPilot', 'TargetCount', IntToStr(InstallTargets.Count));
  for I := 0 to InstallTargets.Count - 1 do
    RegWriteStringValue(HKCU, 'Software\DrawPilot', 'Target' + IntToStr(I),
      TargetRoot(InstallTargets.Strings[I]));
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
    DeployToTargets();
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  I: Integer;
  Count: Integer;
  CountText: String;
  GmsRoot: String;
begin
  if CurUninstallStep <> usUninstall then
    Exit;

  if not RegQueryStringValue(HKCU, 'Software\DrawPilot', 'TargetCount', CountText) then
    Exit;

  Count := StrToIntDef(CountText, 0);
  for I := 0 to Count - 1 do
  begin
    if RegQueryStringValue(HKCU, 'Software\DrawPilot', 'Target' + IntToStr(I), GmsRoot) then
    begin
      DelTree(GmsRoot + '\DrawPilot\app', True, True, True);
      DeleteFile(GmsRoot + '\DrawPilot.gms');
    end;
  end;
end;
