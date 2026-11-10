@echo off
rem Builds publish\StandaloneFomodInstaller.exe as one self-contained file:
rem users don't need to install .NET. Building needs the .NET SDK.
cd /d "%~dp0"
if exist publish rmdir /s /q publish
dotnet publish src\StandaloneFomodInstaller\StandaloneFomodInstaller.csproj -c Release -r win-x64 --self-contained true ^
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true ^
    -p:DebugType=none -o publish
if errorlevel 1 (
    echo.
    echo Build failed.
    pause
    exit /b 1
)
echo.
echo Built: %~dp0publish\StandaloneFomodInstaller.exe
pause