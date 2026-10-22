#!/usr/bin/env bash
# Open the existing checkout directly in the pinned Editor, without Hub discovery.
set -euo pipefail
repo_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
version_file="$repo_dir/ProjectSettings/ProjectVersion.txt"
if [[ ! -d "$repo_dir/Assets" || ! -f "$repo_dir/Packages/manifest.json" || ! -f "$version_file" ]]; then
  echo "Not a complete Unity checkout: $repo_dir" >&2
  exit 1
fi
editor_version="$(sed -n 's/^m_EditorVersion: //p' "$version_file" | tr -d '\r')"
if [[ -z "$editor_version" ]]; then
  echo "Cannot read the required Unity version from $version_file" >&2
  exit 1
fi
editor_bin="${UNITY_EDITOR:-}"
if [[ -n "$editor_bin" && ! -x "$editor_bin" ]]; then
  echo "UNITY_EDITOR is not an executable file: $editor_bin" >&2
  exit 1
fi
if [[ -z "$editor_bin" ]]; then
  candidates=(
    "$HOME/Unity/Hub/Editor/$editor_version/Editor/Unity"
    "$HOME/Applications/Unity/Hub/Editor/$editor_version/Editor/Unity"
    "/opt/unity/Hub/Editor/$editor_version/Editor/Unity"
    "/opt/Unity/Hub/Editor/$editor_version/Editor/Unity"
    "/opt/unity/editors/$editor_version/Editor/Unity"
    "/Applications/Unity/Hub/Editor/$editor_version/Unity.app/Contents/MacOS/Unity"
  )
  for candidate in "${candidates[@]}"; do
    if [[ -x "$candidate" ]]; then
      editor_bin="$candidate"
      break
    fi
  done
fi
if [[ -z "$editor_bin" ]]; then
  echo "Unity Editor $editor_version was not found in the standard installation directories." >&2
  echo 'In Unity Hub, open Installs and install that Editor version, or locate its executable.' >&2
  echo 'For a custom install, run:' >&2
  echo "  UNITY_EDITOR='/absolute/path/to/Editor/Unity' bash '$repo_dir/tools/open-unity.sh'" >&2
  echo 'Installing Unity Hub alone does not install Unity Editor.' >&2
  exit 1
fi
if [[ -f "$repo_dir/Temp/UnityLockfile" ]]; then
  echo 'A Unity lock file exists. If the project is already open, use that Editor window.' >&2
  echo 'This helper will not remove locks or close existing Editor processes.' >&2
fi
echo "Opening project: $repo_dir"
echo "Editor: $editor_bin"
exec "$editor_bin" -projectPath "$repo_dir" -executeMethod AeroLab.Editor.BuildTools.PlayArena "$@"
