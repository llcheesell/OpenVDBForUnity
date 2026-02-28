# OpenVDBForUnity

[日本語](README_ja.md)

Unity plugin for importing and rendering [OpenVDB](http://www.openvdb.org/) volumes.

![gif](https://github.com/karasusan/OpenVDBForUnity/wiki/images/CloudSample.gif)

## Requirements

- Unity 2022.3 LTS or later
- **HDRP 14.x** (High Definition Render Pipeline) or Built-in Render Pipeline

## Supported Platforms

| Platform | Plugin | Status |
|----------|--------|--------|
| Windows x86_64 | `openvdbi.dll` | Prebuilt included |
| Linux x86_64 | `libopenvdbi.so` | Prebuilt included |
| macOS | `libopenvdbi.bundle` | Build from source |

## Quick Start

### 1. Open the project

Open the `OpenVDBForUnity/` folder in Unity 2022.3 or later. Prebuilt native plugins for Windows and Linux are included in the repository.

### 2. Import a VDB file

1. Place a `.vdb` file in the `Assets/` folder
2. Unity automatically detects the file via ScriptedImporter and creates:
   - A 3D texture sampled from the VDB volume
   - A mesh for the bounding box
   - A material using the appropriate volume rendering shader

### 3. View the result

Open `Assets/Scenes/Test.unity` or create a new scene. Drag the imported VDB asset into the scene. The volume is rendered using ray marching.

## Render Pipeline Support

### HDRP (Recommended)

The `OpenVDB/HDRP/Standard` shader is designed for HDRP 14.x and provides:

| Property | Description | Default |
|----------|-------------|---------|
| Volume | 3D texture from VDB data | - |
| Intensity | Rendering intensity | 0.3 |
| Step Distance | Ray march step size | 0.01 |
| Shadow Steps | Shadow sampling steps | 32 |
| Shadow Density | Shadow color | (0.4, 0.4, 0.4) |
| Shadow Threshold | Shadow cutoff | 0.001 |
| Ambient Color | Ambient light color | (0.4, 0.4, 0.5) |
| Ambient Density | Ambient light strength | 0.2 |
| Culling | Face culling mode | Off |
| Enable Directional Light | Use directional lighting | On |
| Enable Ambient Light | Use ambient lighting | On |
| Auto HDRP Light | Read light data from HDRP buffer | On |
| **Write Depth** | Write voxel-accurate depth via SV_Depth | On |
| **Clip Against Scene Depth** | Stop rays at opaque geometry | On |

#### Depth Options

- **Write Depth (voxel-accurate)** -- Writes the depth of the first voxel hit to the depth buffer via `SV_Depth`. Required for correct interaction with post-processing effects (Depth of Field, fog, etc.).
- **Clip Against Scene Depth** -- Reads the opaque depth buffer to stop volume ray marching at scene geometry surfaces. This prevents VDB volumes from rendering behind opaque objects like meshes and planes.

Both options can be toggled independently from the material inspector.

#### Spotlight Support

When using HDRP with `Auto HDRP Light` enabled, the volume automatically receives influence from spotlights in the scene. The shader reads HDRP's punctual light buffer and applies proper cone angle falloff and distance attenuation for natural-looking volumetric spotlight effects.

### Built-in Render Pipeline

The `OpenVDB/Standard` shader provides basic volume rendering for the Built-in Render Pipeline.

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
│           │   ├── Importer/        # Runtime C# scripts (P/Invoke, mesh, texture)
│           │   ├── HDRP/            # HDRP volume components
│           │   ├── Plugins/x86_64/  # Native plugin binaries
│           │   ├── Sequence/        # VDB sequence player
│           │   └── Timeline/        # Timeline integration
│           ├── Editor/
│           │   ├── Importer/        # ScriptedImporter for .vdb files
│           │   ├── HDRP/            # HDRP shader GUI
│           │   └── Sequence/        # Sequence editor
│           └── Shaders/
│               ├── HDRP/            # HDRP volume ray marching shaders
│               ├── VolumeStandard.shader  # Built-in RP shader
│               └── *.cginc          # Built-in RP shader includes
└── THIRD_PARTY_LICENSES.md          # Licenses for bundled native DLLs
```

## License

MIT

See [THIRD_PARTY_LICENSES.md](THIRD_PARTY_LICENSES.md) for licenses of bundled third-party libraries.
