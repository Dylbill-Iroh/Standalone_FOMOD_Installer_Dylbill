@echo off
rem Builds publish\StandaloneFomodInstaller.exe (needs the .NET 9 SDK or newer)
cd /d "%~dp0"
dotnet publish src\StandaloneFomodInstaller\StandaloneFomodInstaller.csproj -c Release -o publish
if errorlevel 1 (
    echo.
    echo Build failed.
    pause
    exit /b 1
)
echo.
echo Built: %~dp0publish\StandaloneFomodInstaller.exe
pause
