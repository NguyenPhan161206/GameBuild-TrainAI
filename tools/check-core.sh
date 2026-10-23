#!/usr/bin/env bash
set -euo pipefail
repo_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
if command -v dotnet >/dev/null 2>&1; then
  dotnet_bin="$(command -v dotnet)"
elif [[ -x "$repo_dir/artifacts/tooling/dotnet/dotnet" ]]; then
  dotnet_bin="$repo_dir/artifacts/tooling/dotnet/dotnet"
elif [[ -x /workspace/.tools/dotnet/dotnet ]]; then
  dotnet_bin=/workspace/.tools/dotnet/dotnet
else
  echo 'Install .NET SDK 8, or run tools/install-check-tools.sh on Linux x64.' >&2
  exit 1
fi
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
if [[ -d /workspace && -w /workspace ]]; then
  task_cache=/workspace/.cache
else
  task_cache="$repo_dir/artifacts/cache"
fi
export DOTNET_CLI_HOME="${DOTNET_CLI_HOME:-$task_cache/aerolab-dotnet}"
mkdir -p "$task_cache/runtime" "$task_cache/data"
chmod 700 "$task_cache/runtime"
export XDG_RUNTIME_DIR="$task_cache/runtime"
export XDG_DATA_HOME="$task_cache/data"
export NUGET_PACKAGES="${NUGET_PACKAGES:-$task_cache/aerolab-nuget}"
"$dotnet_bin" run --project "$repo_dir/tools/CoreChecks/CoreChecks.csproj" --configuration Release
