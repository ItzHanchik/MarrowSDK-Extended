#!/usr/bin/env bash
# ============================================================
#  AuroraRP: сборка AuroraRP.dll (Linux/macOS, .NET SDK 6+)
#  Игра:  BONELAB_DIR=/path/to/BONELAB  ./build_mod.sh
# ============================================================
set -e

cd "$(dirname "$0")"

BONELAB="${BONELAB_DIR:-$HOME/.steam/steam/steamapps/common/BONELAB}"

echo
echo "=== AuroraRP: сборка мода ==="
echo "Игра: $BONELAB"
echo

if ! command -v dotnet >/dev/null 2>&1; then
  echo "[ОШИБКА] dotnet не найден. Установите .NET SDK 6+: https://dotnet.microsoft.com/download"
  exit 1
fi

dotnet build AuroraRP.csproj -c Release -o bin -p:BonelabDir="$BONELAB"

echo
echo "Готово: $(pwd)/bin/AuroraRP.dll"
echo "Скопируйте его в: $BONELAB/Mods/"
