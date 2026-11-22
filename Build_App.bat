@echo off
echo Building Slaysi.si 3D Live Wallpaper Windows App...
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./dist
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERROR] Please install .NET 8 SDK from https://dotnet.microsoft.com/download/dotnet/8.0
    pause
    exit /b 1
)
echo.
echo [SUCCESS] Your standalone app is ready at: dist\SlaysiWallpaper.exe
pause
