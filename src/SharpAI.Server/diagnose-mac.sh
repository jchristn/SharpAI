#!/bin/bash
#
# macOS ARM64 Diagnostic Script for libllama.dylib
#

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

echo "================================================"
echo "SharpAI macOS ARM64 Diagnostics"
echo "================================================"
echo ""
echo "Script directory: $SCRIPT_DIR"
echo ""

# Detect architecture
ARCH=$(uname -m)
if [ "$ARCH" = "arm64" ]; then
    LIB_PATH="$SCRIPT_DIR/runtimes/osx-arm64/native/libllama.dylib"
else
    LIB_PATH="$SCRIPT_DIR/runtimes/osx-x64/native/avx2/libllama.dylib"
fi

echo "Detected architecture: $ARCH"
echo ""

echo "1. Checking if library exists..."
if [ -f "$LIB_PATH" ]; then
    echo "   ✓ Library found: $LIB_PATH"
    ls -lh "$LIB_PATH"
else
    echo "   ✗ Library NOT found at: $LIB_PATH"
    exit 1
fi

echo ""
echo "2. Checking library architecture..."
file "$LIB_PATH"
lipo -info "$LIB_PATH"

echo ""
echo "3. Checking library dependencies..."
otool -L "$LIB_PATH"

echo ""
echo "4. Checking for missing dependencies..."
echo "   (Checking if each dependency exists on the system)"
echo ""

MISSING=0
while IFS= read -r line; do
    # Extract library path (first column after tab)
    lib=$(echo "$line" | awk '{print $1}')

    # Skip the library itself
    if [[ "$lib" == *"libllama.dylib"* ]]; then
        continue
    fi

    # Skip empty lines
    if [ -z "$lib" ]; then
        continue
    fi

    # Check if it's a system library (starts with /)
    if [[ "$lib" == /* ]]; then
        if [ -f "$lib" ]; then
            echo "   ✓ Found: $lib"
        else
            echo "   ✗ MISSING: $lib"
            MISSING=$((MISSING + 1))
        fi
    else
        # Relative path - check common locations
        found=0
        for prefix in "/usr/lib" "/usr/local/lib" "/opt/homebrew/lib"; do
            if [ -f "$prefix/$lib" ]; then
                echo "   ✓ Found: $prefix/$lib"
                found=1
                break
            fi
        done
        if [ $found -eq 0 ]; then
            echo "   ✗ MISSING: $lib (searched /usr/lib, /usr/local/lib, /opt/homebrew/lib)"
            MISSING=$((MISSING + 1))
        fi
    fi
done < <(otool -L "$LIB_PATH" | tail -n +2)

echo ""
echo "5. Checking system frameworks..."
# Check for Accelerate framework (common dependency for ML libraries)
if [ -d "/System/Library/Frameworks/Accelerate.framework" ]; then
    echo "   ✓ Accelerate framework found"
else
    echo "   ✗ Accelerate framework missing"
fi

# Check for Metal framework (GPU acceleration on Apple Silicon)
if [ -d "/System/Library/Frameworks/Metal.framework" ]; then
    echo "   ✓ Metal framework found"
else
    echo "   ✗ Metal framework missing"
fi

echo ""
echo "5b. Checking Metal GPU readiness..."
if [ "$ARCH" = "arm64" ]; then
    NATIVE_DIR="$SCRIPT_DIR/runtimes/osx-arm64/native"
    METAL_LIB="$NATIVE_DIR/libggml-metal.dylib"
    METAL_SHADER="$NATIVE_DIR/ggml-metal.metal"

    if [ -f "$METAL_LIB" ]; then
        echo "   ✓ Metal library found: $METAL_LIB"
        file "$METAL_LIB" | sed 's/^/   /'
    else
        echo "   ✗ Metal library missing: $METAL_LIB"
    fi

    if [ -f "$METAL_SHADER" ]; then
        echo "   ✓ Metal shader found: $METAL_SHADER"
    else
        echo "   ✗ Metal shader missing: $METAL_SHADER"
    fi

    if [ -f "$METAL_LIB" ] && [ -f "$METAL_SHADER" ]; then
        echo "   → Metal GPU: Ready"
    else
        echo "   → Metal GPU: Not available (missing files)"
    fi
else
    echo "   → Metal GPU: Not applicable (requires Apple Silicon ARM64)"
fi

echo ""
echo "6. Checking .NET environment..."
dotnet --info | grep -E "RID|Version|OS"

echo ""
echo "7. Attempting to load library with dlopen..."
# Create a small test program
cat > /tmp/test_load.c << 'EOF'
#include <stdio.h>
#include <dlfcn.h>

int main(int argc, char *argv[]) {
    if (argc != 2) {
        fprintf(stderr, "Usage: %s <library_path>\n", argv[0]);
        return 1;
    }

    void *handle = dlopen(argv[1], RTLD_NOW);
    if (!handle) {
        fprintf(stderr, "dlopen() failed: %s\n", dlerror());
        return 1;
    }

    printf("✓ Library loaded successfully with dlopen()\n");
    dlclose(handle);
    return 0;
}
EOF

# Compile and run
cc -o /tmp/test_load /tmp/test_load.c
/tmp/test_load "$LIB_PATH"
TEST_RESULT=$?

echo ""
echo "================================================"
echo "Summary"
echo "================================================"
if [ $MISSING -gt 0 ]; then
    echo "✗ Found $MISSING missing dependencies"
    echo ""
    echo "RECOMMENDATION: Install missing dependencies using Homebrew"
    echo "  brew install <missing-library>"
elif [ $TEST_RESULT -ne 0 ]; then
    echo "✗ Library dependencies satisfied but dlopen() failed"
    echo ""
    echo "This may indicate:"
    echo "  1. Library is for wrong architecture (check lipo -info output above)"
    echo "  2. Code signing issue (try: codesign --verify --verbose $LIB_PATH)"
    echo "  3. Library built with incompatible compiler/SDK version"
    echo ""
    echo "RECOMMENDATION: Try running under Rosetta 2 (x64 emulation):"
    echo "  arch -x86_64 dotnet run"
else
    echo "✓ All checks passed!"
    echo ""
    echo "Library loads successfully with dlopen(), but .NET may have"
    echo "additional restrictions or issues with P/Invoke on this platform."
    echo ""
    echo "RECOMMENDATION: This may be a LlamaSharp/ARM64 compatibility issue."
    echo "  1. Try running under Rosetta 2: arch -x86_64 dotnet run"
    echo "  2. Check for LlamaSharp updates"
    echo "  3. Report issue to LlamaSharp GitHub"
fi

echo "================================================"

# Cleanup
rm -f /tmp/test_load.c /tmp/test_load
