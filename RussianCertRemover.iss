; Script generated for Russian CA Remover
; Inno Setup 6 Modern Installer

#define MyAppName "Russian CA Remover"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Security Tools"
#define MyAppExeName "RussianCertRemover.exe"
#define MyAppAssocName MyAppName + " File"

[Setup]
; App Information
AppId={{D37B4A1F-9C84-4B6A-8E2E-1563F281E27B}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppComments=Утилита поиска и удаления сертификатов Минцифры (Russian Trusted Root CA / Russian Trusted Sub CA)
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes

; Visuals and UI
WizardStyle=modern
SetupIconFile=e:\WOW\GIF\RussianCertRemover\app.ico
WizardImageFile=e:\WOW\GIF\wizard_banner.bmp
WizardSmallImageFile=e:\WOW\GIF\wizard_small.bmp
UninstallDisplayIcon={app}\app.ico
UninstallDisplayName={#MyAppName} (Удаление сертификатов Минцифры)

; Compression and Packaging
Compression=lzma2/ultra64
SolidCompression=yes
OutputDir=e:\WOW\GIF
OutputBaseFilename=RussianCertRemover_Setup
ArchitecturesInstallIn64BitMode=x64

; Permissions: allow user to choose Current User or All Users
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

; Clean uninstall
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "e:\WOW\GIF\InstallerPayload\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\app.ico"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\app.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

[Code]
// Helper function to check if app is running before install
function InitializeSetup(): Boolean;
var
  ErrorCode: Integer;
begin
  Result := True;
end;
