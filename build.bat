@echo off
setlocal
cd /d "%~dp0"

set "PROJECT=%~dp0Source\Lilly.IntegratedModManager.csproj"
set "OUTPUT=%~dp0Mod\Assemblies\Lilly.IntegratedModManager.dll"

if not exist "%PROJECT%" (
    echo [ERROR] Project file not found:
    echo "%PROJECT%"
    exit /b 1
)

for /r "%~dp0Source" %%F in (*.cs) do (
    set "HAS_CS=1"
    goto :FOUND_CS
)

:FOUND_CS
if not defined HAS_CS (
    echo [INFO] No C# source files exist yet.
    echo [INFO] Project scaffold is ready; C# implementation will be added later.
    exit /b 0
)

where msbuild >nul 2>nul
if errorlevel 1 (
    echo [ERROR] MSBuild was not found.
    echo Install Visual Studio Build Tools with .NET desktop build tools,
    echo or add MSBuild.exe to PATH.
    exit /b 1
)

msbuild "%PROJECT%" /restore /t:Build /p:Configuration=Release /v:minimal
if errorlevel 1 (
    echo [ERROR] Build failed.
    exit /b 1
)

if exist "%OUTPUT%" (
    echo [SUCCESS] Built:
    echo "%OUTPUT%"
) else (
    echo [WARNING] Build completed but output DLL was not found at:
    echo "%OUTPUT%"
)
exit /b 0
