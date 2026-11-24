#!/bin/bash
# Render Insectum level maps to PNG and open them. No Unity needed (uses .NET + the game's generator source).
#   tools/map.sh                  random seed, level 1
#   tools/map.sh 1234             seed 1234, level 1
#   tools/map.sh 1234 7           seed 1234, level 7 (1-20)
#   tools/map.sh random all       random seed, every level
#   tools/map.sh 1234 13 10       seed 1234, level 13, zoom 10 px per unit
set -e
HERE="$(cd "$(dirname "$0")" && pwd)"
dotnet build "$HERE/MapGen/MapGen.csproj" -v q -nologo -o "$HERE/MapGen/bin/out" >/dev/null \
  || { dotnet build "$HERE/MapGen/MapGen.csproj" -v q -nologo -o "$HERE/MapGen/bin/out" | grep -E "error" ; exit 1; }
OUTPUT=$(dotnet "$HERE/MapGen/bin/out/MapGen.dll" "${1:-random}" "${2:-1}" "${3:-6}")
echo "$OUTPUT"
FILES=$(echo "$OUTPUT" | sed -n 's/^  \(\/.*\.png\)$/\1/p')
[ -n "$FILES" ] && open $FILES
