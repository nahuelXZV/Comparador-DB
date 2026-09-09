@echo off
setlocal

set "SCRIPT_DIR=%~dp0"
set "PUBLISH_DIR=%SCRIPT_DIR%salida\portable"
set "INNO_SCRIPT=%SCRIPT_DIR%Comparador.iss"
set "ISCC_EXE="

call "%SCRIPT_DIR%generar-portable.bat"
if errorlevel 1 exit /b 1

where ISCC.exe >nul 2>&1
if not errorlevel 1 set "ISCC_EXE=ISCC.exe"

if not defined ISCC_EXE if exist "%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe" set "ISCC_EXE=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
if not defined ISCC_EXE if exist "%ProgramFiles%\Inno Setup 6\ISCC.exe" set "ISCC_EXE=%ProgramFiles%\Inno Setup 6\ISCC.exe"

if not defined ISCC_EXE (
    echo.
    echo ERROR: No se encontro Inno Setup 6.
    echo Instalalo desde https://jrsoftware.org/isdl.php y vuelve a ejecutar este archivo.
    exit /b 1
)

echo.
echo Creando el instalador...
"%ISCC_EXE%" /DSourceDir="%PUBLISH_DIR%" "%INNO_SCRIPT%"

if errorlevel 1 (
    echo.
    echo ERROR: No se pudo crear el instalador.
    exit /b 1
)

echo.
echo Instalador creado en:
echo %SCRIPT_DIR%salida\instalador\Comparador-Setup.exe
endlocal
