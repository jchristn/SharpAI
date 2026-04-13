#!/bin/bash
#
# reset.sh - Reset SharpAI docker environment to factory defaults
#
# This script stops SharpAI containers, destroys runtime state, and restores
# factory-default docker/sharpai.json and docker/sharpai.db.
#
# Usage: ./factory/reset.sh
#

set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
DOCKER_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
FACTORY_DIR="$SCRIPT_DIR"

if [ ! -f "$FACTORY_DIR/sharpai.json" ]; then
  echo "ERROR: Factory config not found: $FACTORY_DIR/sharpai.json"
  exit 1
fi

if [ ! -f "$FACTORY_DIR/sharpai.db" ]; then
  echo "ERROR: Factory database not found: $FACTORY_DIR/sharpai.db"
  exit 1
fi

echo ""
echo "=========================================================="
echo "  SharpAI - Reset Docker Environment to Factory Defaults"
echo "=========================================================="
echo ""
echo "WARNING: This is a DESTRUCTIVE action. The following will"
echo "be permanently reset or deleted:"
echo ""
echo "  - docker/sharpai.json restored from factory defaults"
echo "  - docker/sharpai.db restored from factory defaults"
echo "  - SQLite WAL/SHM sidecar files"
echo "  - All Docker log files"
echo "  - All Docker temp files"
echo "  - All downloaded GGUF models (sharpai-models volume)"
echo ""
read -r -p "Type 'RESET' to confirm: " CONFIRM
echo ""

if [ "$CONFIRM" != "RESET" ]; then
  echo "Aborted. No changes were made."
  exit 1
fi

echo "[1/5] Stopping containers..."
cd "$DOCKER_DIR"
docker compose down 2>/dev/null || true
docker compose -f compose-cpu.yaml down 2>/dev/null || true
docker compose -f compose-cuda.yaml down 2>/dev/null || true

echo "[2/5] Restoring factory configuration and database..."
rm -f "$DOCKER_DIR/sharpai.json"
cp "$FACTORY_DIR/sharpai.json" "$DOCKER_DIR/sharpai.json"

rm -f "$DOCKER_DIR/sharpai.db"
rm -f "$DOCKER_DIR/sharpai.db-shm"
rm -f "$DOCKER_DIR/sharpai.db-wal"
cp "$FACTORY_DIR/sharpai.db" "$DOCKER_DIR/sharpai.db"
echo "        Restored sharpai.json and sharpai.db"

echo "[3/5] Resetting runtime directories..."
mkdir -p "$DOCKER_DIR/logs" "$DOCKER_DIR/temp"
rm -rf "$DOCKER_DIR/logs/"*
rm -rf "$DOCKER_DIR/temp/"*
echo "        Cleared logs and temp files"

echo "[4/5] Removing downloaded models..."
docker volume rm sharpai-models 2>/dev/null || true
echo "        Removed sharpai-models volume"

echo "[5/5] Factory reset complete."
echo ""
echo "To start CPU mode:"
echo "  cd $DOCKER_DIR"
echo "  docker compose -f compose-cpu.yaml up -d"
echo ""
echo "To start CUDA mode:"
echo "  cd $DOCKER_DIR"
echo "  docker compose -f compose-cuda.yaml up -d"
echo ""
