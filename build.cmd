@echo off
chcp 65001 >nul
rem Bygger og tester hele projektet: tests (Core + API), og appen til Windows og Android i Release.
rem Brug: dobbeltklik, eller skriv "build" i en terminal i projektmappen.
cd /d "%~dp0"

echo === Tests (Core og API) ===
dotnet test || goto fejl

echo === App til Windows (Release) ===
dotnet build TiltMaze.App -c Release -f net10.0-windows10.0.19041.0 || goto fejl

echo === App til Android (Release) ===
dotnet build TiltMaze.App -c Release -f net10.0-android || goto fejl

echo.
echo Alt er bygget og testet.
pause
exit /b 0

:fejl
echo.
echo FEJL: se teksten ovenfor.
pause
exit /b 1
