@echo off
chcp 65001 >nul
setlocal

rem ============================================================
rem  AuroraRP: сборка релизного zip для Thunderstore (или mod.io).
rem  1) собирает AuroraRP.dll (если его ещё нет);
rem  2) кладёт manifest.json + README.md + icon.png + AuroraRP.dll в архив.
rem  Готовый файл: ..\Builds\AuroraRP-Thunderstore-1.0.0.zip
rem ============================================================

cd /d "%~dp0"

set VERSION=1.0.0
set DLL=..\Mod~\bin\AuroraRP.dll
set UPDATER=..\Updater\bin\AuroraRPUpdater.dll
set OUT=..\Builds

if not exist "%DLL%" (
  echo.
  echo AuroraRP.dll не найден — собираю мод...
  call ..\Mod~\build_mod.cmd
  if errorlevel 1 exit /b 1
)

if not exist "%DLL%" (
  echo [ОШИБКА] Сборка DLL не удалась, архив не собран.
  pause
  exit /b 1
)

if not exist "%OUT%" mkdir "%OUT%"

copy /y "%DLL%" "AuroraRP.dll" >nul

set HASUPDATER=0
if exist "%UPDATER%" (
  mkdir "Plugins" 2>nul
  copy /y "%UPDATER%" "Plugins\AuroraRPUpdater.dll" >nul
  set HASUPDATER=1
)

echo.
echo Упаковываю Thunderstore-пакет...
if "%HASUPDATER%"=="1" (
  powershell -NoProfile -ExecutionPolicy Bypass -Command ^
    "Compress-Archive -Path 'manifest.json','README.md','icon.png','AuroraRP.dll','Plugins' -DestinationPath '%OUT%\AuroraRP-Thunderstore-%VERSION%.zip' -Force"
) else (
  powershell -NoProfile -ExecutionPolicy Bypass -Command ^
    "Compress-Archive -Path 'manifest.json','README.md','icon.png','AuroraRP.dll' -DestinationPath '%OUT%\AuroraRP-Thunderstore-%VERSION%.zip' -Force"
)

del "AuroraRP.dll" >nul 2>nul
rmdir /s /q "Plugins" >nul 2>nul

if errorlevel 1 (
  echo [ОШИБКА] Не удалось создать архив.
  pause
  exit /b 1
)

echo.
echo Готово: %OUT%\AuroraRP-Thunderstore-%VERSION%.zip
echo Загрузите его на https://thunderstore.io/c/bonelab/ (или как файл на mod.io).
echo Версию в manifest.json не забудьте поднять при обновлении.
echo.
pause
