#!/usr/bin/env bash
set -euo pipefail

if [ -z "${1:-}" ]; then
  echo "Provide a tag argument for the build."
  echo "Example: ./build-server.sh v4.0.1"
  exit 1
fi

cd "$(dirname "$0")"

echo
echo "Building SharpAI server for linux/amd64 and linux/arm64/v8..."
docker buildx build -f src/SharpAI.Server/Dockerfile --builder cloud-jchristn77-jchristn77 --platform linux/amd64,linux/arm64/v8 --tag "jchristn77/sharpai:$1" --tag jchristn77/sharpai:latest --push src

echo
echo "Pulling images into the local registry..."
docker pull "jchristn77/sharpai:$1"
docker pull jchristn77/sharpai:latest

echo "Done"
