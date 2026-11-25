#!/bin/bash
# Run the bosses' logic headless and check warnings, openings, air time and scripted fights. No Unity needed.
usage() {
  cat <<'USAGE'
Usage: tools/bosscheck.sh [seeds]

Steps every reworked boss's real fight logic (game/Assets/Scripts/Bosses) without Unity and checks that
every attack follows a warning of at least 24 steps with nothing live during it, that openings never
hurt on contact, that the boss stays in the hall, how much of each phase it spends airborne, and that a
scripted player who dodges and strikes during openings wins on every seed (default 12 seeds).
USAGE
}
case "$1" in -h|--help|help) usage; exit 0;; esac
set -e
source "$(dirname "$0")/_build.sh"
dotnet "$MAPGEN" bosscheck "${1:-12}"
