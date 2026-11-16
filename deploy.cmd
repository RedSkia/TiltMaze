@echo off
chcp 65001 >nul
rem Kører appen (backend er Supabase).
rem   deploy           = telefonen (Android via USB, USB-fejlfinding slået til)
rem   deploy windows   = Windows-appen på PC'en
cd /d "%~dp0"

if /i "%1"=="windows" goto windows

echo === Telefon ===
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
