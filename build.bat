@echo off
REM Builds a single self-contained GitHubReleaseFetcher.exe (no .NET runtime needed on target PC)
REM Requires: .NET 8 SDK  (https://dotnet.microsoft.com/download/dotnet/8.0)
cd /d "%~dp0"
dotnet publish GitHubReleaseFetcher.csproj -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none ^
  -o publish
if errorlevel 1 (
  echo.
  echo BUILD FAILED
  pause
  exit /b 1
)
echo.
echo Done: %~dp0publish\GitHubReleaseFetcher.exe
pause
