#!/usr/bin/env bash
# ============================================================================
#  AuroraRP — «дай мне просто DLL» (Linux / macOS / WSL).
#
#  Соберёт AuroraRP.dll из настоящих игровых сборок и положит его
#  в Mods игры и рядом с собой.
#
#  Нужно один раз: .NET SDK 6+  https://dotnet.microsoft.com/download
#  Путь к игре, если нестандартный:  BONELAB_DIR=/path/to/BONELAB ./собрать_dll.sh
# ============================================================================
set -e

ROOT="$(cd "$(dirname "$0")" && pwd)"
cd "$ROOT"

echo
echo "============================================"
echo "  AuroraRP: сборка AuroraRP.dll"
echo "============================================"
echo

if ! command -v dotnet >/dev/null 2>&1; then
  echo "[ОШИБКА] Не найден dotnet (.NET SDK 6 или новее)."
  echo "Установите: https://dotnet.microsoft.com/download"
  exit 1
fi

# ------------------------------------------------------------------ поиск игры
BONELAB="${BONELAB_DIR:-}"

candidates=(
  "$HOME/.steam/steam/steamapps/common/BONELAB"
  "$HOME/.local/share/Steam/steamapps/common/BONELAB"
  "$HOME/.steam/steam/steamapps/common/BONELAB"
  "/mnt/c/Program Files (x86)/Steam/steamapps/common/BONELAB"
  "/mnt/d/SteamLibrary/steamapps/common/BONELAB"
)

if [ -z "$BONELAB" ]; then
  for candidate in "${candidates[@]}"; do
    if [ -d "$candidate/BONELAB_Data/il2cpp/MelonLoader/Il2CppAssemblies" ]; then
      BONELAB="$candidate"
      break
    fi
  done
fi

if [ -z "$BONELAB" ]; then
  echo "[ОШИБКА] Не нашёл установленный BONELAB."
  echo "Укажите путь вручную:  BONELAB_DIR=/путь/к/BONELAB ./собрать_dll.sh"
  exit 1
fi

if [ ! -d "$BONELAB/BONELAB_Data/il2cpp/MelonLoader/Il2CppAssemblies" ]; then
  echo "[ОШИБКА] Нет IL2CPP-сборок MelonLoader:"
  echo "    $BONELAB/BONELAB_Data/il2cpp/MelonLoader/Il2CppAssemblies"
  echo "Запустите игру один раз с установленным MelonLoader — он их создаст."
  exit 1
fi

if [ ! -f "$BONELAB/Mods/BoneLib.dll" ]; then
  echo "[ПРЕДУПРЕЖДЕНИЕ] В $BONELAB/Mods нет BoneLib.dll — сборка не пройдёт."
  echo "Установите BoneLib в папку Mods игры."
  echo
fi

echo "Игра:    $BONELAB"
echo

if [ -f "$ROOT/AuroraRP/Mod~/Runtime/Assets/aurorarp.pack" ]; then
  echo "Красота: пак найден — будет вшит внутрь DLL"
else
  echo "Красота: пак НЕ найден (соберите его в Unity: AuroraRP → 3 / 7)."
  echo "         DLL соберётся только с логикой: иконки код рисует сам, звуки синтезируются."
fi

echo
echo "Собираю (первый раз может занять 1-2 минуты)..."
echo

rm -rf "$ROOT/build_out"

dotnet build "$ROOT/AuroraRP/Mod~/AuroraRP.csproj" \
  -c Release -o "$ROOT/build_out" \
  -p:BonelabDir="$BONELAB" -v minimal

if [ ! -f "$ROOT/build_out/AuroraRP.dll" ]; then
  echo "[ОШИБКА] AuroraRP.dll не появился — пришлите вывод выше."
  exit 1
fi

cp -f "$ROOT/build_out/AuroraRP.dll" "$ROOT/AuroraRP.dll"
cp -f "$ROOT/build_out/AuroraRP.dll" "$BONELAB/Mods/AuroraRP.dll"

echo
echo "============================================"
echo "  ГОТОВО"
echo "============================================"
echo
echo "DLL:     $ROOT/AuroraRP.dll"
echo "Ставлен: $BONELAB/Mods/AuroraRP.dll"
echo
echo "Загрузите AuroraRP.dll в релиз на GitHub — игроки получат его автообновлением."
echo "Палет ставить не надо."
