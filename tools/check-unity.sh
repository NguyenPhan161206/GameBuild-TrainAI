#!/usr/bin/env bash
# Requires an installed and legitimately activated Unity Editor.
set -euo pipefail
repo_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
if [[ -z "${UNITY_EDITOR:-}" || ! -x "$UNITY_EDITOR" ]]; then
  echo 'Set UNITY_EDITOR to the installed Unity 6000.0.62f1 executable. Activate it legitimately before running this check.' >&2
  exit 1
fi
mkdir -p "$repo_dir/artifacts"
"$UNITY_EDITOR" -batchmode -nographics -projectPath "$repo_dir" \
  -executeMethod AeroLab.Editor.BuildTools.Smoke \
  -logFile "$repo_dir/artifacts/unity-smoke.log"
if ! grep -qF 'AEROLAB_PLAYMODE_SMOKE_PASS' "$repo_dir/artifacts/unity-smoke.log"; then
  echo 'Unity exited without the expected smoke-test completion marker. Read artifacts/unity-smoke.log.' >&2
  exit 1
fi
