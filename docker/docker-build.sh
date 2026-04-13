#!/bin/bash
#
# Docker build script for SharpAI Server
# Builds a Docker image with both CPU and GPU backend support
#

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
VERSION="${1:-latest}"
IMAGE_NAME="sharpai"
FULL_IMAGE_NAME="${IMAGE_NAME}:${VERSION}"

echo "=================================="
echo "Building SharpAI Docker Image"
echo "=================================="
echo "Image: ${FULL_IMAGE_NAME}"
echo "Build directory: ${SCRIPT_DIR}/../src"
echo ""

# Navigate to src directory
cd "${SCRIPT_DIR}/../src"

# Build the Docker image
echo "Starting Docker build..."
docker build \
    -f SharpAI.Server/Dockerfile \
    -t "${FULL_IMAGE_NAME}" \
    .

echo ""
echo "=================================="
echo "Build Complete!"
echo "=================================="
echo "Image: ${FULL_IMAGE_NAME}"
echo ""
echo "To run in CPU mode:"
echo "  docker run -p 8000:8000 ${FULL_IMAGE_NAME}"
echo ""
echo "To run in GPU mode (requires NVIDIA Docker):"
echo "  docker run --gpus all -p 8000:8000 ${FULL_IMAGE_NAME}"
echo ""
echo "Note: For Apple Metal GPU acceleration, run SharpAI directly on macOS (not in Docker)."
echo ""
