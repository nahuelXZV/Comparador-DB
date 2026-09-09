@echo off
setlocal

for %%I in ("%~dp0..") do set "PROJECT_ROOT=%%~fI"
set "PROJECT_FILE=%PROJECT_ROOT%\src\Desktop\Desktop.csproj"
set "PUBLISH_DIR=%~dp0salida\portable"

if not exist "%PROJECT_FILE%" (
    echo ERROR: No se encontro el proyecto Desktop.csproj.
    exit /b 1
)

if exist "%PUBLISH_DIR%" rmdir /s /q "%PUBLISH_DIR%"
mkdir "%PUBLISH_DIR%"

echo.
echo Publicando la version portable para Windows x64...
dotnet publish "%PROJECT_FILE%" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false -p:DebugType=None -p:DebugSymbols=false -o "%PUBLISH_DIR%"

if errorlevel 1 (
    echo.
    echo ERROR: No se pudo generar la version portable.
    exit /b 1
)

echo.
echo Version portable creada en:
echo %PUBLISH_DIR%
echo.
echo Para distribuirla, comprime el contenido de esa carpeta en un archivo ZIP.
endlocal
