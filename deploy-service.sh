#!/bin/bash
# Installs/updates the atlas-report-print systemd unit from this publish folder,
# then reloads systemd so it picks up the change.
#
# Run with sudo, from the directory containing the published app files
# (i.e. wherever atlas-report-print-{DEV,QA,PROD}.service was published to):
#   sudo ./deploy-service.sh

set -e

if [ "$EUID" -ne 0 ]; then
  echo "This script must be run as root (sudo ./deploy-service.sh) — it needs to write to /etc/systemd/system/."
  exit 1
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Each environment's publish output contains exactly one atlas-report-print-*.service
# file (DEV, QA, or PROD) — no need to specify which environment this is.
SERVICE_FILE=$(find "$SCRIPT_DIR" -maxdepth 1 -name "atlas-report-print-*.service" | head -n1)

if [ -z "$SERVICE_FILE" ]; then
  echo "No atlas-report-print-*.service file found in $SCRIPT_DIR — is this a published output folder?"
  exit 1
fi

echo "Installing $(basename "$SERVICE_FILE") as /etc/systemd/system/atlas-report-print.service"
cp "$SERVICE_FILE" /etc/systemd/system/atlas-report-print.service

# Windows-built publish output loses the Unix executable bit on transfer —
# Playwright's bundled Node.js driver under .playwright/ needs it restored,
# every time, or BrowserPool.StartAsync() fails with Win32Exception (13) EACCES.
if [ -d "$SCRIPT_DIR/.playwright" ]; then
  echo "Restoring execute permissions on Playwright's bundled driver..."
  chmod -R +x "$SCRIPT_DIR/.playwright"
fi

systemctl daemon-reload

echo ""
echo "Unit installed and systemd reloaded."
echo "Not started automatically — run when ready:"
echo "  sudo systemctl enable atlas-report-print"
echo "  sudo systemctl start atlas-report-print"
echo "  sudo systemctl status atlas-report-print"
