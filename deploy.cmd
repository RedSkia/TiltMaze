@echo off
chcp 65001 >nul
rem Starter API'et i Docker og kører appen.
rem   deploy           = telefonen (Android via USB, USB-fejlfinding slået til)
rem   deploy windows   = Windows-appen på PC'en
rem Kræver Docker Desktop og .env med CERT_PASSWORD.
cd /d "%~dp0"

set ADB=C:\Program Files (x86)\Android\android-sdk\platform-tools\adb.exe

echo === API i Docker ===
docker compose up -d --build || goto fejl

if /i "%1"=="windows" goto windows

echo === Telefon ===
"%ADB%" devices
rem Telefonen når PC'ens API på localhost:7065 gennem USB-kablet
"%ADB%" reverse tcp:7065 tcp:7065 || goto fejl
dotnet build TiltMaze.App -t:Run -f net10.0-android || goto fejl
echo Appen er startet på telefonen.
pause
exit /b 0

:windows
echo === Windows ===
dotnet run --project TiltMaze.App -f net10.0-windows10.0.19041.0 || goto fejl
exit /b 0

:fejl
echo.
echo FEJL: se teksten ovenfor. Telefon: er kablet i, og er "Tillad USB-fejlfinding" trykket?
pause
exit /b 1
