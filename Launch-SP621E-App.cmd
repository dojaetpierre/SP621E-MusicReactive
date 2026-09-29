@echo off
rem Launch the self-contained SP621E app (no .NET install needed).
set "APP=%~dp0dist\SP621E.App\SP621E.App.exe"
if not exist "%APP%" (
    echo ERROR: app not published yet. Run:  dotnet publish src/SP621E.App -c Release -r win-x64 --self-contained true -o dist\SP621E.App
    pause
    exit /b 1
)
start "" "%APP%"