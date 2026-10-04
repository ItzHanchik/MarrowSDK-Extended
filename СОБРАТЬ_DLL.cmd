@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion

rem ============================================================================
rem  AuroraRP — «дай мне просто DLL».
rem  Двойной щёлк по этому файлу: скрипт найдёт BONELAB, соберёт AuroraRP.dll
rem  из настоящих игровых сборок и положит его в папку Mods игры и рядом с собой.
rem
rem  Что нужно один раз поставить: .NET SDK 6 или новее
rem      https://dotnet.microsoft.com/download
rem  (MelonLoader и BoneLib в BONELAB, конечно, уже должны быть.)
rem
rem  Если игра лежит в необычном месте:  set BONELAB_DIR=D:\Путь\К\BONELAB
rem ============================================================================

set "ROOT=%~dp0"
cd /d "%ROOT%"

echo.
echo ============================================
echo   AuroraRP: сборка AuroraRP.dll
echo ============================================
echo.

rem ---------------------------------------------------------------- .NET SDK
where dotnet >nul 2>nul
if errorlevel 1 (
  echo [ОШИБКА] Не найден dotnet ^(.NET SDK 6 или новее^).
  echo.
  echo Установите: https://dotnet.microsoft.com/download
  echo Затем запустите этот файл снова.
  echo.
  pause
  exit /b 1
)

rem ---------------------------------------------------------------- поиск игры
set "BONELAB=%BONELAB_DIR%"

if "%BONELAB%"=="" (
  for %%P in (
    "C:\Program Files (x86)\Steam\steamapps\common\BONELAB"
    "C:\Program Files\Steam\steamapps\common\BONELAB"
    "D:\Steam\steamapps\common\BONELAB"
    "D:\SteamLibrary\steamapps\common\BONELAB"
    "E:\SteamLibrary\steamapps\common\BONELAB"
    "C:\Program Files\Oculus\Software\Software\stress-level-zero-inc-bonelab"
    "D:\Oculus\Software\Software\stress-level-zero-inc-bonelab"
  ) do (
    if "!BONELAB!"=="" if exist "%%~P\BONELAB_Data\il2cpp\MelonLoader\Il2CppAssemblies" set "BONELAB=%%~P"
  )
)

rem Пробуем вытащить путь Steam из реестра
if "%BONELAB%"=="" (
  for /f "tokens=2,*" %%A in ('reg query "HKCU\Software\Valve\Steam" /v SteamPath 2^>nul ^| findstr /i SteamPath') do (
    set "STEAM=%%~B"
    if exist "!STEAM!\steamapps\common\BONELAB\BONELAB_Data" set "BONELAB=!STEAM!\steamapps\common\BONELAB"
  )
)

if "%BONELAB%"=="" (
  echo [ОШИБКА] Не нашёл установленный BONELAB.
  echo.
  echo Укажите путь вручную, например:
  echo     set BONELAB_DIR=D:\Games\BONELAB
  echo     СОБРАТЬ_DLL.cmd
  echo.
  pause
  exit /b 1
)

if not exist "%BONELAB%\BONELAB_Data\il2cpp\MelonLoader\Il2CppAssemblies" (
  echo [ОШИБКА] В папке есть игра, но нет IL2CPP-сборок MelonLoader:
  echo     %BONELAB%\BONELAB_Data\il2cpp\MelonLoader\Il2CppAssemblies
  echo.
  echo Запустите игру один раз с установленным MelonLoader — он создаст эти файлы.
  echo.
  pause
  exit /b 1
)

if not exist "%BONELAB%\Mods\BoneLib.dll" (
  echo [ПРЕДУПРЕЖДЕНИЕ] В %BONELAB%\Mods нет BoneLib.dll — сборка не пройдёт.
  echo Установите BoneLib ^(https://thunderstore.io — BoneLib^) в папку Mods игры.
  echo.
  pause
)

echo Игра:      %BONELAB%
echo.

rem --------------------------------------------------------- красота внутри DLL?
if exist "%ROOT%AuroraRP\Mod~\Runtime\Assets\aurorarp.pack" (
  echo Красота:   пак найден — будет вшит внутрь DLL
) else (
  echo Красота:   пак НЕ найден ^(соберите его в Unity: AuroraRP -^> 3 / 7^).
  echo            DLL соберётся только с логикой: иконки код рисует сам, звуки синтезируются.
)
echo.
echo Собираю... ^(первый раз может занять 1-2 минуты^)
echo.

if exist "%ROOT%build_out" rmdir /s /q "%ROOT%build_out"

dotnet build "%ROOT%AuroraRP\Mod~\AuroraRP.csproj" -c Release -o "%ROOT%build_out" -p:BonelabDir="%BONELAB%" -v minimal
if errorlevel 1 (
  echo.
  echo [ОШИБКА] Сборка не удалась. Смотрите сообщения выше.
  echo  - путь к BONELAB верный? ^(set BONELAB_DIR=...^)
  echo  - BoneLib лежит в %BONELAB%\Mods\BoneLib.dll?
  echo.
  pause
  exit /b 1
)

if not exist "%ROOT%build_out\AuroraRP.dll" (
  echo [ОШИБКА] dotnet отработал, но AuroraRP.dll не появился. Пришлите вывод выше.
  pause
  exit /b 1
)

copy /y "%ROOT%build_out\AuroraRP.dll" "%ROOT%AuroraRP.dll" >nul
copy /y "%ROOT%build_out\AuroraRP.dll" "%BONELAB%\Mods\AuroraRP.dll" >nul

echo.
echo ============================================
echo   ГОТОВО
echo ============================================
echo.
echo DLL:    %ROOT%AuroraRP.dll
echo Ставлен: %BONELAB%\Mods\AuroraRP.dll
echo.
echo Кинь этот AuroraRP.dll в релиз на GitHub — и всё, игроки получат его
echo автообновлением. Палет ставить не надо.
echo.
echo Запусти игру: двойное нажатие обоих триггеров — меню.
echo.

pause
