; The setup a tester double-clicks: installs per-user with no admin prompt, sets the screensaver,
; optionally fetches starter recitations, and adds a proper entry to Settings > Apps.
;
; Build it with installer\build-setup.ps1 rather than compiling this directly: that publishes the app
; first and passes in the version and the folder to package.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\publish\setup"
#endif

#define AppName "Islamic Screensaver"
#define ScrName "IslamicScreensaver.scr"

[Setup]
; Fixed for ever: it is how a newer build recognises and upgrades an older one in place.
AppId={{DDBBEB21-5B31-47A1-A35D-CCA304A71EE1}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=Abdulrahman AlGhofaily

; The same place install.ps1 uses, so the two never end up as separate copies. Per-user and fixed,
; so there is no folder page to get wrong and no administrator prompt.
DefaultDirName={localappdata}\Programs\IslamicScreensaver
DisableDirPage=yes
DisableProgramGroupPage=yes
PrivilegesRequired=lowest

ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0

OutputDir=Output
OutputBaseFilename=IslamicScreensaver-Setup-{#AppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ShowLanguageDialog=auto

UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\{#ScrName}

; A topmost fullscreen screensaver cannot be asked politely to close, so the code below stops it.
CloseApplications=no
SetupLogging=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "arabic"; MessagesFile: "compiler:Languages\Arabic.isl"

[CustomMessages]
english.TaskSetScreensaver=Use it as my screensaver
arabic.TaskSetScreensaver=استخدمه شاشةَ توقفٍ لي
english.TaskSeed=Download starter recitations now — Mishari al-ʿAfasy, Al-Fātiḥah and Juzʾ ʿAmma, about 55 MB
arabic.TaskSeed=نزّل تلاوات للبدء الآن — مشاري العفاسي، الفاتحة وجزء عمّ، نحو 55 ميجابايت
english.Seeding=Downloading starter recitations. This needs an internet connection and may take a minute...
arabic.Seeding=جارٍ تنزيل تلاوات البدء. يتطلب ذلك اتصالًا بالإنترنت وقد يستغرق دقيقة...
english.OpenSettings=Open the screensaver's settings
arabic.OpenSettings=افتح إعدادات شاشة التوقف
english.PurgeAsk=Also remove your settings and every downloaded recitation?%n%nChoose No to keep them, so they are still there if you install it again.
arabic.PurgeAsk=هل تريد أيضًا حذف إعداداتك وجميع التلاوات المنزَّلة؟%n%nاختر «لا» للإبقاء عليها، لتجدها إن ثبّتّه مرة أخرى.

[Tasks]
Name: "setscreensaver"; Description: "{cm:TaskSetScreensaver}"
Name: "seed"; Description: "{cm:TaskSeed}"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Registry]
; Not removed by the uninstaller automatically: the user may have chosen another screensaver since,
; so the code below takes it out only if it still points here.
Root: HKCU; Subkey: "Control Panel\Desktop"; ValueType: string; ValueName: "SCRNSAVE.EXE"; ValueData: "{app}\{#ScrName}"; Tasks: setscreensaver
Root: HKCU; Subkey: "Control Panel\Desktop"; ValueType: string; ValueName: "ScreenSaveActive"; ValueData: "1"; Tasks: setscreensaver

[Run]
; Launched directly, never through the shell: Windows applies a .scr file's default verb (/S, "run
; the screensaver") when it is opened through the shell, and the argument would be thrown away.
Filename: "{app}\{#ScrName}"; Parameters: "/seed"; StatusMsg: "{cm:Seeding}"; Flags: runhidden waituntilterminated; Tasks: seed
Filename: "{app}\{#ScrName}"; Parameters: "/c"; Description: "{cm:OpenSettings}"; Flags: postinstall nowait skipifsilent

[Code]
const
  SPI_SETSCREENSAVEACTIVE = $0011;
  SPIF_UPDATEINIFILE = $01;
  SPIF_SENDCHANGE = $02;
  DesktopKey = 'Control Panel\Desktop';

function SystemParametersInfo(uiAction, uiParam, pvParam, fWinIni: Cardinal): Boolean;
  external 'SystemParametersInfoW@user32.dll stdcall';

{ A running screensaver, or the preview inside Windows' own Screen Saver dialog, holds the files open. }
procedure StopRunningScreensaver;
var
  Code: Integer;
begin
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM {#ScrName}', '', SW_HIDE, ewWaitUntilTerminated, Code);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  StopRunningScreensaver;
  Result := '';
end;

{ Windows caches its screensaver settings; this makes it re-read the ones just written. }
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if (CurStep = ssPostInstall) and WizardIsTaskSelected('setscreensaver') then
    SystemParametersInfo(SPI_SETSCREENSAVEACTIVE, 1, 0, SPIF_UPDATEINIFILE or SPIF_SENDCHANGE);
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Current: String;
  Code: Integer;
begin
  if CurUninstallStep <> usUninstall then
    Exit;

  StopRunningScreensaver;

  { Asked while the app is still here, because only the app can read its settings and find a library
    moved to another drive. No is the default: deleting someone's downloads must be a choice. }
  if not UninstallSilent then
    if MsgBox(CustomMessage('PurgeAsk'), mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
      Exec(ExpandConstant('{app}\{#ScrName}'), '/purge', '', SW_HIDE, ewWaitUntilTerminated, Code);

  { Unregister only if it is still ours. }
  if RegQueryStringValue(HKCU, DesktopKey, 'SCRNSAVE.EXE', Current) then
    if CompareText(Current, ExpandConstant('{app}\{#ScrName}')) = 0 then
    begin
      RegDeleteValue(HKCU, DesktopKey, 'SCRNSAVE.EXE');
      SystemParametersInfo(SPI_SETSCREENSAVEACTIVE, 0, 0, SPIF_UPDATEINIFILE or SPIF_SENDCHANGE);
    end;
end;
