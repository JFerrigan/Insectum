#!/bin/bash
# Render an Insectum level map to PNG and open it.
#   tools/map.sh                 random seed, level 1
#   tools/map.sh 1234            seed 1234, level 1
#   tools/map.sh 1234 7          seed 1234, level 7 (1-20)
#   tools/map.sh random 13 10    random seed, level 13, zoom 10
# Unity must not have the project open (it locks it).
set -e
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PROJECT="$ROOT/game"
VERSION=$(sed -n 's/^m_EditorVersion: //p' "$PROJECT/ProjectSettings/ProjectVersion.txt")
UNITY="/Applications/Unity/Hub/Editor/$VERSION/Unity.app/Contents/MacOS/Unity"
SEED="${1:-random}"; LEVEL="${2:-1}"; ZOOM="${3:-6}"
ARGS=(-mapLevel "$LEVEL" -mapZoom "$ZOOM")
[ "$SEED" != "random" ] && ARGS+=(-mapSeed "$SEED")
LOG="$(mktemp -t insectum-map)"
if ! "$UNITY" -batchmode -nographics -quit -projectPath "$PROJECT" \
    -executeMethod Insectum.Game.Editor.MapViewer.Export "${ARGS[@]}" -logFile "$LOG"; then
  grep -E "error|Exception|another Unity instance" "$LOG" | head -20
  echo "Unity failed (is the editor open on this project?). Full log: $LOG"; exit 1
fi
OUT=$(sed -n 's/^INSECTUM_MAP //p' "$LOG" | head -1)
sed -n 's/^INSECTUM_MAP_INFO //p' "$LOG" | head -1
echo "$OUT"
open "$OUT"
