# SharpAI Deployment Guide

Complete guide for deploying, configuring, and testing SharpAI across all platforms.

---

## Table of Contents

1. [Quick Start](#quick-start)
2. [System Requirements](#system-requirements)
3. [Installation](#installation)
   - [Docker (Recommended)](#docker-recommended)
   - [Docker Compose](#docker-compose)
   - [Windows](#windows)
   - [macOS](#macos)
   - [Linux/Ubuntu](#linuxubuntu)
4. [Configuration](#configuration)
5. [Using SharpAI](#using-sharpai)
6. [Testing & Verification](#testing--verification)
7. [Troubleshooting](#troubleshooting)
8. [Production Deployment](#production-deployment)

---

## Quick Start

**Fastest way to get running:**

```bash
# CPU mode
docker run -d -p 8000:8000 --name sharpai jchristn77/sharpai:latest

# GPU mode (NVIDIA GPU required)
docker run -d --gpus all -p 8000:8000 --name sharpai jchristn77/sharpai:latest

# Verify
curl http://localhost:8000/
```

**That's it!** Skip to [Using SharpAI](#using-sharpai).

---

## System Requirements

### Hardware

**Minimum:**
- CPU: x86_64 (64-bit) or ARM64
- RAM: Minimum 8GB of RAM recommended, have enough RAM for running models if using CPU
- Disk: 20GB+ of disk space recommended, have enough capacity for downloaded models

**For GPU Acceleration (Optional):**

*NVIDIA CUDA (Windows/Linux):*
- NVIDIA GPU with Compute Capability 6.0+ (Pascal or newer)
- 8GB+ VRAM (16GB+ for larger models)
- NVIDIA proprietary drivers
- CUDA Toolkit 12.x (bare-metal only)
- NVIDIA Container Toolkit (Docker only)

*Apple Metal (macOS Apple Silicon):*
- Apple M1, M2, M3, or M4 chip
- macOS 13 (Ventura) or later
- Bare-metal installation only (Docker containers run Linux and cannot access Metal)

**Note:** AMD/Intel GPUs are not supported.

### Software

- **.NET 8 SDK** (bare-metal) or **.NET 8 Runtime** (Docker)
- **Docker** (for container deployments)
- **Git** (for building from source)
- **HuggingFace API Key** (required for downloading models)
  - Get free key: https://huggingface.co/settings/tokens

### Supported Platforms

| Platform | CPU | GPU |
|----------|-----|-----|
| Windows x64 | ✅ | ✅ (CUDA) |
| Linux x64 | ✅ | ✅ (CUDA) |
| macOS Apple Silicon (ARM64) | ✅ | ✅ (Metal) |
| macOS Intel (x64) | ✅ | ❌ |
| Docker on Apple Silicon | ✅ | ❌ (Metal requires bare-metal macOS) |

---

## Installation

### Docker (Recommended)

**Why Docker?**
- No platform-specific dependencies
- Automatic backend detection (CPU/GPU)
- Clean, isolated environment
- Easy updates

**Prerequisites:** Docker installed
- Windows/Mac: [Docker Desktop](https://www.docker.com/products/docker-desktop)
- Linux: `sudo apt install docker.io`

**CPU Mode:**
```bash
docker run -d -p 8000:8000 --name sharpai jchristn77/sharpai:latest
```

**Force CPU Mode (even with GPU available):**
```bash
docker run -d -e SHARPAI_FORCE_BACKEND=cpu -p 8000:8000 --name sharpai jchristn77/sharpai:latest
```

**GPU Mode:**
```bash
docker run -d --gpus all -p 8000:8000 --name sharpai jchristn77/sharpai:latest
```

**With persistent storage:**
```bash
docker run -d \
  -p 8000:8000 \
  -v $(pwd)/models:/app/models \
  -v $(pwd)/sharpai.json:/app/sharpai.json \
  --name sharpai \
  jchristn77/sharpai:latest
```

### Docker Compose

Create `docker-compose.yml`:

**CPU Mode:**
```yaml
version: '3.8'
services:
  sharpai:
    image: jchristn77/sharpai:latest
    ports:
      - "8000:8000"
    volumes:
      - ./models:/app/models
      - ./sharpai.json:/app/sharpai.json
    environment:
      - SHARPAI_FORCE_BACKEND=cpu  # Force CPU even if GPU is available
    restart: unless-stopped
```

**GPU Mode:**
```yaml
version: '3.8'
services:
  sharpai:
    image: jchristn77/sharpai:latest
    ports:
      - "8000:8000"
    volumes:
      - ./models:/app/models
      - ./sharpai.json:/app/sharpai.json
    restart: unless-stopped
    deploy:
      resources:
        reservations:
          devices:
            - driver: nvidia
              count: all
              capabilities: [gpu]
```

**Start:**
```bash
docker-compose up -d
```

**View logs:**
```bash
docker-compose logs -f
```

**Stop:**
```bash
docker-compose down
```

### Windows

1. **Install .NET 8 SDK**
   ```powershell
   # Download: https://dotnet.microsoft.com/download/dotnet/8.0
   # Or use winget:
   winget install Microsoft.DotNet.SDK.8
   ```

2. **Clone and build**
   ```powershell
   git clone https://github.com/jchristn/sharpai.git
   cd sharpai\src
   dotnet build SharpAI.sln
   ```

3. **Configure HuggingFace API key**
   ```powershell
   cd SharpAI.Server
   @"
   {
     "HuggingFace": {
       "ApiKey": "hf_YOUR_API_KEY_HERE"
     }
   }
   "@ | Out-File -FilePath "sharpai.json" -Encoding UTF8
   ```

4. **Run**
   ```powershell
   cd bin\Debug\net8.0
   .\start-windows.bat
   ```

**Startup script automatically:**
- Detects CPU/GPU
- Loads correct native libraries
- Configures backend

**GPU Support on Windows:**
- Requires NVIDIA GPU with latest drivers
- Install CUDA Toolkit 12.x from: https://developer.nvidia.com/cuda-downloads
- Server auto-detects GPU

### macOS

**Prerequisites:**

1. **Xcode Command Line Tools**
   ```bash
   xcode-select --install
   ```

2. **Install .NET 8 SDK (ARM64 for Apple Silicon)**
   - Download: https://dotnet.microsoft.com/download/dotnet/8.0
   - Choose: **macOS ARM64 Installer** (M1/M2/M3/M4)
   - Or: `brew install dotnet@8`

   **Verify ARM64:**
   ```bash
   dotnet --info | grep RID
   # Should show: osx-arm64
   ```

**Installation:**

1. **Clone and build**
   ```bash
   git clone https://github.com/jchristn/sharpai.git
   cd sharpai/src
   dotnet build SharpAI.sln
   ```

2. **Configure HuggingFace API key**
   ```bash
   cd SharpAI.Server
   cat > sharpai.json << 'EOF'
   {
     "HuggingFace": {
       "ApiKey": "hf_YOUR_API_KEY_HERE"
     }
   }
   EOF
   ```

3. **Run**
   ```bash
   cd bin/Debug/net8.0
   chmod +x start-mac.sh
   ./start-mac.sh
   ```

**The startup script automatically:**
- Detects architecture (Intel/Apple Silicon)
- Validates dependencies exist
- Fixes library path references (macOS-specific issue)
- Starts the server

**Apple Silicon Note:**
- Metal GPU acceleration is auto-detected on bare-metal macOS
- Force Metal: `"Runtime": {"ForceBackend": "metal"}` or `SHARPAI_FORCE_BACKEND=metal`
- Docker on Apple Silicon runs Linux containers — Metal is not available, CPU is used instead

**Troubleshooting macOS:**

If you get library loading errors:

1. **Run diagnostics:**
   ```bash
   ./diagnose-mac.sh
   ```

2. **Fix dependencies:**
   ```bash
   ./fix-mac-dependencies.sh
   ```

3. **Restart server:**
   ```bash
   ./start-mac.sh
   ```

### Linux/Ubuntu

**Prerequisites:**

1. **System updates**
   ```bash
   sudo apt update && sudo apt upgrade -y
   ```

2. **Install .NET 8 SDK**
   ```bash
   wget https://packages.microsoft.com/config/ubuntu/$(lsb_release -rs)/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
   sudo dpkg -i packages-microsoft-prod.deb
   rm packages-microsoft-prod.deb

   sudo apt update
   sudo apt install -y dotnet-sdk-8.0
   ```

3. **Install build essentials**
   ```bash
   sudo apt install -y build-essential libgomp1
   ```

4. **GPU Support (Optional)**

   **NVIDIA Drivers:**
   ```bash
   # Check for GPU
   lspci | grep -i nvidia

   # Install drivers
   sudo apt install -y nvidia-driver-535
   sudo reboot

   # Verify
   nvidia-smi
   ```

   **CUDA Toolkit:**
   ```bash
   # Install CUDA 12.x
   wget https://developer.download.nvidia.com/compute/cuda/repos/ubuntu2204/x86_64/cuda-keyring_1.1-1_all.deb
   sudo dpkg -i cuda-keyring_1.1-1_all.deb
   sudo apt update
   sudo apt install -y cuda-toolkit-12-4
   ```

**Installation:**

1. **Clone and build**
   ```bash
   git clone https://github.com/jchristn/sharpai.git
   cd sharpai/src
   dotnet build SharpAI.sln
   ```

2. **Configure HuggingFace API key**
   ```bash
   cd SharpAI.Server
   cat > sharpai.json << 'EOF'
   {
     "HuggingFace": {
       "ApiKey": "hf_YOUR_API_KEY_HERE"
     }
   }
   EOF
   ```

3. **Run**
   ```bash
   cd bin/Debug/net8.0
   chmod +x start-linux.sh
   ./start-linux.sh
   ```

**The startup script automatically:**
- Detects architecture (x64/ARM64)
- Detects CPU features (AVX2/AVX)
- Pre-loads library dependencies
- Validates all dependencies exist
- Starts the server

**Run as systemd service:**

```bash
sudo nano /etc/systemd/system/sharpai.service
```

Add (adjust paths for your username):
```ini
[Unit]
Description=SharpAI Server
After=network.target

[Service]
Type=simple
User=yourusername
WorkingDirectory=/home/yourusername/sharpai/src/SharpAI.Server/bin/Debug/net8.0
ExecStart=/home/yourusername/sharpai/src/SharpAI.Server/bin/Debug/net8.0/start-linux.sh
Restart=on-failure
RestartSec=10
KillSignal=SIGINT

[Install]
WantedBy=multi-user.target
```

Enable and start:
```bash
sudo systemctl daemon-reload
sudo systemctl enable sharpai
sudo systemctl start sharpai
sudo systemctl status sharpai
```

---

## Configuration

### Configuration File

All settings are in `sharpai.json` (auto-created on first run if missing).

**Minimal configuration:**
```json
{
  "HuggingFace": {
    "ApiKey": "hf_YOUR_API_KEY_HERE"
  }
}
```

**Complete configuration:**
```json
{
  "HuggingFace": {
    "ApiKey": "hf_YOUR_API_KEY_HERE"
  },
  "Runtime": {
    "ForceBackend": null,
    "CpuBackendPath": null,
    "GpuBackendPath": null,
    "EnableNativeLogging": false
  },
  "Storage": {
    "ModelsDirectory": "./models/"
  },
  "Rest": {
    "Port": 8000,
    "Host": "localhost"
  },
  "Logging": {
    "ConsoleLogging": true,
    "LogDirectory": "./logs/"
  }
}
```

### Runtime Backend Options

**Environment Variable: SHARPAI_FORCE_BACKEND** (Highest Priority)
- Override backend selection via environment variable
- Takes precedence over configuration file settings
- Values: `cpu`, `cuda`, or `metal`
- Usage examples:
  ```bash
  # Docker
  docker run -e SHARPAI_FORCE_BACKEND=cpu -p 8000:8000 jchristn77/sharpai:latest

  # Bare metal (Linux/Mac)
  export SHARPAI_FORCE_BACKEND=cpu
  ./start-linux.sh

  # Bare metal (Windows)
  set SHARPAI_FORCE_BACKEND=cpu
  start-windows.bat
  ```

**ForceBackend** (Configuration File)
- Override automatic detection in `sharpai.json`
- Values: `null` (default), `"cpu"`, `"cuda"`, or `"metal"`
- Note: Environment variable takes precedence over this setting

**CpuBackendPath** - Custom CPU library path
- Default: Auto-detected from NuGet packages
- Supports environment variables: `$HOME`, `%USERPROFILE%`

**GpuBackendPath** - Custom GPU (CUDA) library path
- Default: Auto-detected from NuGet packages
- Supports environment variables

**MetalBackendPath** - Custom Metal library path (macOS Apple Silicon only)
- Default: Auto-detected from NuGet packages
- Supports environment variables

**EnableNativeLogging** - Show llama.cpp debug output
- `false` (default) - Clean console
- `true` - Verbose native library logs (debugging only)

**Backend Selection Priority:**
1. `SHARPAI_FORCE_BACKEND` environment variable (highest)
2. `Runtime.ForceBackend` in configuration file
3. Automatic GPU detection (lowest)

### How Auto-Detection Works

At startup, SharpAI:

1. **Detects platform** - Windows/Linux/macOS
2. **Detects architecture** - x64/ARM64
3. **Checks for GPU**:
   - Apple Silicon (macOS ARM64): Checks for `libggml-metal.dylib` → Metal backend
   - NVIDIA driver files
   - Environment variables (`NVIDIA_VISIBLE_DEVICES`)
   - `nvidia-smi` command
   - CUDA libraries
4. **Selects backend**:
   - Metal: If Apple Silicon detected and Metal library present (macOS bare-metal only)
   - CUDA: If NVIDIA GPU detected (Windows/Linux)
   - CPU: If no GPU detected, or in Docker on Apple Silicon

**Platform Support:**

| Platform | Architecture | CPU | GPU |
|----------|--------------|-----|-----|
| Windows | x64 | ✅ | ✅ (CUDA) |
| Linux | x64 | ✅ | ✅ (CUDA) |
| macOS | ARM64 (Apple Silicon) | ✅ | ✅ (Metal) |
| macOS | x64 (Intel) | ✅ | ❌ |
| Docker on Apple Silicon | ARM64 | ✅ | ❌ (Metal requires bare-metal macOS) |

---

## Using SharpAI

Once running, SharpAI is accessible at `http://localhost:8000`

### Pull a Model

**Required first step** - Download a GGUF format model:

**Embeddings:**
```bash
curl -X POST http://localhost:8000/api/pull \
  -H "Content-Type: application/json" \
  -d '{"model":"leliuga/all-MiniLM-L6-v2-GGUF"}'
```

**Completions:**
```bash
curl -X POST http://localhost:8000/api/pull \
  -H "Content-Type: application/json" \
  -d '{"model":"QuantFactory/Qwen2.5-3B-GGUF"}'
```

**Note:** Only GGUF format models are supported. Search HuggingFace: https://huggingface.co/models?search=gguf

### Generate Embeddings

```bash
curl -X POST http://localhost:8000/api/embed \
  -H "Content-Type: application/json" \
  -d '{"model":"leliuga/all-MiniLM-L6-v2-GGUF","input":"Hello, World!"}'
```

### Generate Text

```bash
curl -X POST http://localhost:8000/api/generate \
  -H "Content-Type: application/json" \
  -d '{"model":"QuantFactory/Qwen2.5-3B-GGUF","prompt":"Once upon a time","stream":false}'
```

### Chat Completion

```bash
curl -X POST http://localhost:8000/api/chat \
  -H "Content-Type: application/json" \
  -d '{
    "model":"QuantFactory/Qwen2.5-3B-GGUF",
    "messages":[
      {"role":"user","content":"What is the capital of France?"}
    ],
    "stream":false
  }'
```

### List Models

```bash
curl http://localhost:8000/api/tags
```

### Delete a Model

```bash
curl -X DELETE http://localhost:8000/api/delete \
  -H "Content-Type: application/json" \
  -d '{"name":"leliuga/all-MiniLM-L6-v2-GGUF"}'
```

---

## Testing & Verification

### Quick Test Sequence

After installation, verify everything works:

```bash
# 1. Check server health
curl http://localhost:8000/

# 2. Pull test model
curl -X POST http://localhost:8000/api/pull \
  -H "Content-Type: application/json" \
  -d '{"model":"leliuga/all-MiniLM-L6-v2-GGUF"}'

# 3. Test embeddings
curl -X POST http://localhost:8000/api/embed \
  -H "Content-Type: application/json" \
  -d '{"model":"leliuga/all-MiniLM-L6-v2-GGUF","input":"test"}'

# 4. Pull completion model
curl -X POST http://localhost:8000/api/pull \
  -H "Content-Type: application/json" \
  -d '{"model":"QuantFactory/Qwen2.5-3B-GGUF"}'

# 5. Test completions
curl -X POST http://localhost:8000/api/generate \
  -H "Content-Type: application/json" \
  -d '{"model":"QuantFactory/Qwen2.5-3B-GGUF","prompt":"Hello","stream":false}'
```

### Expected Startup Logs

**CPU Mode:**
```
[NativeLibraryBootstrapper] detected platform: <PLATFORM>, architecture: X64
[NativeLibraryBootstrapper] no GPU detected, selecting CPU backend
[NativeLibraryBootstrapper] found library at <path>
[NativeLibraryBootstrapper] successfully configured cpu backend
[NativeLibraryBootstrapper] library loaded successfully, 0 device(s) reported
```

**GPU Mode:**
```
[NativeLibraryBootstrapper] detected platform: <PLATFORM>, architecture: X64
[NativeLibraryBootstrapper] GPU detected, selecting CUDA backend
[NativeLibraryBootstrapper] found library at <path>
[NativeLibraryBootstrapper] successfully configured cuda backend
[NativeLibraryBootstrapper] library loaded successfully, 1 device(s) reported
[LlamaSharpEngine] CUDA backend selected, 1 GPU device(s) available
```

### Verify GPU Usage

**Monitor GPU:**
```bash
watch -n 1 nvidia-smi
```

During inference, you should see:
- GPU memory allocated
- GPU utilization >0%

---

## Troubleshooting

### Docker: Container Won't Start

**Check logs:**
```bash
docker logs sharpai
```

**Common issues:**
- Missing HuggingFace API key
- Port 8000 already in use

**Fix:**
```bash
# Change port
docker run -d -p 8080:8000 --name sharpai jchristn77/sharpai:latest

# Or stop conflicting process
sudo lsof -i :8000
kill <PID>
```

### Docker: GPU Not Detected

**Verify NVIDIA Docker:**
```bash
docker run --rm --gpus all nvidia/cuda:12.0-base nvidia-smi
```

**If fails, install NVIDIA Container Toolkit:**
```bash
curl -fsSL https://nvidia.github.io/libnvidia-container/gpgkey | sudo gpg --dearmor -o /usr/share/keyrings/nvidia-container-toolkit-keyring.gpg
curl -s -L https://nvidia.github.io/libnvidia-container/stable/deb/nvidia-container-toolkit.list | \
  sed 's#deb https://#deb [signed-by=/usr/share/keyrings/nvidia-container-toolkit-keyring.gpg] https://#g' | \
  sudo tee /etc/apt/sources.list.d/nvidia-container-toolkit.list

sudo apt-get update
sudo apt-get install -y nvidia-container-toolkit
sudo systemctl restart docker
```

### Model Download Fails

**Error: "Unauthorized"**
- Missing/invalid HuggingFace API key
- Add key to `sharpai.json`
- Restart server

**Error: "Not Found"**
- Model doesn't exist or wrong name
- Only GGUF format supported
- Search: https://huggingface.co/models?search=gguf

### Library Loading Fails (Bare-Metal)

**Windows:**
- Usually works without issues
- Ensure .NET 8 SDK installed
- Rebuild: `dotnet build SharpAI.sln`

**macOS:**
```bash
# Run diagnostics
cd bin/Debug/net8.0
./diagnose-mac.sh

# Fix dependencies
./fix-mac-dependencies.sh

# Restart
./start-mac.sh
```

**Linux:**
```bash
# Check dependencies
cd bin/Debug/net8.0/runtimes/linux-x64/native/avx2
ldd libllama.so

# Install missing libraries
sudo apt install build-essential libgomp1

# Restart
cd ../../../..
./start-linux.sh
```

### Port Already in Use

**Change in config:**
```json
{
  "Rest": {
    "Port": 8080
  }
}
```

**Or kill process:**
```bash
# Linux/Mac
lsof -i :8000
kill <PID>

# Windows
netstat -ano | findstr :8000
taskkill /PID <PID> /F
```

### Out of Memory

**Symptoms:**
- Server crashes during inference
- "Out of memory" errors

**Solutions:**

1. **Use smaller model:**
   - CPU: <3B parameters
   - GPU (8GB): <7B parameters

2. **Use more quantized model:**
   - Q4_K_M instead of Q5_K_M
   - Q3_K_M for smallest size

3. **Close other applications**

4. **Docker: Increase memory limit:**
   ```bash
   docker run --memory=8g ...
   ```

---

## Production Deployment

### Checklist

- [ ] Use Docker Compose for easy management
- [ ] Configure HuggingFace API key
- [ ] Set resource limits (RAM/VRAM)
- [ ] Enable persistent storage (volumes)
- [ ] Configure logging directory
- [ ] Set up monitoring (health check on `/`)
- [ ] Configure reverse proxy (nginx/traefik) if public
- [ ] Enable HTTPS if public
- [ ] Set up backup for models directory
- [ ] Document deployed models

### Performance Recommendations

**CPU Mode:**
- Use Q4_K_M or Q5_K_M quantized models
- Limit to models <7B parameters
- Close unnecessary applications

**GPU Mode:**
- 2-10x faster than CPU
- Ensure sufficient VRAM
- Monitor with `nvidia-smi`
- Q4_K_M works on 8GB VRAM
- Larger models need 12GB+ VRAM

### Model Recommendations by Hardware

**CPU Only (8GB RAM):**
- Embeddings: `leliuga/all-MiniLM-L6-v2-GGUF`
- Completions: `QuantFactory/Qwen2.5-3B-GGUF`

**GPU (8GB VRAM):**
- Embeddings: `leliuga/all-MiniLM-L6-v2-GGUF`
- Completions: `QuantFactory/Qwen2.5-7B-GGUF` (Q4_K_M)

**GPU (16GB+ VRAM):**
- Models up to 13B parameters (Q4/Q5 quantization)

### Monitoring

**Health check:**
```bash
curl http://localhost:8000/
```

**View logs:**
```bash
# Docker
docker logs -f sharpai

# Docker Compose
docker-compose logs -f

# Systemd
sudo journalctl -u sharpai -f
```

### Backup

**Models directory:**
```bash
tar -czf models-backup-$(date +%Y%m%d).tar.gz models/
```

**Configuration:**
```bash
cp sharpai.json sharpai.json.backup
```

---

## Getting Help

- **GitHub Issues:** https://github.com/jchristn/sharpai/issues
- **Discussions:** https://github.com/jchristn/sharpai/discussions
- **Documentation:** https://github.com/jchristn/sharpai

---

**Last Updated:** 2025-10-10
