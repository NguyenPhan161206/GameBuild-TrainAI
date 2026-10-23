#!/usr/bin/env bash
set -euo pipefail
repo_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_dir"
unity_bin="${UNITY_EDITOR:-/home/binhnguyen/Unity/Hub/Editor/6000.6.7f1/Editor/Unity}"
export UPM_CACHE_ROOT="$repo_dir/artifacts/cache/upm"
export XDG_CACHE_HOME="$repo_dir/artifacts/cache"
export XDG_DATA_HOME="$repo_dir/artifacts/cache/data"
export XDG_RUNTIME_DIR="$repo_dir/artifacts/cache/runtime"
mkdir -p "$UPM_CACHE_ROOT" "$XDG_DATA_HOME" "$XDG_RUNTIME_DIR"
chmod 700 "$XDG_RUNTIME_DIR"
if [[ -n "${AEROLAB_CHECK_PROJECT:-}" && "$AEROLAB_CHECK_PROJECT" != "$repo_dir" ]]; then
  python3 - "$repo_dir" "$AEROLAB_CHECK_PROJECT" <<'PY_SYNC'
import pathlib,shutil,sys
source,target=map(pathlib.Path,sys.argv[1:])
for folder in ['Assets','Packages','ProjectSettings']:
    shutil.copytree(source/folder,target/folder,dirs_exist_ok=True)
(target/'tools').mkdir(parents=True,exist_ok=True)
shutil.copy2(source/'tools/prepare-mlagents.py',target/'tools/prepare-mlagents.py')
# Preserve the same relative local-package manifest in a verification copy.
package=source/'artifacts/packages/com.unity.ml-agents'
if package.is_dir():shutil.copytree(package,target/'artifacts/packages/com.unity.ml-agents',dirs_exist_ok=True)
PY_SYNC
fi
case "${1:-validate}" in
  validate) method=AeroLab.Editor.DeliveryBuildTools.Validate; quit=(-quit);;
  smoke) method=AeroLab.Editor.DeliveryBuildTools.Smoke; quit=();;
  layout-smoke) method=AeroLab.Editor.DeliveryBuildTools.LayoutSmoke; quit=();;
  build) method=AeroLab.Editor.DeliveryBuildTools.Linux; quit=(-quit);;
  *) echo 'Usage: check-delivery-unity.sh [validate|smoke|layout-smoke|build]' >&2; exit 2;;
esac
trace_prefix=()
if [[ "${AEROLAB_TRACE_UNITY:-0}" == 1 ]]; then
  trace_prefix=(strace -f -e trace=file -o "$repo_dir/artifacts/delivery-${1:-validate}-trace.log")
fi
log_path="$repo_dir/artifacts/delivery-${1:-validate}.log"
: > "$log_path"
if "${trace_prefix[@]}" "$unity_bin" -batchmode -nographics -job-worker-count 2 "${quit[@]}" -projectPath "${AEROLAB_CHECK_PROJECT:-$repo_dir}" -executeMethod "$method" -logFile "$repo_dir/artifacts/delivery-${1:-validate}.log"; then
  exit 0
else
  editor_status=$?
fi
# Some managed sandboxes kill the Editor after successful native cleanup starts.
# Accept this one exit status only with a fresh success marker and complete artifact.
if [[ "${1:-validate}" == build && "$editor_status" == 137 ]]; then
  if python3 - "${AEROLAB_CHECK_PROJECT:-$repo_dir}" "$log_path" <<'PY_CHECK'
import json, pathlib, sys
project=pathlib.Path(sys.argv[1]); log=pathlib.Path(sys.argv[2]).read_text(errors='replace')
if 'AEROLAB_DELIVERY_BUILD_PASS:' not in log: raise SystemExit(1)
version=json.loads((project/'Builds/Delivery/current.json').read_text())['version']
if len(version)!=32 or any(c not in '0123456789abcdef' for c in version): raise SystemExit(1)
folder=project/'Builds/DeliveryVersions'/version
files=['AeroLabDelivery.x86_64','UnityPlayer.so','AeroLabDelivery_Data/level0','AeroLabDelivery_Data/Managed/AeroLab.Runtime.dll','AeroLabDelivery_Data/Plugins/x86_64/libgrpc_csharp_ext.x64.so']
if not all((folder/name).is_file() and (folder/name).stat().st_size>0 for name in files): raise SystemExit(1)
PY_CHECK
  then
    echo 'AEROLAB_BUILD_ARTIFACT_COMPLETE; Editor cleanup was terminated (137). Verify the standalone player separately.'
    exit 0
  fi
fi
exit "$editor_status"
