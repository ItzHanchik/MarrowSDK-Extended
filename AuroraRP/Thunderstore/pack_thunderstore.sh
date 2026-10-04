#!/usr/bin/env bash
# ============================================================
#  AuroraRP: сборка релизного zip для Thunderstore / mod.io
#  Результат: ../Builds/AuroraRP-Thunderstore-1.0.0.zip
# ============================================================
set -e

cd "$(dirname "$0")"

VERSION=1.0.0
DLL="../Mod~/bin/AuroraRP.dll"
UPDATER="../Updater/bin/AuroraRPUpdater.dll"
OUT="../Builds"

if [ ! -f "$DLL" ]; then
  echo "AuroraRP.dll не найден — собираю мод..."
  (cd "../Mod~" && ./build_mod.sh)
fi

[ -f "$DLL" ] || { echo "[ОШИБКА] DLL нет, архив не собран"; exit 1; }

mkdir -p "$OUT"
cp -f "$DLL" "AuroraRP.dll"

FILES=(manifest.json README.md icon.png AuroraRP.dll)

if [ -f "$UPDATER" ]; then
  mkdir -p Plugins
  cp -f "$UPDATER" "Plugins/AuroraRPUpdater.dll"
  FILES+=(Plugins)
fi

rm -f "$OUT/AuroraRP-Thunderstore-$VERSION.zip"
zip -q -r "$OUT/AuroraRP-Thunderstore-$VERSION.zip" "${FILES[@]}"
rm -f "AuroraRP.dll"
rm -rf Plugins

echo "Готово: $OUT/AuroraRP-Thunderstore-$VERSION.zip"
echo "Загрузите его на https://thunderstore.io/c/bonelab/ (или как файл на mod.io)."
