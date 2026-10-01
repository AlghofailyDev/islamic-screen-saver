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

; Welcome, then the licence, so nobody lands cold on a list of checkboxes. The licence is shown to be
; read, not accepted: the GPL itself (section 9) says no acceptance is needed to receive or run it.
DisableWelcomePage=no
InfoBeforeFile=..\LICENSE

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

[Messages]
english.WelcomeLabel2=This will install [name/ver] on your computer.%n%nIt is free software: you may use it, share it and change it under the GNU General Public License, which you can read on the next page.%n%nThis is a test build — please tell us about anything that does not work as you expect.
arabic.WelcomeLabel2=سيُثبّت هذا المعالج [name/ver] على جهازك.%n%nهذا برنامج حر: يحق لك استخدامه ومشاركته وتعديله وفق رخصة جنو العمومية العامة، التي يمكنك قراءتها في الصفحة التالية.%n%nهذه نسخة تجريبية، فأخبرنا بأي شيء لا يعمل كما تتوقع.
english.WizardInfoBefore=License
english.InfoBeforeLabel=Islamic Screensaver is free software, released under the GNU General Public License version 3:
english.InfoBeforeClickLabel=You don't need to accept anything to install or use it. When you are ready, click Next.
arabic.WizardInfoBefore=الترخيص
arabic.InfoBeforeLabel=هذا برنامج حر، يُنشر وفق رخصة جنو العمومية العامة، الإصدار 3:
arabic.InfoBeforeClickLabel=لا يلزمك قبول أي شيء لتثبيته أو استخدامه. عندما تكون جاهزًا اضغط التالي.

[CustomMessages]
english.LibraryTitle=Where to keep recitations
arabic.LibraryTitle=مكان حفظ التلاوات
english.LibrarySubtitle=Recitations can take a few gigabytes, so you may prefer another drive.
arabic.LibrarySubtitle=قد تشغل التلاوات بضعة جيجابايت، لذا قد تفضّل قرصًا آخر.
english.LibraryExplain=Choose a folder for downloaded recitations, or keep the one suggested. A folder called IslamicScreensaver is made inside it to hold them.%n%nYou can move them later from the screensaver's settings, under Library.
arabic.LibraryExplain=اختر مجلدًا للتلاوات المنزَّلة أو أبقِ المجلد المقترح. سيُنشأ داخله مجلد باسم IslamicScreensaver لحفظها.%n%nيمكنك نقلها لاحقًا من إعدادات شاشة التوقف، في قسم المكتبة.
english.LibraryPreview=Recitations will be saved in:%n%1
arabic.LibraryPreview=ستُحفظ التلاوات في:%n%1
english.LibraryInsideApp=Choose a folder outside the program's own folder, so an update can never erase your recitations.
arabic.LibraryInsideApp=اختر مجلدًا خارج مجلد البرنامج، حتى لا يمحو أي تحديث تلاواتك.
english.LibraryNoDrive=That drive is not available. Choose another folder.
arabic.LibraryNoDrive=هذا القرص غير متاح. اختر مجلدًا آخر.
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
; The library folder is recorded first, so the starter recitations below land in it.
Filename: "{app}\{#ScrName}"; Parameters: "/library ""{code:LibraryFolder}"""; Flags: runhidden waituntilterminated; Check: IsFreshInstall
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

var
  LibraryPage: TInputDirWizardPage;
  LibraryPreview: TNewStaticText;
  FreshInstall: Boolean;

{ The storage page appears on a first install only. With settings already on disk there may be
  gigabytes of recitations somewhere, and choosing a new folder here would leave them behind rather
  than move them; Library > Storage in the app moves them properly. The app enforces this too. }
function InitializeSetup: Boolean;
begin
  FreshInstall := not FileExists(ExpandConstant('{userappdata}\IslamicScreensaver\settings.json'));
  Result := True;
end;

function IsFreshInstall: Boolean;
begin
  Result := FreshInstall;
end;

{ Where the recitations will actually go: a folder of their own inside the one chosen, never loose
  in it. Display only — mirrors AppPaths.LibraryInside, which is what the app really applies. }
function LibraryTarget(Folder: String): String;
var
  Leaf, Parent: String;
begin
  Folder := RemoveBackslashUnlessRoot(Trim(Folder));
  Leaf := ExtractFileName(Folder);
  Parent := ExtractFileName(ExtractFileDir(Folder));
  if (CompareText(Leaf, 'cache') = 0) and (CompareText(Parent, 'IslamicScreensaver') = 0) then
    Result := Folder
  else if CompareText(Leaf, 'IslamicScreensaver') = 0 then
    Result := AddBackslash(Folder) + 'cache'
  else
    Result := AddBackslash(Folder) + 'IslamicScreensaver\cache';
end;

function LibraryFolder(Param: String): String;
begin
  Result := LibraryPage.Values[0];
end;

procedure LibraryChanged(Sender: TObject);
begin
  LibraryPreview.Caption := FmtMessage(CustomMessage('LibraryPreview'), [LibraryTarget(LibraryPage.Values[0])]);
end;

procedure InitializeWizard;
begin
  LibraryPage := CreateInputDirPage(wpInfoBefore,
    CustomMessage('LibraryTitle'), CustomMessage('LibrarySubtitle'),
    CustomMessage('LibraryExplain'), False, '');
  LibraryPage.Add('');
  LibraryPage.Values[0] := ExpandConstant('{localappdata}');

  { The exact final path, under the box, so nobody has to guess what is added to their folder. }
  LibraryPreview := TNewStaticText.Create(LibraryPage);
  LibraryPreview.Parent := LibraryPage.Surface;
  LibraryPreview.Top := LibraryPage.Edits[0].Top + LibraryPage.Edits[0].Height + ScaleY(16);
  LibraryPreview.Left := LibraryPage.Edits[0].Left;
  LibraryPreview.Width := LibraryPage.SurfaceWidth - LibraryPreview.Left;
  LibraryPreview.AutoSize := False;
  LibraryPreview.WordWrap := True;
  LibraryPreview.Height := ScaleY(48);
  LibraryPage.Edits[0].OnChange := @LibraryChanged;
  LibraryChanged(nil);
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := (PageID = LibraryPage.ID) and not FreshInstall;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  Target, AppDir: String;
begin
  Result := True;
  if CurPageID <> LibraryPage.ID then
    Exit;

  Target := LibraryTarget(LibraryPage.Values[0]);
  // WizardDirValue rather than the app constant: this page comes before the (hidden) folder page,
  // and the constant is not set until that has been passed.
  AppDir := AddBackslash(WizardDirValue);
  if CompareText(Copy(AddBackslash(Target), 1, Length(AppDir)), AppDir) = 0 then
  begin
    MsgBox(CustomMessage('LibraryInsideApp'), mbError, MB_OK);
    Result := False;
  end
  else if not DirExists(AddBackslash(ExtractFileDrive(Target))) then
  begin
    MsgBox(CustomMessage('LibraryNoDrive'), mbError, MB_OK);
    Result := False;
  end;
end;

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
