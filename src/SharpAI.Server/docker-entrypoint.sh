#!/bin/bash
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

echo ""
echo "SharpAI.Server - Linux Startup"
echo ""

ARCH="$(uname -m)"
case "$ARCH" in
    x86_64)
        RID="linux-x64"
        ;;
    aarch64|arm64)
        RID="linux-arm64"
        ;;
    *)
        echo "ERROR: Unsupported architecture: $ARCH"
        exit 1
        ;;
esac

echo "Detected architecture: $ARCH (RID: $RID)"

DOTNET_GC_SERVER="${DOTNET_GC_SERVER:-${DOTNET_GCServer:-1}}"
export DOTNET_GC_SERVER
export DOTNET_gcServer="${DOTNET_gcServer:-$DOTNET_GC_SERVER}"

CPU_VARIANT="${SHARPAI_CPU_VARIANT:-auto}"
CPU_VARIANT="$(echo "$CPU_VARIANT" | tr '[:upper:]' '[:lower:]' | tr -d '-')"

variant_supported() {
    local variant="$1"
    local flags=""

    if [ -r /proc/cpuinfo ]; then
        flags="$(grep -m 1 -i '^flags' /proc/cpuinfo || true)"
    fi

    case "$variant" in
        avx512) echo "$flags" | grep -qi 'avx512f' ;;
        avx2) echo "$flags" | grep -qi 'avx2' ;;
        avx) echo "$flags" | grep -qi ' avx ' ;;
        noavx) return 0 ;;
        *) return 1 ;;
    esac
}

variant_dir_exists() {
    local variant="$1"
    [ -d "$SCRIPT_DIR/runtimes/$RID/native/$variant" ]
}

select_cpu_variant() {
    if [ "$ARCH" != "x86_64" ]; then
        echo ""
        return 0
    fi

    if [ "$CPU_VARIANT" != "auto" ] && [ -n "$CPU_VARIANT" ]; then
        if variant_supported "$CPU_VARIANT" && variant_dir_exists "$CPU_VARIANT"; then
            echo "$CPU_VARIANT"
            return 0
        fi

        echo "WARNING: requested CPU variant '$CPU_VARIANT' is unavailable or unsupported, using automatic selection" >&2
    fi

    for variant in avx512 avx2 avx noavx; do
        if variant_supported "$variant" && variant_dir_exists "$variant"; then
            echo "$variant"
            return 0
        fi
    done

    echo ""
}

if [ "$ARCH" = "x86_64" ]; then
    SELECTED_CPU_VARIANT="$(select_cpu_variant)"
    if [ -z "$SELECTED_CPU_VARIANT" ]; then
        echo "ERROR: no compatible x64 native CPU library variant found"
        exit 1
    fi

    CPU_NATIVE_DIR="$SCRIPT_DIR/runtimes/$RID/native/$SELECTED_CPU_VARIANT"
    echo "Selected CPU native library variant: $SELECTED_CPU_VARIANT"
else
    CPU_NATIVE_DIR="$SCRIPT_DIR/runtimes/$RID/native"
fi

if [ ! -d "$CPU_NATIVE_DIR" ]; then
    echo "ERROR: native library directory not found: $CPU_NATIVE_DIR"
    exit 1
fi

if [ ! -f "$CPU_NATIVE_DIR/libllama.so" ]; then
    echo "ERROR: libllama.so not found at: $CPU_NATIVE_DIR/libllama.so"
    exit 1
fi

# Create versioned symlinks for ggml libraries if missing
# libllama.so expects libggml.so.0 and libggml-base.so.0
create_versioned_symlinks() {
    local dir="$1"
    for lib in libggml.so libggml-base.so libggml-cpu.so libggml-cuda.so; do
        if [ -f "$dir/$lib" ] && [ ! -e "$dir/${lib}.0" ]; then
            ln -sf "$lib" "$dir/${lib}.0"
        fi
    done
}

create_versioned_symlinks "$CPU_NATIVE_DIR"

CUDA_NATIVE_DIR="$SCRIPT_DIR/runtimes/$RID/native/cuda12"
LD_PATHS="$CPU_NATIVE_DIR"

if [ -d "$CUDA_NATIVE_DIR" ]; then
    LD_PATHS="$LD_PATHS:$CUDA_NATIVE_DIR"
    create_versioned_symlinks "$CUDA_NATIVE_DIR"
fi

if [ "$ARCH" = "x86_64" ]; then
    for variant in avx512 avx2 avx noavx; do
        candidate="$SCRIPT_DIR/runtimes/$RID/native/$variant"
        if [ -d "$candidate" ] && [ "$candidate" != "$CPU_NATIVE_DIR" ]; then
            LD_PATHS="$LD_PATHS:$candidate"
        fi
    done
fi

LD_PATHS="$LD_PATHS:$SCRIPT_DIR/runtimes/$RID/native"
export LD_LIBRARY_PATH="$LD_PATHS:${LD_LIBRARY_PATH}"

echo "Native library search path: $LD_PATHS"
echo "Backend override: ${SHARPAI_FORCE_BACKEND:-auto}"
echo ""

if [ -f "./SharpAI.Server" ]; then
    exec ./SharpAI.Server "$@"
else
    exec dotnet SharpAI.Server.dll "$@"
fi
