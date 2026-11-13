@echo off
setlocal
cd /d "%~dp0"

where dotnet >nul 2>&1
if errorlevel 1 (
    echo [ERROR] .NET SDK 10 is required to build Lilly Integrated Mod Manager.
    set "BUILD_EXIT_CODE=1"
    goto :finish
)

dotnet build "%~dp0Source\Lilly.IntegratedModManager.csproj" --configuration Release --nologo
set "BUILD_EXIT_CODE=%ERRORLEVEL%"

if "%BUILD_EXIT_CODE%"=="0" (
    echo.
    echo [SUCCESS] Built: "%~dp0Assemblies\Lilly.IntegratedModManager.dll"
)

:finish
if not "%BUILD_EXIT_CODE%"=="0" echo [ERROR] Build failed with exit code %BUILD_EXIT_CODE%.
if /I not "%~1"=="/nopause" pause
exit /b %BUILD_EXIT_CODE%
