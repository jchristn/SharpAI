#!/usr/bin/env bash
set -euo pipefail

if [ -z "${1:-}" ]; then
  echo
  echo "Provide a tag argument."
  echo "Example: ./build-dashboard.sh v2.0.0"
  exit 1
fi

cd "$(dirname "$0")"

echo
echo "Building SharpAI dashboard for linux/amd64 and linux/arm64/v8..."
docker buildx build -f dashboard/Dockerfile --builder cloud-jchristn77-jchristn77 --platform linux/amd64,linux/arm64/v8 --tag "jchristn77/sharpai-ui:$1" --tag jchristn77/sharpai-ui:latest --push dashboard

echo
echo "Pulling images into the local registry..."
docker pull "jchristn77/sharpai-ui:$1"
docker pull jchristn77/sharpai-ui:latest

echo
echo "Done"
