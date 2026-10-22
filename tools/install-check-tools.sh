#!/usr/bin/env bash
# Installs the standalone C# checker, not Unity Editor or a Unity license.
set -euo pipefail
if [[ "$(uname -s)" != Linux || "$(uname -m)" != x86_64 ]]; then
  echo 'This installer targets Linux x64. Install .NET SDK 8 using the official installer for your platform.' >&2
  exit 1
fi
sdk_version=8.0.426
sdk_dir="${AEROLAB_DOTNET_DIR:-/workspace/.tools/dotnet}"
cache_dir=/workspace/.cache/aerolab-setup
sdk_archive="$cache_dir/dotnet-sdk-$sdk_version-linux-x64.tar.gz"
sdk_sha512=8141d01bdc53f604aed3bb661b604707a64209f8a947cd81bf95b4f518951593d77864069191f54ea5e84362a409da6b0f69074bf893b283d7e0cf93e2bf20a8
if [[ -x "$sdk_dir/dotnet" ]] && [[ "$("$sdk_dir/dotnet" --version)" == "$sdk_version" ]]; then
  echo ".NET SDK $sdk_version already installed."
  exit 0
fi
mkdir -p "$cache_dir" "$sdk_dir"
if [[ ! -f "$sdk_archive" ]]; then
  curl --fail --location --silent --show-error \
    "https://builds.dotnet.microsoft.com/dotnet/Sdk/$sdk_version/dotnet-sdk-$sdk_version-linux-x64.tar.gz" \
    --output "$sdk_archive.part"
  mv "$sdk_archive.part" "$sdk_archive"
fi
echo "$sdk_sha512  $sdk_archive" | sha512sum --check --status
tar -xzf "$sdk_archive" -C "$sdk_dir"
"$sdk_dir/dotnet" --version
