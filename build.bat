@echo off
setlocal

:: Always run from the repo root (where this .bat lives)
cd /d "%~dp0"

echo ============================================
echo   Mater2026 - Build ^& Deploy to Revit 2026
echo ============================================
echo.

:: Configuration
set CONFIG=Release
set CSPROJ=MaterRevitAddin\Mater2026.csproj
set REVIT_ADDINS=C:\ProgramData\Autodesk\Revit\Addins\2026
set DEPLOY_DIR=%REVIT_ADDINS%\Mater2026

:: Build
echo [1/3] Compilation %CONFIG%...
dotnet build "%CSPROJ%" -c %CONFIG% --nologo
if %ERRORLEVEL% neq 0 (
    echo.
    echo ERREUR : La compilation a echoue.
    pause
    exit /b 1
)

:: Create deploy folder
echo.
echo [2/3] Deploiement vers %DEPLOY_DIR%...
if not exist "%DEPLOY_DIR%" mkdir "%DEPLOY_DIR%"

:: Copy DLL + dependencies
set OUT=MaterRevitAddin\bin\%CONFIG%\net8.0-windows\win-x64
copy /y "%OUT%\Mater2026.dll" "%DEPLOY_DIR%\" >nul
if %ERRORLEVEL% neq 0 (
    echo ERREUR : Impossible de copier Mater2026.dll
    echo Verifiez que Revit est ferme.
    pause
    exit /b 1
)

:: Copy .addin manifest to Addins root
copy /y "%OUT%\Mater2026.addin" "%REVIT_ADDINS%\" >nul

echo.
echo [3/3] Verification...
echo.
if exist "%DEPLOY_DIR%\Mater2026.dll" (
    echo   OK  %DEPLOY_DIR%\Mater2026.dll
) else (
    echo   MANQUANT  Mater2026.dll
)
if exist "%REVIT_ADDINS%\Mater2026.addin" (
    echo   OK  %REVIT_ADDINS%\Mater2026.addin
) else (
    echo   MANQUANT  Mater2026.addin
)

echo.
echo ============================================
echo   Build termine. Lancez Revit 2026.
echo ============================================
pause
