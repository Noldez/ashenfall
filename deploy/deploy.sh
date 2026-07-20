#!/usr/bin/env bash
# Builds the module and deploys it to the game server container's mount.
# Requires deploy/deploy.env (untracked) defining SSH_TARGET, e.g. SSH_TARGET=user@host
set -euo pipefail
cd "$(dirname "$0")/.."
source deploy/deploy.env

MODULE_DIR=/opt/ashenfall/cs2-data/game/sharp/modules/Ashenfall.Core

dotnet publish src/Ashenfall.Core -c Release -o out/Ashenfall.Core
ssh "$SSH_TARGET" "mkdir -p $MODULE_DIR"
scp -q -r out/Ashenfall.Core/* "$SSH_TARGET:$MODULE_DIR/"
if [ -d configs ]; then
  ssh "$SSH_TARGET" "mkdir -p /opt/ashenfall/cs2-data/game/sharp/configs/ashenfall"
  scp -q -r configs/* "$SSH_TARGET:/opt/ashenfall/cs2-data/game/sharp/configs/ashenfall/"
fi
ssh "$SSH_TARGET" "chown -R 1000:1000 /opt/ashenfall/cs2-data/game/sharp && cd /opt/ashenfall && docker compose restart ashenfall-cs2"
echo "Deployed and server restarted."
