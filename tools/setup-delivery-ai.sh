#!/usr/bin/env bash
set -euo pipefail
repo_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_dir"
export UV_CACHE_DIR="$repo_dir/artifacts/cache/uv"
export UV_PYTHON_INSTALL_DIR="$repo_dir/artifacts/tooling/python"
export UV_PYTHON_BIN_DIR="$repo_dir/artifacts/tooling/bin"
if [[ "${1:-}" != --runtime-only ]]; then python3 tools/prepare-mlagents.py; fi
bash tools/install-check-tools.sh
mkdir -p artifacts/tooling/bootstrap
if [[ ! -x artifacts/tooling/bootstrap/bin/uv ]]; then
  python3 -m pip install --target artifacts/tooling/bootstrap 'uv==0.13.5'
fi
uv_bin="$repo_dir/artifacts/tooling/bootstrap/bin/uv"
"$uv_bin" python install 3.10.12
if [[ ! -x .venv/bin/python ]]; then "$uv_bin" venv --python "$UV_PYTHON_INSTALL_DIR/cpython-3.10.12-linux-x86_64-gnu/bin/python3.10" .venv; fi
"$uv_bin" pip install --python .venv/bin/python 'torch==2.2.2' --index-url https://download.pytorch.org/whl/cpu
"$uv_bin" pip install --python .venv/bin/python 'mlagents==1.1.0' 'setuptools==80.9.0'
"$uv_bin" pip install --python .venv/bin/python --requirement training/requirements-observed.txt
"$uv_bin" pip check --python .venv/bin/python
.venv/bin/python -c 'import torch, mlagents.trainers; print("ML-Agents",mlagents.trainers.__version__,"PyTorch",torch.__version__)'
