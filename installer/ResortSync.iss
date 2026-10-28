; ============================================================================
;  PRPMS — Inno Setup installer script
;  Compile with:  ISCC.exe installer\ResortSync.iss
;  Output:        installer\Output\PRPMS-Setup-1.0.0.exe
;
;  Ships the self-contained publish produced by:
;    dotnet publish src\ResortSync\ResortSync.csproj -c Release -r win-x64
;                    --self-contained true -o publish
;  (No .NET runtime prerequisite required on the target machine.)
; ============================================================================

#define MyAppName "PRPMS"
#define MyAppNameFull "PRPMS — Private Resort Payment Monitoring System"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Terelle Sarmiento"
#define MyAppExeName "PRPMS.exe"

[Setup]
; Stable AppId so upgrades/uninstalls find the same installation
AppId={{7C9E4B2A-5D18-4F6E-9A31-8B2D0F4E6C55}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppNameFull} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
; 64-bit install on x64/ARM64 Windows
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Installer + app icons
SetupIconFile=..\src\ResortSync\Assets\app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
; Output
OutputDir=Output
OutputBaseFilename=PRPMS-Setup-{#MyAppVersion}
; Compression
Compression=lzma2/max
SolidCompression=yes
; Modern look
WizardStyle=modern
; Per-machine install (Program Files)
PrivilegesRequired=admin
; Do not restart anything
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"

[Files]
; Full self-contained publish output (500+ files, .NET runtime included)
Source: "..\publish\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

[Icons]
; Start Menu
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; \
    Comment: "Private resort payment monitoring"
; Desktop
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; \
    Description: "Launch {#MyAppName}"; \
    Flags: nowait postinstall skipifsilent

; Note: application data (%LocalAppData%\ResortSync — database + settings)
; is intentionally preserved on uninstall so guest/booking history survives.
