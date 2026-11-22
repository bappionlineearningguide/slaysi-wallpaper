; Inno Setup Script for 1-Click Slaysi-Wallpaper-Setup.exe
[Setup]
AppName=Slaysi.si 3D Live Wallpaper
AppVersion=1.0.0
DefaultDirName={autopf}\SlaysiWallpaper
DefaultGroupName=Slaysi.si 3D Wallpaper
OutputDir=dist_installer
OutputBaseFilename=Slaysi-Wallpaper-Setup
Compression=lzma2
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64compatible

[Files]
Source: "dist\SlaysiWallpaper.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\Slaysi.si 3D Live Wallpaper"; Filename: "{app}\SlaysiWallpaper.exe"
Name: "{autodesktop}\Slaysi.si 3D Live Wallpaper"; Filename: "{app}\SlaysiWallpaper.exe"

[Run]
Filename: "{app}\SlaysiWallpaper.exe"; Description: "Launch Slaysi.si 3D Live Wallpaper now"; Flags: nowait postinstall skipifsilent
