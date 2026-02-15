# OpenVDBForUnity

Unity plugin for importing and rendering [OpenVDB](http://www.openvdb.org/) volumes.

![gif](https://github.com/karasusan/OpenVDBForUnity/wiki/images/CloudSample.gif)

## Requirements

- Unity 2022.3 LTS or later
- Built-in Render Pipeline

## Quick Start

### 1. Open the project

Open the `OpenVDBForUnity/` folder in Unity 2022.3 or later. Unity will automatically migrate project settings on first launch.

### 2. Install the native plugin

The native plugin (`libopenvdbi`) is required to load `.vdb` files. A prebuilt Linux x86_64 binary is included in the repository.

For other platforms, build from source (see [Building the Native Plugin](#building-the-native-plugin) below) and place the library in:

```
OpenVDBForUnity/Assets/OpenVDB/Scripts/Plugins/x86_64/
```

| Platform | Library Name |
|----------|-------------|
| Linux | `libopenvdbi.so` |
| macOS | `libopenvdbi.bundle` |
| Windows | `openvdbi.dll` |

### 3. Import a VDB file

1. Place a `.vdb` file in the `Assets/` folder of the Unity project
2. Unity will automatically detect the file via the ScriptedImporter and create a GameObject with:
   - A 3D texture sampled from the VDB volume
   - A mesh for the bounding box
   - A material using the `OpenVDB/Standard` volume rendering shader

### 4. View the result

Open the `Assets/Scenes/Test.unity` scene or create a new scene. Drag the imported VDB asset into the scene. The volume will be rendered using ray marching.

> **Note:** The test scene may show "Missing Script" warnings from legacy HDRP components. These can be safely removed from the GameObjects.

### Shader Properties

The `OpenVDB/Standard` shader provides the following parameters:

| Property | Description | Default |
|----------|-------------|---------|
| Volume | 3D texture from VDB data | - |
| Intensity | Rendering intensity | 0.3 |
| StepDistance | Ray march step size | 0.01 |
| ShadowSteps | Shadow sampling steps | 32 |
| ShadowDensity | Shadow color | (0.4, 0.4, 0.4) |
| AmbientColor | Ambient light color | (0.4, 0.4, 0.5) |
| AmbientDensity | Ambient light strength | 0.2 |
| Culling | Face culling mode | Off |

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

# Install to Unity project
copy openvdbi\Release\openvdbi.dll ^
  ..\..\OpenVDBForUnity\Assets\OpenVDB\Scripts\Plugins\x86_64\
```

## Project Structure

```
OpenVDBForUnity/
├── Plugin/                          # C++ native plugin source
│   ├── openvdbi/                    # OpenVDB wrapper library
│   │   ├── Importer/               # VDB loading and volume sampling
│   │   └── Foundation/             # Memory allocation, logging utilities
│   └── CMakeLists.txt              # CMake build configuration
└── OpenVDBForUnity/                 # Unity project
    └── Assets/
        ├── OpenVDB/
        │   ├── Scripts/             # Runtime C# scripts (P/Invoke, mesh, texture)
        │   ├── Editor/              # ScriptedImporter for .vdb files
        │   └── Shaders/             # Volume ray marching shaders
        └── PostProcessing/          # Depth visualization post-effect (optional)
```

## License

MIT
