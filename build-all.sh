#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")"

bash ./build-server.sh "$@"
bash ./build-dashboard.sh "$@"
