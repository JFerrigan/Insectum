# Shared by map.sh and chunks.sh: builds tools/MapGen quietly, showing errors only on failure.
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
MAPGEN="$HERE/MapGen/bin/out/MapGen.dll"
if ! BUILD=$(dotnet build "$HERE/MapGen/MapGen.csproj" -v q -nologo -o "$HERE/MapGen/bin/out" 2>&1); then
  echo "$BUILD" | grep -E "error" ; exit 1
fi
