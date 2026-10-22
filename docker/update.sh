#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")"

# Docker's short bind syntax turns a missing database file into a directory.
# SQLite creates its schema in an empty file on startup; preserve existing databases.
if [[ -d sharpai.db ]]; then
  echo "ERROR: docker/sharpai.db is a directory, but SQLite requires a file." >&2
  echo "If it is empty, remove it with rmdir sharpai.db, then rerun this script." >&2
  exit 1
fi
if [[ ! -e sharpai.db ]]; then
  touch sharpai.db
fi

docker compose pull && docker compose down && docker compose up -d && docker ps -a
