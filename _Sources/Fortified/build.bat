@echo off
cd /d "%~dp0"
echo Building Fortified.dll...
dotnet build Fortified.csproj -c Release
if %ERRORLEVEL% EQU 0 (
    echo.
    echo Build succeeded! DLL output to: ..\..\1.6\Assemblies\
) else (
    echo.
    echo Build FAILED. See errors above.
)
pause
