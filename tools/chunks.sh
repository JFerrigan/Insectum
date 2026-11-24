#!/bin/bash
# Render every room template and multi-chunk suite on its own, cropped from real generated levels.
usage() {
  cat <<'USAGE'
Usage: tools/chunks.sh [world|all] [filter] [zoom]

Finds a real example of each room template (one per room shape it fits) and each multi-chunk
suite, renders each cropped to its own chunks into game/Builds/Chunks/<World>/, writes an
index.txt per world (template, shape, seed, level, chunks) and opens the folder.
Needs the dotnet SDK, not Unity. Takes about 25 seconds per world.

  world   forest, swamp, caves, desert, alien, or all (default)
  filter  only templates whose name contains this, e.g. Suite, Cascade, Hollow (default: all)
  zoom    pixels per world unit (default 16)

Examples:
  tools/chunks.sh                     every world, every template
  tools/chunks.sh swamp               Swamp only
  tools/chunks.sh all Suite           every world's multi-chunk suites
  tools/chunks.sh caves Crystal 24    Caves templates named *Crystal*, bigger

Same colors as tools/map.sh. Exits 2 if a template never turned up in 400 seeds.
Whole levels: tools/map.sh
USAGE
}
case "$1" in -h|--help|help) usage; exit 0;; esac
source "$(dirname "$0")/_build.sh"
ROOT="$(cd "$HERE/.." && pwd)"
dotnet "$MAPGEN" chunks "${1:-all}" "${2:-}" "${3:-16}"
STATUS=$?
if [ -n "$1" ] && [ "$1" != "all" ]; then
  WORLD="$(tr '[:lower:]' '[:upper:]' <<< "${1:0:1}")$(tr '[:upper:]' '[:lower:]' <<< "${1:1}")"
  open "$ROOT/game/Builds/Chunks/$WORLD" 2>/dev/null
else
  open "$ROOT/game/Builds/Chunks" 2>/dev/null
fi
exit $STATUS
