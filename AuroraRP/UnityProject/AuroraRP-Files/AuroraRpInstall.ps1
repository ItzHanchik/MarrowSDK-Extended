# AuroraRP — установка без Unity (двойной клик по УСТАНОВИТЬ_БЕЗ_UNITY.cmd)
# Делает то же, что кнопка «Установить всё в BONELAB» в Unity.

$ErrorActionPreference = "Stop"
try { [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12 } catch { }

$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$log  = New-Object System.Collections.Generic.List[string]

function Say($text, $color = "Gray") {
    Write-Host $text -ForegroundColor $color
    $log.Add($text) | Out-Null
}

function Find-Game {
    $cacheFile = Join-Path $env:APPDATA "AuroraRP\game.txt"
    if (Test-Path $cacheFile) {
        $cached = (Get-Content $cacheFile -Raw).Trim()
        if ($cached -and (Test-Path (Join-Path $cached "BONELAB.exe"))) { return $cached }
    }

    $libs = @()
    try { $libs += (Get-ItemProperty "HKCU:\Software\Valve\Steam" -Name SteamPath -ErrorAction Stop).SteamPath } catch { }
    try { $libs += (Get-ItemProperty "HKLM:\SOFTWARE\WOW6432Node\Valve\Steam" -Name InstallPath -ErrorAction Stop).InstallPath } catch { }
    $libs += "C:\Program Files (x86)\Steam", "C:\Program Files\Steam"

    $cands = @()
    foreach ($l in $libs) {
        if (-not $l) { continue }
        $cands += (Join-Path $l "steamapps\common\BONELAB")
        $vdf = Join-Path $l "steamapps\libraryfolders.vdf"
        if (Test-Path $vdf) {
            $raw = Get-Content $vdf -Raw
            foreach ($m in [regex]::Matches($raw, '"path"\s*"([^"]+)"')) {
                $p = $m.Groups[1].Value -replace '\\\\', '\'
                $cands += (Join-Path $p "steamapps\common\BONELAB")
            }
            foreach ($m in [regex]::Matches($raw, '^\s*"\d+"\s*"([^"]+)"', 'Multiline')) {
                $p = $m.Groups[1].Value -replace '\\\\', '\'
                $cands += (Join-Path $p "steamapps\common\BONELAB")
            }
        }
    }
    $cands += "D:\SteamLibrary\steamapps\common\BONELAB", "E:\SteamLibrary\steamapps\common\BONELAB"

    foreach ($c in $cands) { if (Test-Path (Join-Path $c "BONELAB.exe")) { return $c } }
    return $null
}

function Get-File($url, $path) {
    Say ("  качаю " + (Split-Path -Leaf $path) + " ...")
    Invoke-WebRequest -Uri $url -OutFile $path -UseBasicParsing
}

Say "=== AuroraRP: установка в BONELAB ===" "Cyan"

$game = Find-Game
if (-not $game) {
    Say "Папка BONELAB не найдена автоматически." "Yellow"
    Add-Type -AssemblyName System.Windows.Forms
    $dialog = New-Object System.Windows.Forms.FolderBrowserDialog
    $dialog.Description = "Выбери папку BONELAB (там, где BONELAB.exe)"
    if ($dialog.ShowDialog() -ne "OK") { Say "Отменено." "Red"; exit 1 }
    $game = $dialog.SelectedPath
    if (-not (Test-Path (Join-Path $game "BONELAB.exe"))) { Say "В папке нет BONELAB.exe" "Red"; exit 1 }
}

Say ("Папка игры: " + $game) "Green"
try { New-Item -ItemType Directory -Force -Path (Join-Path $env:APPDATA "AuroraRP") | Out-Null; Set-Content -Path (Join-Path $env:APPDATA "AuroraRP\game.txt") -Value $game } catch { }

$temp = Join-Path $env:TEMP "AuroraRP-install"
if (Test-Path $temp) { Remove-Item $temp -Recurse -Force }
New-Item -ItemType Directory -Force -Path $temp | Out-Null

# 1) MelonLoader
if ((Test-Path (Join-Path $game "version.dll")) -and (Test-Path (Join-Path $game "MelonLoader"))) {
    Say "MelonLoader: уже стоит" "Green"
} else {
    Say "MelonLoader: ставлю..." "Yellow"
    $zip = Join-Path $temp "MelonLoader.x64.zip"
    Get-File "https://github.com/LavaGang/MelonLoader/releases/download/v0.6.6/MelonLoader.x64.zip" $zip
    $un = Join-Path $temp "ml"
    Expand-Archive -Path $zip -DestinationPath $un -Force
    $vd = Get-ChildItem $un -Recurse -Filter "version.dll" | Select-Object -First 1
    $root = if ($vd) { $vd.DirectoryName } else { $un }
    Copy-Item (Join-Path $root "*") $game -Recurse -Force
    if (Test-Path (Join-Path $game "version.dll")) { Say "MelonLoader: установлен" "Green" } else { Say "MelonLoader: НЕ установился" "Red" }
}

# 2) папки
$mods = Join-Path $game "Mods"
$plugins = Join-Path $game "Plugins"
New-Item -ItemType Directory -Force -Path $mods, $plugins | Out-Null

if (Test-Path (Join-Path $plugins "AuroraRP.dll")) {
    Move-Item (Join-Path $plugins "AuroraRP.dll") (Join-Path $mods "AuroraRP.dll") -Force
    Say "AuroraRP.dll перенесён из Plugins в Mods" "Yellow"
}

# 3) сам мод
$dll = Join-Path $here "AuroraRP.dll"
if (Test-Path $dll) {
    Copy-Item $dll (Join-Path $mods "AuroraRP.dll") -Force
    Say "AuroraRP.dll -> Mods" "Green"
} else {
    Say "Рядом нет AuroraRP.dll — скачай его из релиза" "Red"
}

if (Test-Path (Join-Path $here "AuroraRPUpdater.dll")) {
    Copy-Item (Join-Path $here "AuroraRPUpdater.dll") (Join-Path $plugins "AuroraRPUpdater.dll") -Force
    Say "AuroraRPUpdater.dll -> Plugins" "Green"
}

# 4) BoneLib
if (Test-Path (Join-Path $mods "BoneLib.dll")) {
    Say "BoneLib: уже стоит" "Green"
} else {
    Say "BoneLib: ставлю..." "Yellow"
    $zip = Join-Path $temp "BoneLib.zip"
    Get-File "https://github.com/yowchap/BoneLib/releases/download/v3.2.2/BoneLib.zip" $zip
    $un = Join-Path $temp "bonelib"
    Expand-Archive -Path $zip -DestinationPath $un -Force
    $n = 0
    Get-ChildItem $un -Recurse -Filter "*.dll" | ForEach-Object { Copy-Item $_.FullName (Join-Path $mods $_.Name) -Force; $n++ }
    Say ("BoneLib: поставлено dll: " + $n) ($(if ($n -gt 0) { "Green" } else { "Red" }))
}

# 5) LabFusion
if (Test-Path (Join-Path $mods "LabFusion.dll")) {
    Say "LabFusion: уже стоит" "Green"
} else {
    Say "LabFusion: ставлю (мультиплеер)..." "Yellow"
    Get-File "https://github.com/Lakatrazz/BONELAB-Fusion/releases/download/v1.14.2/LabFusion.dll" (Join-Path $mods "LabFusion.dll")
    Get-File "https://github.com/Lakatrazz/BONELAB-Fusion/releases/download/v1.14.2/LabFusionUpdater.dll" (Join-Path $plugins "LabFusionUpdater.dll")
    Say "LabFusion: поставлен" "Green"
}

# 6) отчёт
Say ""
Say "В Mods сейчас:" "Cyan"
Get-ChildItem $mods -Filter "*.dll" | ForEach-Object { Say ("  " + $_.Name) }
Say "В Plugins сейчас:" "Cyan"
Get-ChildItem $plugins -Filter "*.dll" | ForEach-Object { Say ("  " + $_.Name) }

$logPath = Join-Path $game "MelonLoader\Latest.log"
if (Test-Path $logPath) {
    $tail = Get-Content $logPath -Tail 200
    $hits = $tail | Where-Object { $_ -match "AuroraRP|MelonLoader v|BoneLib|LabFusion|Exception|Failed" }
    Say ""
    Say "Из лога игры:" "Cyan"
    if ($hits) { $hits | ForEach-Object { Say ("  " + $_) } } else { Say "  (про мод ничего — игра ещё не запускалась с модом)" }
}

$report = Join-Path $game "AuroraRP-Отчёт.txt"
$log | Set-Content -Path $report -Encoding UTF8
Say ""
Say ("Отчёт сохранён: " + $report) "Green"

Say ""
$answer = Read-Host "Запустить BONELAB сейчас? (y/n)"
if ($answer -eq "y" -or $answer -eq "Y") { Start-Process "steam://rungameid/1592190" }

Say ""
Say "Дальше в игре: зайди в уровень и нажми Y + A (или F8 с клавиатуры)." "Cyan"
Write-Host "Нажми Enter, чтобы закрыть..." -ForegroundColor DarkGray
Read-Host
