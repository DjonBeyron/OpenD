; Установщик OpenD. Ставится для текущего пользователя (права администратора не нужны).
; Компоненты (yt-dlp, ffmpeg, deno) уже внутри — интернет при первом запуске не нужен.
; Собирается в CI: .github/workflows/release.yml
#ifndef AppVer
  #define AppVer "0.0.0"
#endif

[Setup]
AppId={{6B1E7C4A-3D52-4F0B-9A1E-0E9D00000001}
AppName=OpenD
AppVersion={#AppVer}
AppPublisher=DjonBeyron
DefaultDirName={localappdata}\Programs\OpenD
DefaultGroupName=OpenD
PrivilegesRequired=lowest
OutputDir=..\dist
OutputBaseFilename=OpenD-Setup
Compression=lzma2/fast
SolidCompression=yes
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\OpenD.exe
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern
CloseApplications=force

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "autostart"; Description: "Запускать вместе с Windows"; Flags: checkedonce
Name: "desktopicon"; Description: "Ярлык на рабочем столе"; Flags: unchecked

[Files]
Source: "..\dist\OpenD.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\dist\*.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\stage\bin\*"; DestDir: "{userappdata}\OpenD\bin"; Flags: ignoreversion
Source: "..\stage\MicrosoftEdgeWebview2Setup.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall; Check: NeedWebView2

[Icons]
Name: "{group}\OpenD"; Filename: "{app}\OpenD.exe"
Name: "{userdesktop}\OpenD"; Filename: "{app}\OpenD.exe"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "OpenD"; ValueData: """{app}\OpenD.exe"" --autostart"; Tasks: autostart; Flags: uninsdeletevalue

[Run]
Filename: "{tmp}\MicrosoftEdgeWebview2Setup.exe"; Parameters: "/silent /install"; StatusMsg: "Установка Microsoft Edge WebView2 (нужен для входа в YouTube)…"; Check: NeedWebView2; Flags: waituntilterminated
Filename: "{app}\OpenD.exe"; Description: "Запустить OpenD и войти в YouTube"; Flags: nowait postinstall skipifsilent

[Code]
function WebView2Installed(): Boolean;
var v: String;
begin
  Result := RegQueryStringValue(HKLM, 'SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', v)
         or RegQueryStringValue(HKCU, 'Software\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', v)
         or RegQueryStringValue(HKLM, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', v);
  if Result then Result := (v <> '') and (v <> '0.0.0.0');
end;
function NeedWebView2(): Boolean;
begin
  Result := not WebView2Installed();
end;
