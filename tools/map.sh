#!/bin/bash
# Render Insectum level maps to PNG (plus a text listing) and open them. No Unity needed.
usage() {
  cat <<'USAGE'
Usage: tools/map.sh [seed|random] [level|all] [zoom]

Renders generated levels with the game's own generator (needs the dotnet SDK, not Unity)
into game/Builds/Maps/seed<N>_level<NN>.png and .txt, prints the listing and opens the PNGs.

  seed    a number, or random (default)
  level   1-20 (default 1; 4, 8, 12, 16, 20 are boss arenas), or all
  zoom    pixels per world unit (default 6)

Examples:
  tools/map.sh                  random seed, level 1
  tools/map.sh 1234 7           seed 1234, level 7
  tools/map.sh random all       random seed, every level
  tools/map.sh 1234 13 10       level 13 at zoom 10

Image: dark is open air, muted colors are rock, pale lines are chunk borders, pink outlines
are multi-chunk rooms, yellow is the route. Green spawn, red exit, blue checkpoints.
Multi-chunk spawn statistics: dotnet tools/MapGen/bin/out/MapGen.dll stats [seeds]
Single rooms and suites on their own: tools/chunks.sh
USAGE
}
case "$1" in -h|--help|help) usage; exit 0;; esac
set -e
source "$(dirname "$0")/_build.sh"
OUTPUT=$(dotnet "$MAPGEN" "${1:-random}" "${2:-1}" "${3:-6}")
echo "$OUTPUT"
FILES=$(echo "$OUTPUT" | sed -n 's/^  \(\/.*\.png\)$/\1/p')
[ -n "$FILES" ] && open $FILES
