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
# SharpAI and SharpAI.Server multi-target net8.0;net10.0 - pin to net10.0 so the
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

ENV DOTNET_GC_SERVER=1 \
    SHARPAI_FORCE_BACKEND=auto \
    SHARPAI_CPU_VARIANT=auto \
    SHARPAI_REQUIRE_BACKEND=false \
    SHARPAI_ENABLE_NATIVE_LOGGING=false \
    SHARPAI_NUM_THREADS=0 \
    SHARPAI_BATCH_THREADS=0 \
    SHARPAI_GPU_LAYERS=auto \
    SHARPAI_MAIN_GPU=0 \
    SHARPAI_CONTEXT_SIZE=0 \
    SHARPAI_BATCH_SIZE=0 \
    SHARPAI_UBATCH_SIZE=0 \
    SHARPAI_USE_MMAP=true \
    SHARPAI_USE_MLOCK=false \
    SHARPAI_FLASH_ATTENTION=false

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
# Note: Apple Metal GPU acceleration is only available for bare-metal macOS installs.
# Docker containers on Apple Silicon run Linux and cannot access the Metal framework.
# Use bare-metal deployment on macOS for GPU acceleration.
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
RUN mkdir -p /app/logs /app/models /app/temp
COPY --from=build /app/publish .
COPY SharpAI.Server/sharpai.docker.json ./sharpai.json
COPY SharpAI.Server/docker-entrypoint.sh ./start-linux.sh

# Normalize line endings in case the source checkout used CRLF.
RUN sed -i 's/\r$//' start-linux.sh && chmod +x start-linux.sh

EXPOSE 8000
HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=5 \
    CMD curl --fail http://localhost:8000/ready || exit 1

ENTRYPOINT ["./start-linux.sh"]
