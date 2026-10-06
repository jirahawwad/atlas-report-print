#!/bin/bash
# Installs/updates the atlas-report-print systemd unit from this publish folder,
# then reloads systemd so it picks up the change.
#
# Run with sudo, from the directory containing the published app files:
#   sudo ./deploy-service.sh

set -e

if [ "$EUID" -ne 0 ]; then
  echo "This script must be run as root (sudo ./deploy-service.sh) — it needs to write to /etc/systemd/system/."
  exit 1
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Search recursively (removing -maxdepth 1) so it works whether executed 
# from outside the version folder or directly inside it.
SERVICE_FILE=$(find "$SCRIPT_DIR" -name "atlas-report-print-*.service" | head -n1)

if [ -z "$SERVICE_FILE" ]; then
  echo "No atlas-report-print-*.service file found in $SCRIPT_DIR — is this a published output folder?"
  exit 1
fi

# Dynamically set the working directory context to wherever the service file lives
APP_DIR=$(dirname "$SERVICE_FILE")

echo "Installing $(basename "$SERVICE_FILE") as /etc/systemd/system/atlas-report-print.service"
cp "$SERVICE_FILE" /etc/systemd/system/atlas-report-print.service

# Restores Unix executable bits on Playwright's bundled Node.js drivers inside the correct directory context
if [ -d "$APP_DIR/.playwright" ]; then
  echo "Restoring execute permissions on Playwright's bundled driver at $APP_DIR/.playwright ..."
  chmod -R +x "$APP_DIR/.playwright"
fi

systemctl daemon-reload

echo ""
echo "Unit installed and systemd reloaded."
echo "Not started automatically — run when ready:"
echo "  sudo systemctl enable atlas-report-print"
echo "  sudo systemctl start atlas-report-print"
echo "  sudo systemctl status atlas-report-print"
