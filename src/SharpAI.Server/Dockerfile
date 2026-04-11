#
# SharpAI.Server Dockerfile
# Run docker build from src directory, not from SharpAI.Server
#

#
# Build stage
# .NET 10 SDK on Ubuntu 24.04 (Noble)
#
FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
WORKDIR /src

# Copy source
COPY . .

# Build and publish
# SharpAI and SharpAI.Server multi-target net8.0;net10.0 — pin to net10.0 so the
# publish command produces a single output directory rather than trying to write
# both TFMs into /app/publish.
WORKDIR /src/SharpAI.Server
RUN dotnet publish SharpAI.Server.csproj -c Release -f net10.0 -o /app/publish \
    --no-self-contained \
    /p:UseAppHost=false \
    /p:GeneratePackageOnBuild=false

#
# Runtime stage
# Using Ubuntu 24.04 (Noble) base for glibc 2.39 (ARM64 native libs need 2.38+)
#
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS final

# Install runtime dependencies required by LlamaSharp native libraries
# - libgomp1: OpenMP runtime for parallel processing
# - libstdc++6: C++ standard library
RUN apt-get update && apt-get install -y \
    libgomp1 \
    libstdc++6 \
    iputils-ping \
    traceroute \
    net-tools \
    curl \
    wget \
    dnsutils \
    iproute2 \
    vim \
    file \
    && rm -rf /var/lib/apt/lists/*

# Install NVIDIA CUDA runtime libraries (amd64 only - ARM64 doesn't support NVIDIA GPUs)
# Using Ubuntu 22.04 repository for stable CUDA 12 packages
RUN if [ "$(dpkg --print-architecture)" = "amd64" ]; then \
        apt-get update && apt-get install -y --no-install-recommends \
        gnupg2 ca-certificates && \
        curl -fsSL https://developer.download.nvidia.com/compute/cuda/repos/ubuntu2204/x86_64/3bf863cc.pub | gpg --dearmor -o /usr/share/keyrings/cuda-archive-keyring.gpg && \
        echo "deb [signed-by=/usr/share/keyrings/cuda-archive-keyring.gpg] https://developer.download.nvidia.com/compute/cuda/repos/ubuntu2204/x86_64/ /" > /etc/apt/sources.list.d/cuda.list && \
        apt-get update && apt-get install -y --no-install-recommends \
        cuda-cudart-12-4 \
        libcublas-12-4 && \
        rm -rf /var/lib/apt/lists/*; \
    fi

WORKDIR /app
COPY --from=build /app/publish .

# Create startup script dynamically (avoids Windows line ending issues)
RUN printf '#!/bin/bash\n\
set -e\n\
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"\n\
cd "$SCRIPT_DIR"\n\
\n\
echo ""\n\
echo "SharpAI.Server - Linux Startup"\n\
echo ""\n\
\n\
ARCH=$(uname -m)\n\
if [ "$ARCH" = "x86_64" ]; then\n\
    RID="linux-x64"\n\
elif [ "$ARCH" = "aarch64" ]; then\n\
    RID="linux-arm64"\n\
else\n\
    echo "ERROR: Unsupported architecture: $ARCH"\n\
    exit 1\n\
fi\n\
\n\
echo "Detected architecture: $ARCH (RID: $RID)"\n\
echo ""\n\
\n\
export LD_LIBRARY_PATH="$SCRIPT_DIR/runtimes/$RID/native/avx2:$SCRIPT_DIR/runtimes/$RID/native/cuda12:$SCRIPT_DIR/runtimes/$RID/native:${LD_LIBRARY_PATH}"\n\
\n\
if [ -f "./SharpAI.Server" ]; then\n\
    exec ./SharpAI.Server "$@"\n\
else\n\
    exec dotnet SharpAI.Server.dll "$@"\n\
fi\n' > start-linux.sh

# Make startup script executable
RUN chmod +x start-linux.sh

EXPOSE 8000
ENTRYPOINT ["./start-linux.sh"]
