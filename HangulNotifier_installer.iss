; HangulNotifier — Inno Setup 인스톨러 스크립트
; 한글 맞춤법 실시간 알림기 (개인 앱, H.Soft)
; 관리자 권한 불필요(per-user 설치). .NET 8 Desktop Runtime 확인 포함.

#define MyAppName "HangulNotifier"
#define MyAppNameKo "한글 맞춤법 알림기"
#define MyAppVersion "0.2.1"
#define MyAppPublisher "H.Soft"
#define MyAppURL "https://github.com/BaeTab/hangeul_detect"
#define MyAppExeName "HangulNotifier.exe"
#define PublishDir "src\HangulNotifier.App\bin\Release\publish"

[Setup]
AppId={{7A3C2E10-8B4D-4E9A-9C1F-2B5D9E4F1A07}}
AppName={#MyAppNameKo}
AppVersion={#MyAppVersion}
AppVerName={#MyAppNameKo} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
VersionInfoVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppNameKo} 설치 관리자
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppNameKo}
DisableProgramGroupPage=yes
; 관리자 권한 불필요 — per-user 설치
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible
OutputDir=dist
OutputBaseFilename=HangulNotifier-Setup-{#MyAppVersion}
SetupIconFile=src\HangulNotifier.App\Assets\app.ico
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppNameKo}

[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startupicon"; Description: "Windows 시작 시 자동 실행"; GroupDescription: "시작 옵션:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PublishDir}\e_sqlite3.dll"; DestDir: "{app}"; Flags: ignoreversion
; README도 함께 배포
Source: "README.md"; DestDir: "{app}"; Flags: ignoreversion isreadme

[Icons]
Name: "{group}\{#MyAppNameKo}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppNameKo}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppNameKo}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
Name: "{userstartup}\{#MyAppNameKo}"; Filename: "{app}\{#MyAppExeName}"; Tasks: startupicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppNameKo}}"; Flags: nowait postinstall skipifsilent

[Code]
// .NET 8 Desktop Runtime 설치 여부 확인
function IsDotNet8DesktopInstalled(): Boolean;
var
  FindRec: TFindRec;
  BasePath: String;
begin
  Result := False;
  BasePath := ExpandConstant('{commonpf64}\dotnet\shared\Microsoft.WindowsDesktop.App');
  if FindFirst(BasePath + '\8.*', FindRec) then
  begin
    try
      repeat
        if (FindRec.Attributes and FILE_ATTRIBUTE_DIRECTORY) <> 0 then
          Result := True;
      until not FindNext(FindRec);
    finally
      FindClose(FindRec);
    end;
  end;
end;

function InitializeSetup(): Boolean;
var
  ErrorCode: Integer;
begin
  Result := True;
  if not IsDotNet8DesktopInstalled() then
  begin
    if MsgBox('이 프로그램은 .NET 8 Desktop Runtime(x64)이 필요합니다.' + #13#10 +
              '설치되어 있지 않은 것 같습니다. 지금 다운로드 페이지를 여시겠습니까?' + #13#10 +
              '(설치 후 이 인스톨러를 다시 실행해 주세요.)',
              mbConfirmation, MB_YESNO) = IDYES then
    begin
      ShellExec('open',
        'https://dotnet.microsoft.com/ko-kr/download/dotnet/8.0/runtime?cid=getdotnetcore',
        '', '', SW_SHOW, ewNoWait, ErrorCode);
      Result := False;  // 런타임 설치 후 재실행하도록 종료
    end;
  end;
end;
