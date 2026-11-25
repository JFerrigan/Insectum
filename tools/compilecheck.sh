#!/bin/bash
# Compile every game and editor script against Unity's DLLs without opening Unity. Works while the editor
# has the project open (unlike Unity's batch verification). Prints errors, or "Compile check passed."
usage() {
  cat <<'USAGE'
Usage: tools/compilecheck.sh

Builds game/Assets/Scripts and game/Assets/Editor as one C# 9 library against the UnityEngine and
UnityEditor DLLs of the editor version in game/ProjectSettings/ProjectVersion.txt (found under
/Applications/Unity/Hub/Editor, or set UNITY_MANAGED to its Contents/Resources/Scripting/Managed/UnityEngine
folder). Needs the dotnet SDK. It catches compile errors only: it runs nothing.
USAGE
}
case "$1" in -h|--help|help) usage; exit 0;; esac
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
VERSION=$(sed -n 's/^m_EditorVersion: //p' "$HERE/../game/ProjectSettings/ProjectVersion.txt")
MANAGED="${UNITY_MANAGED:-/Applications/Unity/Hub/Editor/$VERSION/Unity.app/Contents/Resources/Scripting/Managed/UnityEngine}"
if [ ! -f "$MANAGED/UnityEngine.CoreModule.dll" ]; then
  echo "Unity $VERSION's DLLs are not at $MANAGED; set UNITY_MANAGED." >&2
  exit 1
fi
if OUTPUT=$(dotnet build "$HERE/CompileCheck/CompileCheck.csproj" -v q -nologo -p:UnityManaged="$MANAGED" 2>&1); then
  echo "Compile check passed."
else
  echo "$OUTPUT" | grep -E "error" | sed 's/ \[.*//' | sort -u
  exit 1
fi
