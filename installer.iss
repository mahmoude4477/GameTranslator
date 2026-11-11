#define MyAppName "GameTranslator"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "GameTranslator"
#define MyAppURL "https://github.com/gametranslator/gametranslator"
#define MyAppExeName "GameTranslator.exe"
#define MySourceDir "C:\GameTranslator"

[Setup]
AppId={{C46A2B3D-88F4-47B2-BF1A-76902EA820B1}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={localappdata}\Programs\{#MyAppName}
DisableProgramGroupPage=yes
OutputDir=C:\Users\mehme\OneDrive\Desktop\GameTranslator-Release-v1.0.0
OutputBaseFilename=GameTranslator-Setup-v1.0.0
SetupIconFile=C:\Users\mehme\OneDrive\Belgeler\gametranslator\windows\src\GameTranslator.App\Assets\GameTranslator.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "arabic"; MessagesFile: "compiler:Languages\Arabic.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#MySourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -Command ""Get-ChildItem -Path '{app}' -Recurse | Unblock-File"""; Flags: runhidden
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
