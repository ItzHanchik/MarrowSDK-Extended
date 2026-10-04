#!/usr/bin/env bash
# ============================================================
#  AuroraRP: сборка релизного zip для Thunderstore / mod.io
#  Результат: ../Builds/AuroraRP-Thunderstore-1.0.0.zip
# ============================================================
set -e

cd "$(dirname "$0")"

VERSION=1.0.0
DLL="../Mod~/bin/AuroraRP.dll"
OUT="../Builds"

if [ ! -f "$DLL" ]; then
  echo "AuroraRP.dll не найден — собираю мод..."
  (cd "../Mod~" && ./build_mod.sh)
fi

[ -f "$DLL" ] || { echo "[ОШИБКА] DLL нет, архив не собран"; exit 1; }

mkdir -p "$OUT"
cp -f "$DLL" "AuroraRP.dll"

rm -f "$OUT/AuroraRP-Thunderstore-$VERSION.zip"
zip -q "$OUT/AuroraRP-Thunderstore-$VERSION.zip" manifest.json README.md icon.png AuroraRP.dll
rm -f "AuroraRP.dll"

echo "Готово: $OUT/AuroraRP-Thunderstore-$VERSION.zip"
echo "Загрузите его на https://thunderstore.io/c/bonelab/ (или как файл на mod.io)."
