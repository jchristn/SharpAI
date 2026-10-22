[Setup]
AppId=SharpAI.Tui
AppName=SharpAI TUI
AppVersion={#AppVersion}
DefaultDirName={localappdata}\SharpAI\Tui
PrivilegesRequired=lowest
OutputDir={#OutputDirectory}
OutputBaseFilename=sharpai-tui-{#AppVersion}-{#RuntimeIdentifier}-setup
UninstallDisplayIcon={app}\sharpai-tui.exe
[Files]
Source: "{#SourceDirectory}\*"; DestDir: "{app}"; Flags: ignoreversion
[Icons]
Name: "{group}\SharpAI TUI"; Filename: "{app}\sharpai-tui.exe"
