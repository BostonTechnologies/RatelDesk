#!/usr/bin/env bash
set -euo pipefail

# --- config ---
DOTNET_CHANNEL="9.0"          # GA channel (use "9.0" for latest 9.x)
INSTALL_DIR="${HOME}/.dotnet" # for script-based install
PROFILE_SNIPPET='export DOTNET_ROOT="$HOME/.dotnet"; export PATH="$HOME/.dotnet:$PATH"'

have_cmd() { command -v "$1" >/dev/null 2>&1; }

echo "➡ Checking existing dotnet..."
if have_cmd dotnet; then
  if dotnet --list-sdks | grep -q "^9\."; then
    echo "✅ .NET 9 SDK already present:"
    dotnet --info
    exit 0
  fi
fi

# Try OS package first (Ubuntu/Debian preferred in Codex envs)
if have_cmd apt-get; then
  echo "➡ Installing .NET 9 from Microsoft APT repo…"
  # Add Microsoft packages repo (idempotent)
  if ! apt-cache policy | grep -qi "packages.microsoft.com"; then
    sudo apt-get update -y
    # Detect Ubuntu/Debian codename
    . /etc/os-release
    MS_DEB="/tmp/packages-microsoft-prod.deb"
    curl -fsSL "https://packages.microsoft.com/config/${ID}/${VERSION_ID}/packages-microsoft-prod.deb" -o "$MS_DEB" || true
    if [ -s "$MS_DEB" ]; then
      sudo dpkg -i "$MS_DEB"
    else
      # Fallback: try generic config
      curl -fsSL "https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb" -o "$MS_DEB"
      sudo dpkg -i "$MS_DEB"
    fi
  fi

  sudo apt-get update -y
  if sudo apt-get install -y dotnet-sdk-9.0; then
    echo "✅ Installed via APT:"
    dotnet --info
    exit 0
  else
    echo "⚠ APT install failed or package unavailable; falling back to script installer."
  fi
fi

# Fallback: official script install (no sudo)
echo "➡ Installing .NET ${DOTNET_CHANNEL} via dotnet-install.sh to ${INSTALL_DIR}…"
mkdir -p "${INSTALL_DIR}"
curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
bash /tmp/dotnet-install.sh --channel "${DOTNET_CHANNEL}" --install-dir "${INSTALL_DIR}" --quality ga

# Ensure PATH for current shell
export DOTNET_ROOT="${INSTALL_DIR}"
export PATH="${INSTALL_DIR}:${PATH}"

# Persist PATH for future shells
if ! grep -q 'DOTNET_ROOT="$HOME/.dotnet"' "${HOME}/.bashrc" 2>/dev/null; then
  echo "${PROFILE_SNIPPET}" >> "${HOME}/.bashrc"
fi
if [ -f "${HOME}/.profile" ] && ! grep -q 'DOTNET_ROOT="$HOME/.dotnet"' "${HOME}/.profile"; then
  echo "${PROFILE_SNIPPET}" >> "${HOME}/.profile"
fi

echo "✅ Installed via script:"
dotnet --info
