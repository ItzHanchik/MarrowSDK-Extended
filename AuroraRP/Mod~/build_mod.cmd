@echo off
chcp 65001 >nul
setlocal

rem ============================================================
rem  AuroraRP: сборка AuroraRP.dll
rem  Требуется .NET SDK 6+ : https://dotnet.microsoft.com/download
rem  BONELAB и BoneLib должны быть установлены.
rem ============================================================

cd /d "%~dp0"

set BONELAB=%BONELAB_DIR%
if "%BONELAB%"=="" set BONELAB=C:\Program Files (x86)\Steam\steamapps\common\BONELAB

echo.
echo === AuroraRP: сборка мода ===
echo Игра:      %BONELAB%
echo.

where dotnet >nul 2>nul
if errorlevel 1 (
  echo [ОШИБКА] dotnet не найден. Установите .NET SDK 6 или новее:
  echo          https://dotnet.microsoft.com/download
  pause
  exit /b 1
)

dotnet build AuroraRP.csproj -c Release -o bin -p:BonelabDir="%BONELAB%"
if errorlevel 1 (
  echo.
  echo [ОШИБКА] Сборка не удалась. Смотрите сообщения выше.
  echo  - проверьте путь к BONELAB (ключ BONELAB_DIR или переменная окружения);
  echo  - проверьте, что BoneLib установлен в папку Mods игры.
  pause
  exit /b 1
)

echo.
echo Готово: %~dp0bin\AuroraRP.dll
echo Скопируйте AuroraRP.dll в папку Mods игры:
echo   %BONELAB%\Mods\
echo.
pause
