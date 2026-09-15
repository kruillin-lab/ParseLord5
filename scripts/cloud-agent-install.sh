#!/usr/bin/env bash
# Idempotent Cloud Agent bootstrap for ParseLord5.
# Installs .NET 10, PowerShell, Dalamud hook DLLs, and git submodules.
# Recorded submodule pins may be missing from remotes; fall back to the last
# public SHAs from this repo's history, then origin HEAD.
set -euo pipefail

if [[ -n "${BASH_SOURCE[0]:-}" && -f "${BASH_SOURCE[0]}" ]]; then
  ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
else
  ROOT="$(pwd)"
fi
cd "$ROOT"

export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$DOTNET_ROOT/tools:$HOME/.local/bin:$PATH"
mkdir -p "$HOME/.local/bin"
ln -sfn "$DOTNET_ROOT/dotnet" "$HOME/.local/bin/dotnet"

install_dotnet() {
  if "$DOTNET_ROOT/dotnet" --list-sdks 2>/dev/null | grep -q '^10\.'; then
    echo "dotnet SDK 10 already installed"
    return 0
  fi
  echo "Installing .NET 10 SDK to $DOTNET_ROOT"
  curl -fsSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --channel 10.0 --install-dir "$DOTNET_ROOT"
}

install_pwsh() {
  if command -v pwsh >/dev/null 2>&1; then
    echo "pwsh already installed: $(command -v pwsh)"
    return 0
  fi
  local dest="$HOME/.local/pwsh"
  local version="7.6.6"
  local tarball="/tmp/powershell-${version}-linux-x64.tar.gz"
  echo "Installing PowerShell ${version} to $dest"
  mkdir -p "$dest" "$HOME/.local/bin"
  curl -fsSL -o "$tarball" \
    "https://github.com/PowerShell/PowerShell/releases/download/v${version}/powershell-${version}-linux-x64.tar.gz"
  tar -xzf "$tarball" -C "$dest"
  ln -sfn "$dest/pwsh" "$HOME/.local/bin/pwsh"
  rm -f "$tarball"
}

install_dalamud_hooks() {
  local hooks="${HOME}/.xlcore/dalamud/Hooks/dev"
  if [[ -f "${hooks}/Dalamud.dll" ]]; then
    echo "Dalamud hooks already present at $hooks"
    return 0
  fi
  echo "Downloading Dalamud hooks to $hooks"
  mkdir -p "$hooks"
  local zip="/tmp/dalamud-latest.zip"
  curl -fsSL -o "$zip" https://goatcorp.github.io/dalamud-distrib/latest.zip
  unzip -oq "$zip" -d "$hooks"
  rm -f "$zip"
  test -f "${hooks}/Dalamud.dll"
}

# name url recorded_pin fallback_pin
ensure_submodule() {
  local name="$1"
  local url="$2"
  local pin="$3"
  local fallback="$4"

  if [[ ! -e "${name}/.git" ]]; then
    rm -rf "$name"
    echo "Cloning $name from $url"
    git clone --filter=blob:none "$url" "$name"
  fi

  checkout_sha() {
    local sha="$1"
    if git -C "$name" cat-file -e "${sha}^{commit}" 2>/dev/null; then
      git -C "$name" checkout --detach "$sha"
      return 0
    fi
    if git -C "$name" fetch --depth 1 origin "$sha" 2>/dev/null; then
      git -C "$name" checkout --detach "$sha"
      return 0
    fi
    return 1
  }

  if checkout_sha "$pin"; then
    echo "$name at recorded pin $pin"
    return 0
  fi
  echo "WARN: $name pin $pin is not on the remote"
  if checkout_sha "$fallback"; then
    echo "WARN: $name using last public pin $fallback"
    return 0
  fi
  git -C "$name" fetch origin
  local head
  head="$(git -C "$name" rev-parse origin/HEAD)"
  git -C "$name" checkout --detach "$head"
  echo "WARN: $name using origin HEAD $head"
}

install_dotnet
install_pwsh
install_dalamud_hooks

ensure_submodule ECommons https://github.com/NightmareXIV/ECommons.git \
  c79cd588e7e42c41c2b0572466d78631d9ad1b46 \
  e76756e632558563bbbcb4ffb3d0499ed11e2eea

ensure_submodule PunishLib https://github.com/PunishXIV/PunishLib.git \
  5af0d67484cfad4ebc9b99eef388aeb900688d70 \
  f8b3e807c02f02f0f05fae3cdf7d92bc9d0516b5

ensure_submodule WrathCombo.API https://github.com/PunishXIV/WrathCombo.API.git \
  311c076711757d9554e0376c1c0411c06a4a5d56 \
  a90de1aa53c1a3b95656d3f5d952c15a245138b7

echo "Restoring NuGet packages"
dotnet restore WrathCombo/WrathCombo.csproj
dotnet restore WrathCombo.Tests/WrathCombo.Tests.csproj

echo "cloud-agent-install complete"
dotnet --list-sdks
pwsh -NoLogo -Command '$PSVersionTable.PSVersion'
test -f "${HOME}/.xlcore/dalamud/Hooks/dev/Dalamud.dll"
ls ECommons/ECommons/ECommons.csproj PunishLib/PunishLib/PunishLib.csproj WrathCombo.API/WrathCombo.API.csproj
