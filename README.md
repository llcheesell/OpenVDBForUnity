# OpenVDBForUnity

Unity plugin for importing and rendering [OpenVDB](http://www.openvdb.org/) volumes.

![gif](https://github.com/karasusan/OpenVDBForUnity/wiki/images/CloudSample.gif)

## Requirements

- Unity 6 (6000.x) or later
- **HDRP 17.x** (High Definition Render Pipeline) or Built-in Render Pipeline

## Supported Platforms

| Platform | Plugin | Status |
|----------|--------|--------|
| Windows x86_64 | `openvdbi.dll` | Prebuilt included |
| Linux x86_64 | `libopenvdbi.so` | Prebuilt included |
| macOS | `libopenvdbi.bundle` | Build from source |

## Quick Start

### 1. Open the project

Open the `OpenVDBForUnity/` folder in Unity 6 or later. Prebuilt native plugins for Windows and Linux are included in the repository.

### 2. Import a VDB file

1. Place a `.vdb` file in the `Assets/` folder
2. Unity automatically detects the file via ScriptedImporter and creates:
   - A 3D texture sampled from the VDB volume
   - A mesh for the bounding box
   - A material using the appropriate volume rendering shader

### 3. View the result

Open `Assets/Scenes/Test.unity` or create a new scene. Drag the imported VDB asset into the scene. The volume is rendered using ray marching.

## Unified Volume Component

The `OpenVDBVolume` component is the single entry point for all volume rendering. It supports switching between **Classic** and **Realtime** rendering modes from a dropdown in the Inspector.

### Render Modes

| Mode | Description | Best For |
|------|-------------|----------|
| **Realtime** (default) | Optimized ray marching with occupancy grid, adaptive stepping, temporal jitter, and HG phase function | Interactive / real-time applications |
| **Classic** | Traditional ray marching with HDRP light buffer integration | Offline / highest-fidelity rendering |

When switching modes, the component automatically applies the correct shader and shows only the relevant parameters in the Inspector.

### Shared Features (both modes)

All features are keyword-gated via `shader_feature_local` for zero GPU cost when disabled.

| Feature | Description |
|---------|-------------|
| **Directional Light** | Beer-Lambert shadow marching along light direction |
| **Ambient Light** | Configurable ambient color and density |
| **Light Influence** | Multiplier for directional and ambient light contribution |
| **Color Ramp** | Gradient-based density-to-color mapping (baked to 256x1 texture) |
| **Spot Lights** | Up to 2 Unity Spot Lights with distance and cone attenuation |
| **Shadow Casting** | Volume casts shadows onto other meshes (GPU-expensive) |
| **Auto Sync Light** | Automatically reads main directional light direction and color |

### Realtime-Only Features

| Feature | Description |
|---------|-------------|
| **Quality Presets** | Low / Medium / High / Ultra / Custom presets |
| **Empty Space Skipping** | Sparse occupancy grid for DDA traversal (compute shader) |
| **Temporal Jitter** | Per-pixel noise on ray start for TAA integration |
| **Adaptive Stepping** | Distance-based variable step size |
| **Henyey-Greenstein Phase** | Anisotropic forward/back scattering |
| **Multi-Scatter Approximation** | Powder effect for thin volumes |

### Depth Options (HDRP)

- **Write Depth** -- Writes the depth of the first voxel hit via `SV_Depth` for correct post-processing interaction.
- **Clip Against Scene Depth** -- Stops rays at opaque geometry surfaces.

## Render Pipeline Support

### HDRP (Recommended)

- **Realtime**: `OpenVDB/Realtime/HDRP` -- Full feature set including ShadowCaster pass
- **Classic**: `OpenVDB/HDRP/Standard` -- HDRP light buffer integration

### Built-in Render Pipeline

- **Realtime**: `OpenVDB/Realtime/Standard` -- Full feature set with `_LightColor0` integration (no ShadowCaster)
- **Classic**: `OpenVDB/Standard` -- Basic volume rendering

## Timeline / Sequence Playback

VDB animation sequences can be played back using:

- **OpenVDBSequencePlayer** -- Runtime component for sequential `.vdb` file playback
- **Timeline integration** -- Custom Timeline track (`OpenVDBTimelineTrack`) for playback control in Unity Timeline

## Building the Native Plugin

The native plugin wraps the OpenVDB C++ library for use in Unity via P/Invoke.

### Linux (Ubuntu/Debian)

```bash
# Install dependencies
sudo apt install cmake libopenvdb-dev libtbb-dev libimath-dev libboost-iostreams-dev

# Build
cd Plugin
mkdir build && cd build
cmake .. -DBUILD_SHARED_LIBS=ON -DCMAKE_BUILD_TYPE=Release \
  -DCMAKE_MODULE_PATH="/usr/lib/x86_64-linux-gnu/cmake/OpenVDB"
cmake --build . -j$(nproc)

# Install to Unity project
cp openvdbi/libopenvdbi.so \
  ../../OpenVDBForUnity/Assets/OpenVDB/Scripts/Plugins/x86_64/
```

### macOS

```bash
# Install dependencies
brew install openvdb imath cmake

# Build
cd Plugin
mkdir build && cd build
cmake .. -DBUILD_SHARED_LIBS=ON -DCMAKE_BUILD_TYPE=Release
cmake --build . -j$(sysctl -n hw.ncpu)

# Install to Unity project
cp openvdbi/libopenvdbi.bundle \
  ../../OpenVDBForUnity/Assets/OpenVDB/Scripts/Plugins/x86_64/
```

### Windows

```powershell
# Install dependencies via vcpkg
vcpkg install openvdb:x64-windows

# Build
cd Plugin
mkdir build && cd build
cmake .. -DBUILD_SHARED_LIBS=ON -DCMAKE_BUILD_TYPE=Release ^
  -DCMAKE_TOOLCHAIN_FILE=[vcpkg root]/scripts/buildsystems/vcpkg.cmake
cmake --build . --config Release

# Install to Unity project (DLL and dependencies)
copy openvdbi\Release\openvdbi.dll ^
  ..\..\OpenVDBForUnity\Assets\OpenVDB\Scripts\Plugins\x86_64\
```

> **Note:** On Windows, the following runtime DLLs are also required alongside `openvdbi.dll`: `openvdb.dll`, `tbb12.dll`, `blosc.dll`, `lz4.dll`, `zlib1.dll`, `zstd.dll`, `Imath-3_2.dll`. These are included in the prebuilt package.

## Project Structure

```
OpenVDBForUnity/
├── Plugin/                          # C++ native plugin source
│   ├── openvdbi/                    # OpenVDB wrapper library
│   │   ├── Importer/               # VDB loading and volume sampling
│   │   └── Foundation/             # Memory allocation, logging utilities
│   └── CMakeLists.txt              # CMake build configuration
├── OpenVDBForUnity/                 # Unity project
│   └── Assets/
│       └── OpenVDB/
│           ├── Scripts/
│           │   ├── OpenVDBVolume.cs  # Unified volume component (Classic + Realtime)
│           │   ├── Importer/        # Runtime C# scripts (P/Invoke, mesh, texture)
│           │   ├── HDRP/            # Classic HDRP volume component (deprecated)
│           │   ├── Realtime/        # Realtime volume, sequence player, LOD controller
│           │   ├── Plugins/x86_64/  # Native plugin binaries
│           │   ├── Sequence/        # VDB sequence player
│           │   └── Timeline/        # Timeline integration
│           ├── Editor/
│           │   ├── OpenVDBVolumeEditor.cs  # Unified Inspector editor
│           │   ├── Importer/        # ScriptedImporter for .vdb files
│           │   ├── HDRP/            # Classic HDRP shader GUI
│           │   ├── Realtime/        # Realtime shader GUI
│           │   └── Sequence/        # Sequence editor
│           └── Shaders/
│               ├── HDRP/            # Classic HDRP volume shaders
│               ├── Realtime/        # Realtime volume shaders (HDRP + Standard)
│               ├── VolumeStandard.shader  # Built-in RP Classic shader
│               └── *.cginc          # Built-in RP shader includes
└── THIRD_PARTY_LICENSES.md          # Licenses for bundled native DLLs
```

## Migration from Old Components

The previous `OpenVDBHDRPVolume` and `OpenVDBRealtimeVolume` components are now deprecated. Use `OpenVDBVolume` instead:

1. Remove the old component from your GameObject
2. Add `OpenVDBVolume` component
3. Select the desired **Render Mode** (Classic or Realtime)
4. Reassign your Volume Texture and configure parameters

## Changelog

### dev/feature-update (latest)

- **Unified Architecture** -- Merged Classic and Realtime renderers into a single `OpenVDBVolume` component with shared feature set and per-mode Inspector UI
- **Cross-Pipeline Spot Lights** -- Up to 2 spot lights with smooth inverse-square distance falloff and cone attenuation, working on both HDRP and Built-in RP
- **Standard RP Light Color** -- Realtime shader now reads `_LightColor0` from Unity's forward rendering pipeline instead of hardcoded white
- **HDRP Compatibility** -- Uses `UNITY_MATRIX_M` for object-to-world transforms, compatible with both HDRP and Built-in RP shader compilation
- **Keyword-Gated Features** -- All shared features use `shader_feature_local` for zero GPU cost when disabled
- **Shadow Casting** -- Volumes can cast shadows into the HDRP shadow map via a dedicated ShadowCaster pass
- **Color Ramp** -- Gradient-based density-to-color mapping baked to a 256x1 lookup texture
- **Quality Presets** -- Low / Medium / High / Ultra presets with one-click configuration
- **Empty Space Skipping** -- Compute-shader-generated occupancy grid with DDA traversal for fast sparse volume rendering

## License

MIT

See [THIRD_PARTY_LICENSES.md](THIRD_PARTY_LICENSES.md) for licenses of bundled third-party libraries.
