#!/bin/bash
#
# SharpAI.Server startup script for macOS
# Validates dependencies, fixes @rpath references, and launches the server
#

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

echo ""
echo "SharpAI.Server - macOS Startup"
echo ""

# Detect architecture
ARCH=$(uname -m)
if [ "$ARCH" = "x86_64" ]; then
    RID="osx-x64"
    AVX_VARIANT="avx2"
elif [ "$ARCH" = "arm64" ]; then
    RID="osx-arm64"
    AVX_VARIANT=""
else
    echo "ERROR: Unsupported architecture: $ARCH"
    exit 1
fi

echo "Detected architecture: $ARCH (RID: $RID)"
echo ""

# Determine native library directory
if [ "$ARCH" = "x86_64" ]; then
    NATIVE_DIR="runtimes/$RID/native/$AVX_VARIANT"
else
    NATIVE_DIR="runtimes/$RID/native"
fi

echo "Native library directory: $NATIVE_DIR"
echo ""

# Verify native library directory exists
if [ ! -d "$NATIVE_DIR" ]; then
    echo "ERROR: Native library directory not found: $NATIVE_DIR"
    echo ""
    echo "This may indicate the LLamaSharp backend package was not restored correctly."
    echo "Try running: dotnet restore"
    exit 1
fi

LIBLLAMA_PATH="$NATIVE_DIR/libllama.dylib"
if [ ! -f "$LIBLLAMA_PATH" ]; then
    echo "ERROR: libllama.dylib not found at: $LIBLLAMA_PATH"
    exit 1
fi

echo "✓ Found libllama.dylib"

# For ARM64, verify and fix dependencies
if [ "$ARCH" = "arm64" ]; then
    echo ""
    echo "Checking ARM64 dependencies..."

    REQUIRED_DEPS=("libggml-base.dylib" "libggml-cpu.dylib" "libggml.dylib" "libggml-blas.dylib" "libggml-metal.dylib")
    MISSING_DEPS=0

    for dep in "${REQUIRED_DEPS[@]}"; do
        if [ -f "$NATIVE_DIR/$dep" ]; then
            echo "✓ Found $dep"
        else
            echo "✗ MISSING: $dep"
            MISSING_DEPS=$((MISSING_DEPS + 1))
        fi
    done

    if [ $MISSING_DEPS -gt 0 ]; then
        echo ""
        echo "ERROR: Missing $MISSING_DEPS required dependencies"
        echo ""
        echo "The LLamaSharp backend package may be incomplete for macOS ARM64."
        echo "Try restoring packages: dotnet restore"
        exit 1
    fi

    echo ""
    echo "Fixing @rpath references in libllama.dylib..."

    cd "$NATIVE_DIR" || exit 1

    FIXED=0
    DEPS_TO_FIX=("libggml.dylib" "libggml-cpu.dylib" "libggml-base.dylib" "libggml-blas.dylib" "libggml-metal.dylib")

    for dep in "${DEPS_TO_FIX[@]}"; do
        # Check if this dependency is referenced with @rpath
        if otool -L libllama.dylib 2>/dev/null | grep -q "@rpath/$dep"; then
            echo "  → Fixing: @rpath/$dep → @loader_path/$dep"
            install_name_tool -change "@rpath/$dep" "@loader_path/$dep" libllama.dylib 2>/dev/null || {
                echo "  ✗ Failed to fix $dep (may already be fixed)"
            }
            FIXED=$((FIXED + 1))
        else
            echo "  ✓ Already fixed: $dep"
        fi
    done

    cd "$SCRIPT_DIR" || exit 1

    if [ $FIXED -gt 0 ]; then
        echo ""
        echo "Fixed $FIXED @rpath reference(s)"
    fi
else
    # For x64, just verify basic dependencies exist
    echo ""
    echo "Verifying x64 dependencies..."

    BASIC_DEPS=("libggml.dylib" "libggml-cpu.dylib")
    for dep in "${BASIC_DEPS[@]}"; do
        if [ -f "$NATIVE_DIR/$dep" ]; then
            echo "✓ Found $dep"
        else
            echo "! Warning: $dep not found (may not be required)"
        fi
    done
fi

echo ""

# Report Metal GPU availability for ARM64
if [ "$ARCH" = "arm64" ]; then
    if [ -f "$NATIVE_DIR/libggml-metal.dylib" ]; then
        echo "✓ Metal GPU acceleration available"
    else
        echo "! Metal GPU acceleration not available (libggml-metal.dylib missing)"
    fi
    echo ""
fi

echo "All dependencies verified successfully"
echo "Starting SharpAI.Server"
echo ""

# Launch the server
exec ./SharpAI.Server "$@"
