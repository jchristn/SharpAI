# Apple Metal GPU Support — Implementation Plan

This document is an actionable implementation plan for adding Apple Metal (MPS) GPU acceleration to SharpAI on Apple Silicon (M1/M2/M3/M4) Macs. Each task has a checkbox for tracking progress and completion.

---

## Status (as of 2026-04-28)

Metal support is **implemented and shipping**. All code, configuration, frontend, and documentation tasks are complete. What remains is hardware-dependent verification (testing plan items in section 24) and a couple of cross-build verifications that require non-local machines.

**Code complete:**
- Backend: `NativeLibraryBootstrapper` Metal detection + `PreLoadMacOSDependencies` (`src/SharpAI.Server/Classes/Runtime/NativeLibraryBootstrapper.cs`)
- Settings: `MetalBackendPath` on `RuntimeSettings` (`src/SharpAI.Server/Classes/Settings/RuntimeSettings.cs`)
- Engine: `LlamaSharpEngine.GetOptimalGpuLayers()` treats Metal like CUDA (`src/SharpAI/Engines/LlamaSharpEngine.cs`)
- API: `/api/ps` reports `size_vram` for Metal (`src/SharpAI.Server/API/REST/Ollama/OllamaApiHandler.cs`)
- Scripts: `start-mac.sh` and `diagnose-mac.sh` report Metal readiness
- Frontend: Metal dropdown option, `MetalBackendPath` field, tooltips, type definitions
- Docs: `README.md`, `DEPLOYMENT-GUIDE.md`, `src/CLAUDE.md`, `CHANGELOG.md`, `docker/sharpai.json`

**No separate NuGet package was needed** — `LLamaSharp.Backend.Cpu` v0.27.0 already ships `libggml-metal.dylib` for `osx-arm64`. Only the CPU and CUDA12 backend packages are referenced.

**Remaining (hardware-dependent):**
- Section 2.3–2.5: runtime confirmation on Apple Silicon
- Section 24: full test matrix (auto-detect, force, fallback, single-binary cross-deploy, regression on Windows/Linux/Intel Mac)

---

## Table of Contents

1. [Background and Goals](#1-background-and-goals)
2. [Prerequisites and Research](#2-prerequisites-and-research)
3. [Backend — NuGet Package](#3-backend--nuget-package)
4. [Backend — NativeLibraryBootstrapper](#4-backend--nativelibrarybootstrapper)
5. [Backend — RuntimeSettings and Configuration](#5-backend--runtimesettings-and-configuration)
6. [Backend — NativeBackendInfo and VRAM Reporting](#6-backend--nativebackendinfo-and-vram-reporting)
7. [Backend — LlamaSharpEngine GPU Layers](#7-backend--llamasharpengine-gpu-layers)
8. [Backend — macOS Startup Script](#8-backend--macos-startup-script)
9. [Backend — macOS Diagnostic and Fix Scripts](#9-backend--macos-diagnostic-and-fix-scripts)
10. [Backend — Linux Startup Script (No-Op Verification)](#10-backend--linux-startup-script-no-op-verification)
11. [Frontend — Configuration Page](#11-frontend--configuration-page)
12. [Frontend — Tooltips](#12-frontend--tooltips)
13. [Frontend — Dashboard VRAM Display](#13-frontend--dashboard-vram-display)
14. [Frontend — Chat Settings](#14-frontend--chat-settings)
15. [Frontend — Type Definitions](#15-frontend--type-definitions)
16. [Docker — Dockerfile](#16-docker--dockerfile)
17. [Docker — Compose Files](#17-docker--compose-files)
18. [Docker — Build Scripts](#18-docker--build-scripts)
19. [Configuration — sharpai.json Template](#19-configuration--sharpaijson-template)
20. [Documentation — README.md](#20-documentation--readmemd)
21. [Documentation — DEPLOYMENT-GUIDE.md](#21-documentation--deployment-guidemd)
22. [Documentation — CLAUDE.md](#22-documentation--claudemd)
23. [Documentation — CHANGELOG.md](#23-documentation--changelogmd)
24. [Testing Plan](#24-testing-plan)
25. [Rollback Plan](#25-rollback-plan)

---

## 1. Background and Goals

### Current State
- SharpAI uses **LLamaSharp v0.25.0** with `LLamaSharp.Backend.Cpu` and `LLamaSharp.Backend.Cuda12` NuGet packages.
- Apple Silicon (OSX + ARM64) is explicitly detected in `NativeLibraryBootstrapper.cs:260-263` and forced to CPU.
- The `start-mac.sh` script already validates `libggml-metal.dylib` as a required ARM64 dependency (line 64), meaning the Metal library ships with the CPU backend but is never used for GPU acceleration.
- llama.cpp (the underlying engine) **natively supports Apple Metal** via the `ggml-metal` backend.

### Goal
Enable Metal GPU acceleration on Apple Silicon Macs so that model layers can be offloaded to the unified GPU, matching the CUDA experience on Windows/Linux. The `"metal"` backend should be auto-detected on Apple Silicon and selectable via `ForceBackend`.

### Key Constraint — Single Binary, Single Image
SharpAI produces **one compiled binary** (exe/dll) and **one Docker image** that must work correctly across all deployment scenarios:

| Scenario | Platform Detected | Auto Backend | ForceBackend=cpu | ForceBackend=cuda | ForceBackend=metal |
|----------|------------------|-------------|-----------------|------------------|-------------------|
| Bare-metal macOS Apple Silicon | `OSX + Arm64` | **Metal** | CPU | N/A (no CUDA) → CPU fallback | Metal |
| Bare-metal macOS Apple Silicon, Metal lib missing | `OSX + Arm64` | **CPU** | CPU | N/A → CPU fallback | CPU fallback + warning |
| Bare-metal macOS Intel | `OSX + x64` | **CPU** | CPU | CUDA if NVIDIA present | N/A (not Arm64) → CPU fallback |
| Bare-metal Windows x64 + NVIDIA | `Windows + x64` | **CUDA** | CPU | CUDA | N/A (not macOS) → CPU fallback |
| Bare-metal Windows x64, no GPU | `Windows + x64` | **CPU** | CPU | CPU fallback + warning | N/A → CPU fallback |
| Bare-metal Linux x64 + NVIDIA | `Linux + x64` | **CUDA** | CPU | CUDA | N/A → CPU fallback |
| Bare-metal Linux x64, no GPU | `Linux + x64` | **CPU** | CPU | CPU fallback + warning | N/A → CPU fallback |
| Bare-metal Linux ARM64 | `Linux + Arm64` | **CPU** | CPU | N/A (no CUDA libs) → CPU fallback | N/A → CPU fallback |
| Docker on Apple Silicon host | `Linux + Arm64` | **CPU** | CPU | N/A → CPU fallback | N/A → CPU fallback |
| Docker on x64 host + NVIDIA (`--gpus all`) | `Linux + x64` | **CUDA** | CPU | CUDA | N/A → CPU fallback |
| Docker on x64 host, no GPU passthrough | `Linux + x64` | **CPU** | CPU | CPU fallback + warning | N/A → CPU fallback |

The same binary self-selects the backend at **runtime** via `NativeLibraryBootstrapper.DetermineBackend()`, which checks `OSPlatform` and `Architecture`. All backend-specific libraries (CPU, CUDA, Metal) must be included **unconditionally** at build time — no platform-conditional NuGet references or build-time gates. Docker containers on Apple Silicon run Linux (not macOS), so the `OSPlatform.OSX` check structurally prevents Metal activation inside containers without any special handling.

Every change in this plan must preserve this invariant: the same artifact works everywhere, and the runtime environment determines the backend.

### LLamaSharp Native Libraries
LLamaSharp v0.25.0 bundles Metal-capable native libraries for `osx-arm64` in its CPU backend package. The `libggml-metal.dylib` and Metal shader files (`ggml-metal.metal` or `default.metallib`) are already present in the NuGet runtime directory. This means **no separate backend NuGet package is required** — the work is primarily in the bootstrapper logic, configuration, and documentation.

---

## 2. Prerequisites and Research

- [x] **2.1** Verify that `LLamaSharp.Backend.Cpu` v0.25.0 ships `libggml-metal.dylib` under `runtimes/osx-arm64/native/`. Confirm by inspecting the NuGet cache:
  ```bash
  ls ~/.nuget/packages/llamasharp.backend.cpu/0.25.0/runtimes/osx-arm64/native/
  ```
- [x] **2.2** Check whether a `default.metallib` or `ggml-metal.metal` shader file is also shipped. Metal requires compiled shaders at runtime. If missing, determine how llama.cpp locates them.
- [ ] **2.3** Verify that `NativeLibraryConfig.All.WithLibrary()` in LLamaSharp enables Metal automatically when `libggml-metal.dylib` is loadable alongside `libllama.dylib`, or whether additional configuration is needed (e.g., setting `GpuLayerCount` > 0 is sufficient). _(Hardware test — pending Apple Silicon validation)_
- [ ] **2.4** Build and run SharpAI on an Apple Silicon Mac with the current code, but manually remove the Apple Silicon CPU-force guard (lines 260-263). Observe whether Metal activates automatically and log output confirms GPU usage. _(Hardware test)_
- [ ] **2.5** Confirm the LLamaSharp `NativeApi.llama_max_devices()` return value on Apple Silicon when Metal is active (expected: 1, representing the unified GPU). _(Hardware test)_

---

## 3. Backend — NuGet Package

**File:** `src/SharpAI/SharpAI.csproj`

- [x] **3.1** Confirmed: no separate Metal NuGet package is required. The current `src/SharpAI/SharpAI.csproj` references only `LLamaSharp.Backend.Cpu` v0.27.0 and `LLamaSharp.Backend.Cuda12` v0.27.0; the CPU backend package supplies `libggml-metal.dylib` under `runtimes/osx-arm64/native/`.
- [ ] **3.2** Run `dotnet restore` and `dotnet build` on Apple Silicon, Windows, and Linux. All three must succeed with no new warnings — confirming the same .csproj builds everywhere. _(Cross-platform build verification pending)_

---

## 4. Backend — NativeLibraryBootstrapper

**File:** `src/SharpAI.Server/Classes/Runtime/NativeLibraryBootstrapper.cs`

This is the core change. The bootstrapper currently blocks Apple Silicon from GPU entirely.

### 4.1 — DetermineBackend() (lines 235-279)

- [x] **4.1.1** Replace the Apple Silicon CPU-force block (lines 259-264):
  ```csharp
  // CURRENT (remove):
  if (platform == OSPlatform.OSX && architecture == Architecture.Arm64)
  {
      logging.Debug("Apple Silicon detected, GPU backend not supported, using CPU");
      return "cpu";
  }
  ```
  With Metal detection:
  ```csharp
  // NEW:
  if (platform == OSPlatform.OSX && architecture == Architecture.Arm64)
  {
      bool metalAvailable = DetectMetalAvailability(logging);
      if (metalAvailable)
      {
          logging.Info("[NativeLibraryBootstrapper] Apple Silicon detected, selecting Metal backend");
          return "metal";
      }
      else
      {
          logging.Info("[NativeLibraryBootstrapper] Apple Silicon detected, Metal not available, using CPU");
          return "cpu";
      }
  }
  ```

- [x] **4.1.2** Ensure the environment variable and settings overrides (lines 238-252) accept `"metal"` as a valid value in addition to `"cpu"` and `"cuda"`. _(Done — `IsValidBackend()` accepts `cpu`, `cuda`, `metal`.)_

### 4.2 — New DetectMetalAvailability() Method

- [x] **4.2.1** Add a new private method `DetectMetalAvailability()`:
  ```csharp
  private static bool DetectMetalAvailability(LoggingModule logging)
  {
      try
      {
          // Check 1: Is libggml-metal.dylib present alongside libllama.dylib?
          string baseDirectory = AppContext.BaseDirectory;
          string rid = GetRuntimeIdentifier(OSPlatform.OSX);
          string nativeDir = Path.Combine(baseDirectory, "runtimes", rid, "native");
          string metalLib = Path.Combine(nativeDir, "libggml-metal.dylib");

          if (File.Exists(metalLib))
          {
              logging.Debug($"[NativeLibraryBootstrapper] Metal library detected: {metalLib}");
              return true;
          }

          // Check 2: Try custom Docker structure
          string customMetalLib = Path.Combine(baseDirectory, "runtimes", "metal", "libggml-metal.dylib");
          if (File.Exists(customMetalLib))
          {
              logging.Debug($"[NativeLibraryBootstrapper] Metal library detected at custom path: {customMetalLib}");
              return true;
          }

          logging.Debug("[NativeLibraryBootstrapper] Metal library not found");
          return false;
      }
      catch (Exception ex)
      {
          logging.Debug($"[NativeLibraryBootstrapper] Metal detection failed: {ex.Message}");
          return false;
      }
  }
  ```

### 4.3 — GetLibraryPath() (lines 371-416)

- [x] **4.3.1** Add a `"metal"` backend path resolution branch. Metal uses the same `libllama.dylib` as CPU on macOS ARM64 — the difference is that GPU layers are requested at model load time, causing llama.cpp to activate `ggml-metal`. The library path logic should resolve identically to the CPU ARM64 path:
  ```csharp
  else if (backend.Equals("metal", StringComparison.OrdinalIgnoreCase))
  {
      if (!String.IsNullOrEmpty(settings.Runtime?.MetalBackendPath))
      {
          string expandedPath = Environment.ExpandEnvironmentVariables(settings.Runtime.MetalBackendPath);
          logging.Debug($"[NativeLibraryBootstrapper] using Metal backend path from settings: {expandedPath}");
          return expandedPath;
      }
  }
  ```
- [x] **4.3.2** In the custom Docker structure fallback (line 398), ensure `"metal"` maps to the correct directory or falls through to the NuGet path. _(Done — `GetLibraryPath()` falls through to the shared `runtimes/<backend>/<libraryName>` lookup for any backend, then to the NuGet path.)_

### 4.4 — GetNuGetRuntimePath() (lines 418-481)

- [x] **4.4.1** Add a `"metal"` branch that resolves to the same `osx-arm64/native/` path as CPU ARM64. Metal activation is controlled by GPU layer count, not by a separate library:
  ```csharp
  else if (backend.Equals("metal", StringComparison.OrdinalIgnoreCase))
  {
      // Metal uses the same libllama.dylib as CPU on osx-arm64.
      // GPU offload is activated by requesting GpuLayerCount > 0 at model load time.
      string metalPath = Path.Combine(baseDirectory, "runtimes", rid, "native", libraryName);
      if (File.Exists(metalPath))
      {
          logging.Debug("[NativeLibraryBootstrapper] using Metal backend (osx-arm64 native)");
          return metalPath;
      }
  }
  ```

### 4.5 — Initialize() Fallback Logic (lines 106-155)

- [x] **4.5.1** Update the fallback logic so that if Metal backend fails to load, it falls back to CPU (same pattern as CUDA → CPU fallback). _(Done — the fallback in `Initialize()` is gated on `!backend.Equals("cpu")`, so it covers any non-CPU backend including Metal.)_

### 4.6 — PreLoadMacOSDependencies() — New Method

- [x] **4.6.1** Add a macOS-specific dependency pre-load method (analogous to `PreLoadLinuxDependencies` at lines 171-205) that pre-loads `libggml-base.dylib`, `libggml-cpu.dylib`, `libggml.dylib`, `libggml-blas.dylib`, and `libggml-metal.dylib` using `NativeLibrary.Load()` before the main library configuration. This ensures the `@loader_path` references resolve correctly.
- [x] **4.6.2** Call `PreLoadMacOSDependencies()` in `Initialize()` when platform is macOS, before `NativeLibraryConfig.All.WithLibrary()`. _(Done — invoked from both the primary load path and the CPU fallback path.)_

---

## 5. Backend — RuntimeSettings and Configuration

**File:** `src/SharpAI.Server/Classes/Settings/RuntimeSettings.cs`

- [x] **5.1** Update the `ForceBackend` XML doc comment (line 14) to include `"metal"`:
  ```
  Valid values: "cpu", "cuda", "metal", or null for auto-detection.
  ```

- [x] **5.2** Add a `MetalBackendPath` property following the same pattern as `CpuBackendPath` and `GpuBackendPath`:
  ```csharp
  /// <summary>
  /// Path to the Metal backend native library.
  /// Default: "./runtimes/metal/libllama.dylib" (macOS Apple Silicon).
  /// Supports environment variable expansion.
  /// Note: Only available on macOS Apple Silicon (ARM64).
  /// </summary>
  public string MetalBackendPath
  {
      get { return _MetalBackendPath; }
      set { _MetalBackendPath = value; }
  }
  ```
  Add corresponding backing field: `private string _MetalBackendPath = null;`

- [x] **5.3** Update the `GpuBackendPath` XML doc (line 48) to remove the note about Apple Silicon not being supported. Replace with:
  ```
  Note: On macOS Apple Silicon, use MetalBackendPath instead.
  ```

---

## 6. Backend — NativeBackendInfo and VRAM Reporting

**File:** `src/SharpAI/Classes/Runtime/NativeBackendInfo.cs`
**File:** `src/SharpAI.Server/API/REST/Ollama/OllamaApiHandler.cs`

- [x] **6.1** In `OllamaApiHandler.cs` (lines 425-427), update the `isGpuBackend` check to also recognize `"metal"`:
  ```csharp
  // CURRENT:
  bool isGpuBackend = !String.IsNullOrEmpty(selectedBackend)
      && selectedBackend.Equals("cuda", StringComparison.OrdinalIgnoreCase);

  // NEW:
  bool isGpuBackend = !String.IsNullOrEmpty(selectedBackend)
      && (selectedBackend.Equals("cuda", StringComparison.OrdinalIgnoreCase)
          || selectedBackend.Equals("metal", StringComparison.OrdinalIgnoreCase));
  ```
  This ensures `SizeVRAM` reports correctly when Metal is the active backend.

- [x] **6.2** In `Program.cs` (lines 500-504), update the `/api/ps` endpoint description to mention Metal:
  ```
  "The size_vram field reports the full model size when the CUDA or Metal backend is active
   and 0 when the CPU backend is active."
  ```

---

## 7. Backend — LlamaSharpEngine GPU Layers

**File:** `src/SharpAI/Engines/LlamaSharpEngine.cs`

- [x] **7.1** Update `GetOptimalGpuLayers()` (lines 278-302) to recognize Metal:
  ```csharp
  // CURRENT (line 285):
  if (selectedBackend.Equals("cuda", StringComparison.OrdinalIgnoreCase))

  // NEW:
  if (selectedBackend.Equals("cuda", StringComparison.OrdinalIgnoreCase)
      || selectedBackend.Equals("metal", StringComparison.OrdinalIgnoreCase))
  ```
  When Metal is active, return 999 (all layers offloaded to GPU), same as CUDA. The unified memory architecture on Apple Silicon means all layers can always be offloaded.

- [x] **7.2** Update the debug log message (line 288) to be backend-aware:
  ```csharp
  _Logging.Debug(_Header + $"{selectedBackend} backend selected, {gpuDeviceCount} GPU device(s) available");
  ```

---

## 8. Backend — macOS Startup Script

**File:** `src/SharpAI.Server/start-mac.sh`

- [x] **8.1** The script already validates `libggml-metal.dylib` in the ARM64 dependency list (line 64) and fixes `@rpath` references (line 91). Verify that no changes are needed for Metal to load correctly at runtime.

- [x] **8.2** Add a post-validation status message indicating Metal GPU availability:
  ```bash
  # After dependency verification for ARM64
  if [ -f "$NATIVE_DIR/libggml-metal.dylib" ]; then
      echo "✓ Metal GPU acceleration available"
  else
      echo "! Metal GPU acceleration not available (libggml-metal.dylib missing)"
  fi
  ```

- [ ] **8.3** If research (step 2.2) reveals that a Metal shader file (`default.metallib` or `ggml-metal.metal`) must be co-located with the native libraries, add validation for that file and copy it if needed. _(Partially addressed — `diagnose-mac.sh` checks for `ggml-metal.metal`; `start-mac.sh` does not validate the shader file.)_

---

## 9. Backend — macOS Diagnostic and Fix Scripts

**File:** `src/SharpAI.Server/diagnose-mac.sh`
**File:** `src/SharpAI.Server/fix-mac-dependencies.sh`

- [x] **9.1** In `diagnose-mac.sh`, add a diagnostic check for Metal readiness:
  - Verify `libggml-metal.dylib` is present and the correct ARM64 architecture
  - Check for the Metal shader file
  - Attempt a `dlopen()` of `libggml-metal.dylib`
  - Report "Metal GPU: Ready" or "Metal GPU: Not available" with reason

- [x] **9.2** In `fix-mac-dependencies.sh`, ensure `libggml-metal.dylib` is included in the list of libraries to locate and copy from NuGet cache if missing (it may already be — verify). _(Done — `MISSING_LIBS` array includes `libggml-metal.dylib`.)_

---

## 10. Backend — Linux Startup Script (No-Op Verification)

**File:** `src/SharpAI.Server/start-linux.sh`

- [x] **10.1** Verify that the Linux startup script is unaffected. Metal is macOS-only, so no changes should be needed. Confirm that `LD_LIBRARY_PATH` setup (line 95) does not need a `metal` subdirectory. _(Verified — `start-linux.sh` contains no Metal references.)_

---

## 11. Frontend — Configuration Page

**File:** `dashboard/src/page/configuration/ConfigurationPage.tsx`

- [x] **11.1** Update the Force Backend dropdown (lines 622-628) to add a Metal option:
  ```tsx
  <Select
    options={[
      { label: "Auto-detect", value: "auto" },
      { label: "CPU", value: "cpu" },
      { label: "CUDA (GPU)", value: "cuda" },
      { label: "Metal (Apple GPU)", value: "metal" },
    ]}
  />
  ```

- [x] **11.2** Add a "Metal Backend Path" form field after the GPU Backend Path field (after line 645):
  ```tsx
  <Form.Item
    label="Metal Backend Path"
    name={["Runtime", "MetalBackendPath"]}
    tooltip={t.runtimeMetalBackendPath}
    style={{ flex: 1, minWidth: 200 }}
  >
    <Input placeholder="Auto-detect" allowClear />
  </Form.Item>
  ```

- [ ] **11.3** Update the form value conversion logic (lines 54-82) if `MetalBackendPath` requires any special handling for null serialization. _(Not verified — needs review of ConfigurationPage.tsx form serialization.)_

---

## 12. Frontend — Tooltips

**File:** `dashboard/src/constants/tooltips.ts`

- [x] **12.1** Update `runtimeForceBackend` tooltip (line 243-244) to mention Metal:
  ```
  "Force a specific inference backend. 'Auto' picks GPU if CUDA or Metal is available, else CPU. 'CPU' disables GPU even on CUDA/Metal machines. 'CUDA' requires NVIDIA CUDA runtime. 'Metal' uses Apple Silicon GPU (macOS ARM64 only)."
  ```

- [x] **12.2** Add `runtimeMetalBackendPath` tooltip:
  ```
  runtimeMetalBackendPath: "Absolute path to the Metal native library (libllama.dylib on macOS ARM64). Leave blank to use the bundled default.",
  ```

- [x] **12.3** Update the `vram` tooltip (line 46-47) to mention Metal:
  ```
  "Video memory currently occupied by this model. Zero when SharpAI is running on CPU — the model lives entirely in system RAM instead. Set Runtime.ForceBackend to 'cuda' or 'metal' in Configuration to use GPU."
  ```

- [x] **12.4** Update `numGpu` tooltip (line 142) to mention Metal:
  ```
  "Number of model layers to offload to GPU. 0 = CPU only. Ignored if Runtime.ForceBackend is 'cpu'. Applies to both CUDA and Metal backends."
  ```

---

## 13. Frontend — Dashboard VRAM Display

**File:** `dashboard/src/page/dashboard-home/DashboardHome.tsx`

- [ ] **13.1** No code changes expected. The VRAM column (lines 254-271) renders based on `size_vram > 0`, which will automatically work once the backend reports Metal as a GPU backend (task 6.1). Verify this during integration testing. _(Hardware test pending; backend change in 6.1 is in place.)_

---

## 14. Frontend — Chat Settings

**File:** `dashboard/src/page/completion/components/ChatSettings.tsx`

- [ ] **14.1** No code changes expected. The "Number of GPUs" and "Low VRAM" settings (lines 421-501) pass through to the model options and work regardless of backend type. Verify during integration testing. _(Hardware test pending.)_

---

## 15. Frontend — Type Definitions

**File:** `dashboard/src/lib/reducer/types.ts`

- [x] **15.1** Add `MetalBackendPath` to the `RuntimeSettings` interface (lines 242-247):
  ```typescript
  export interface RuntimeSettings {
    ForceBackend: string | null;
    CpuBackendPath: string | null;
    GpuBackendPath: string | null;
    MetalBackendPath: string | null;
    EnableNativeLogging: boolean;
  }
  ```

---

## 16. Docker — Dockerfile

**File:** `src/SharpAI.Server/Dockerfile`

- [x] **16.1** Docker containers on Apple Silicon run as `linux-arm64`, not `osx-arm64`. Metal is a **macOS-only** framework and is **not available inside Linux containers**, even on Apple Silicon hosts. The `DetermineBackend()` check for `OSPlatform.OSX` will never match inside a Linux container, so Metal cannot accidentally activate. No functional Dockerfile changes are needed.

- [ ] **16.2** Verify that the Dockerfile `dotnet publish` step restores all NuGet packages (including the `osx-arm64` native libraries from the CPU backend). Since the Dockerfile builds inside a Linux container, confirm that the `osx-arm64` runtime assets are included in the published output. If `dotnet publish` strips non-Linux RIDs, this is fine — bare-metal macOS deployments are built outside Docker. Document this distinction. _(Build verification pending.)_

- [x] **16.3** Add a comment to the Dockerfile (near lines 49-60) documenting this:
  ```dockerfile
  # Note: Apple Metal GPU acceleration is only available for bare-metal macOS installs.
  # Docker containers on Apple Silicon run Linux and cannot access the Metal framework.
  # Use bare-metal deployment on macOS for GPU acceleration.
  ```

---

## 17. Docker — Compose Files

**File:** ~~`docker/compose.yaml`~~ _(removed in commit 68b4aaa "Update README and remove compose.yaml")_
**File:** `docker/compose-cpu.yaml`
**File:** `docker/compose-cuda.yaml`

- [x] **17.1** No new compose file is needed for Metal. Docker containers cannot use Metal (see 16.1). Document this limitation in the compose file headers.

- [x] **17.2** Add a comment to `compose.yaml` and `compose-cpu.yaml`: _(`compose-cpu.yaml` has the comment; `compose.yaml` is no longer in the repo.)_
  ```yaml
  # For Apple Metal GPU acceleration, run SharpAI directly on macOS (not in Docker).
  # Docker on Apple Silicon runs Linux containers which cannot access Metal.
  ```

---

## 18. Docker — Build Scripts

**File:** `docker/docker-build.sh`
**File:** `docker/docker-build.bat`

- [x] **18.1** No changes needed. Build scripts produce Linux container images. Metal is macOS bare-metal only.

- [x] **18.2** Update the post-build instructions in `docker-build.sh` to mention Metal:
  ```bash
  echo "Note: For Apple Metal GPU acceleration, run SharpAI directly on macOS (not in Docker)."
  ```

---

## 19. Configuration — sharpai.json Template

**File:** `docker/sharpai.json`

- [x] **19.1** Add `MetalBackendPath` to the `Runtime` section of the JSON template: _(Done — present in both `docker/sharpai.json` and `docker/factory/sharpai.json`.)_
  ```json
  "Runtime": {
    "ForceBackend": null,
    "CpuBackendPath": null,
    "GpuBackendPath": null,
    "MetalBackendPath": null,
    "EnableNativeLogging": false
  }
  ```

---

## 20. Documentation — README.md

**File:** `README.md`

- [x] **20.1** Update the GPU Acceleration feature line (~line 79):
  ```
  GPU Acceleration — Automatic CUDA detection (Windows/Linux) and Metal acceleration (macOS Apple Silicon)
  ```

- [x] **20.2** Update the GPU Acceleration Requirements section (~lines 468-478):
  - Add Apple Silicon Metal requirements:
    ```
    **Apple Silicon (Metal)**
    - Apple M1, M2, M3, or M4 chip
    - macOS 13 (Ventura) or later
    - Bare-metal installation (not Docker — Docker containers run Linux and cannot access Metal)
    ```
  - Remove or update the line "Apple Silicon (M1/M2/M3/M4) - GPU acceleration (Metal) is not supported, CPU mode only"

- [x] **20.3** Update the GPU Support section (~lines 508-520):
  - Move "Apple Silicon Metal" from "Not Supported" to "Supported"
  - Update platform table:
    ```
    | Platform                        | CPU | GPU          |
    | Windows x64                    | ✅  | ✅ (CUDA)    |
    | Linux x64                      | ✅  | ✅ (CUDA)    |
    | macOS Apple Silicon (ARM64)    | ✅  | ✅ (Metal)   |
    | macOS Intel (x64)              | ✅  | ❌           |
    ```

- [x] **20.4** Add a note that Docker on Apple Silicon does NOT provide Metal acceleration and bare-metal install is required for GPU.

---

## 21. Documentation — DEPLOYMENT-GUIDE.md

**File:** `DEPLOYMENT-GUIDE.md`

- [x] **21.1** Update the platform support table (~lines 73-79) to show Metal support for macOS Apple Silicon.

- [x] **21.2** Update Hardware Requirements (~lines 44-60):
  - Add a "Metal (macOS)" requirements subsection
  - Remove or update "Apple Silicon (M1/M2/M3/M4) does not support GPU acceleration" (~line 60)

- [x] **21.3** Add a new section: **macOS Apple Silicon — Metal GPU Setup**:
  - Prerequisites: macOS 13+, Apple Silicon Mac
  - Installation: bare-metal only (not Docker)
  - Verification steps: check logs for `"Metal backend selected"`, verify VRAM in dashboard
  - Forcing Metal: `"Runtime": {"ForceBackend": "metal"}` or `SHARPAI_FORCE_BACKEND=metal`

- [x] **21.4** Update the Auto-Detection Logic section (~lines 500-513) to include Metal in the detection flow diagram.

- [x] **21.5** Update the Backend Selection Priority section (~lines 495-498) to list `"metal"` as a valid value.

- [x] **21.6** Add a Docker limitation note in the Docker sections explaining that Metal is not available in containers.

---

## 22. Documentation — CLAUDE.md

**File:** `src/CLAUDE.md`

- [x] **22.1** Update the Runtime Backend Configuration section (~lines 93-98):
  ```
  - **Windows/Linux**: Auto-detects NVIDIA GPU for CUDA acceleration
  - **macOS Apple Silicon**: Auto-detects Metal GPU acceleration
  - **macOS Intel**: Uses CPU
  - Can be overridden with `"Runtime": {"ForceBackend": "cpu"}`, `"cuda"`, or `"metal"`
  ```

- [x] **22.2** Update the LLamaSharp dependency description (~line 69):
  ```
  Local model inference engine with CPU, CUDA12, and Metal backends
  ```

---

## 23. Documentation — CHANGELOG.md

**File:** `CHANGELOG.md`

- [x] **23.1** Add a changelog entry for the next version:
  ```markdown
  ## vX.Y.Z

  ### Added
  - **Apple Metal GPU acceleration** for macOS Apple Silicon (M1/M2/M3/M4)
    - Auto-detected on Apple Silicon Macs with `libggml-metal.dylib` present
    - Configurable via `ForceBackend: "metal"` or `SHARPAI_FORCE_BACKEND=metal`
    - New `MetalBackendPath` setting for custom Metal library location
    - VRAM reporting in `/api/ps` and dashboard now works for Metal backend
    - Note: Only available for bare-metal macOS installs, not Docker containers

  ### Changed
  - `ForceBackend` now accepts `"metal"` in addition to `"cpu"` and `"cuda"`
  - Updated platform support table: macOS Apple Silicon now shows GPU ✅
  ```

---

## 24. Testing Plan

> **All items in this section require physical hardware and have not been verified locally.** Code is in place; runtime confirmation is pending.

### Unit / Build Verification
- [ ] **24.1** `dotnet build SharpAI.sln` succeeds on macOS Apple Silicon, Windows, and Linux with no new warnings.
- [ ] **24.2** `dotnet build SharpAI.sln` on macOS Intel (x64) does not attempt Metal detection.

### Bare-Metal macOS Apple Silicon
- [ ] **24.3** Auto-detection: Start server with no `ForceBackend` set. Verify log shows `"Metal backend selected"` and `NativeBackendInfo.SelectedBackend == "metal"`.
- [ ] **24.4** Force Metal: Set `"ForceBackend": "metal"` in `sharpai.json`. Verify Metal activates.
- [ ] **24.5** Force CPU: Set `"ForceBackend": "cpu"`. Verify Metal is NOT used and CPU backend loads.
- [ ] **24.6** Environment override: Set `SHARPAI_FORCE_BACKEND=metal`. Verify it takes priority.
- [ ] **24.7** Load a model and call `/api/ps`. Verify `size_vram` > 0 when Metal is active.
- [ ] **24.8** Dashboard: Verify VRAM column shows a value (not "—") for running models.
- [ ] **24.9** Configuration page: Verify Metal option appears in Force Backend dropdown.
- [ ] **24.10** Run a chat completion and verify inference succeeds with reasonable performance.
- [ ] **24.11** Compare inference speed: Metal vs CPU on the same model and prompt. Metal should be significantly faster.

### Fallback Testing
- [ ] **24.12** Rename `libggml-metal.dylib` temporarily. Verify the bootstrapper falls back to CPU with a warning log.
- [ ] **24.13** Set `ForceBackend: "metal"` with Metal library missing. Verify CPU fallback works.

### Regression — macOS Intel
- [ ] **24.14** On macOS Intel (x64): Auto-detection selects CPU (not Metal, not CUDA). Metal code path is not entered because architecture is x64, not Arm64.
- [ ] **24.15** On macOS Intel: `ForceBackend: "metal"` falls back to CPU with a warning.

### Regression — Windows / Linux
- [ ] **24.16** On Windows x64 with NVIDIA GPU: CUDA auto-detection still works, unaffected by Metal changes.
- [ ] **24.17** On Windows x64 without GPU: CPU backend selected. No Metal or CUDA attempted.
- [ ] **24.18** On Linux x64 with NVIDIA GPU: CUDA auto-detection still works.
- [ ] **24.19** On Linux x64 without GPU: CPU backend selected.
- [ ] **24.20** On Linux ARM64 (e.g., Raspberry Pi, AWS Graviton): CPU backend selected. Metal detection not triggered (platform is Linux, not OSX).
- [ ] **24.21** Setting `ForceBackend: "metal"` on any Windows/Linux platform should fall back to CPU with a warning log (Metal not available on non-macOS).
- [ ] **24.22** Setting `ForceBackend: "cuda"` on a machine without NVIDIA GPU should fall back to CPU with a warning log.

### Regression — Docker (same image, all hosts)
- [ ] **24.23** `docker build` succeeds with no changes to the build process.
- [ ] **24.24** Docker container on Apple Silicon host: runs with CPU backend (not Metal). Log shows `Linux + Arm64` platform detection. Metal code path never entered.
- [ ] **24.25** Docker container on x64 host with `--gpus all`: CUDA auto-detected and used.
- [ ] **24.26** Docker container on x64 host without GPU passthrough: CPU backend selected.
- [ ] **24.27** Same Docker image used for 24.24, 24.25, and 24.26 — confirm it is literally the same image tag.

### Start Scripts
- [ ] **24.28** `start-mac.sh` on Apple Silicon: Verify Metal status message appears and server starts correctly.
- [ ] **24.29** `start-mac.sh` on Intel Mac: Verify no Metal-related messages appear.
- [ ] **24.30** `start-linux.sh` on x64: Verify no regressions.
- [ ] **24.31** `start-linux.sh` on ARM64: Verify no regressions, no Metal references in output.

### Single-Binary Verification
- [ ] **24.32** Build on Linux x64 (CI environment). Deploy the resulting binary to bare-metal macOS Apple Silicon. Confirm Metal auto-detects and activates. This is the critical single-binary test.
- [ ] **24.33** Build on macOS Apple Silicon. Deploy the resulting binary to Linux x64 Docker with `--gpus all`. Confirm CUDA auto-detects and activates.
- [ ] **24.34** Confirm the binary from 24.32 and 24.33 is byte-identical (same build output works in both environments).

---

## 25. Rollback Plan

If Metal support causes instability:

1. Revert the `DetermineBackend()` change in `NativeLibraryBootstrapper.cs` to restore the Apple Silicon → CPU force.
2. Remove the `"metal"` option from the frontend dropdown.
3. Keep the `MetalBackendPath` setting (harmless if unused) to avoid config deserialization errors.
4. Update docs to note Metal as experimental/disabled.

The rollback is low-risk because Metal shares the same `libllama.dylib` as CPU — the only difference is whether GPU layers are requested.

---

## Files Changed Summary

| Area | File | Change Type |
|------|------|-------------|
| Backend | `src/SharpAI/SharpAI.csproj` | Possibly add Metal NuGet package |
| Backend | `src/SharpAI.Server/Classes/Runtime/NativeLibraryBootstrapper.cs` | Core Metal detection and activation |
| Backend | `src/SharpAI.Server/Classes/Settings/RuntimeSettings.cs` | Add MetalBackendPath, update docs |
| Backend | `src/SharpAI/Classes/Runtime/NativeBackendInfo.cs` | No changes (accepts any string) |
| Backend | `src/SharpAI.Server/API/REST/Ollama/OllamaApiHandler.cs` | Recognize Metal as GPU backend |
| Backend | `src/SharpAI.Server/Program.cs` | Update /api/ps description |
| Backend | `src/SharpAI/Engines/LlamaSharpEngine.cs` | Recognize Metal for GPU layer offload |
| Backend | `src/SharpAI.Server/start-mac.sh` | Add Metal status output |
| Backend | `src/SharpAI.Server/diagnose-mac.sh` | Add Metal diagnostics |
| Backend | `src/SharpAI.Server/fix-mac-dependencies.sh` | Verify Metal lib in fix list |
| Frontend | `dashboard/src/page/configuration/ConfigurationPage.tsx` | Add Metal dropdown + path field |
| Frontend | `dashboard/src/constants/tooltips.ts` | Update and add Metal tooltips |
| Frontend | `dashboard/src/lib/reducer/types.ts` | Add MetalBackendPath to interface |
| Docker | `src/SharpAI.Server/Dockerfile` | Add comment only |
| Docker | `docker/compose.yaml` | Add comment only |
| Docker | `docker/compose-cpu.yaml` | Add comment only |
| Docker | `docker/docker-build.sh` | Add Metal note to post-build output |
| Config | `docker/sharpai.json` | Add MetalBackendPath field |
| Docs | `README.md` | Update platform table, GPU sections |
| Docs | `DEPLOYMENT-GUIDE.md` | Add Metal section, update tables |
| Docs | `src/CLAUDE.md` | Update backend descriptions |
| Docs | `CHANGELOG.md` | Add Metal changelog entry |
