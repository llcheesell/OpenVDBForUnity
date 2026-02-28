# Real-time Volumetric Rendering Research

## Overview

This document summarizes research into modern real-time volumetric rendering techniques
applicable to OpenVDB data in Unity. It covers data structures, rendering algorithms,
and practical GPU implementation strategies.

> **Note**: This document contains only publicly available information, summaries of
> published research, and references to open-source projects. No proprietary or
> confidential information is included.

---

## 1. Sparse Volume Data Structures for GPU

### 1.1 NanoVDB

NanoVDB is a lightweight, GPU-friendly read-only representation of OpenVDB data,
developed by NVIDIA and contributed to the OpenVDB project.

**Key Concepts:**
- Linearized tree structure that preserves VDB's hierarchical topology
- Zero-dependency header-only implementation (C99/C++/CUDA/HLSL/GLSL)
- Memory layout designed for GPU cache coherence
- Supports the same tree depth as OpenVDB (Root → Internal → Internal → Leaf)
- Each node stores its child/value table in contiguous memory

**Performance Characteristics:**
- Near-zero conversion cost from OpenVDB to NanoVDB
- Random access traversal: O(1) with 3-4 memory lookups per voxel
- Supports both dense and sparse access patterns

**References:**
- OpenVDB repository: https://github.com/AcademySoftwareFoundation/openvdb
- NanoVDB header: included in OpenVDB since v8.0
- K. Museth, "NanoVDB: A GPU-Friendly and Portable VDB Data Structure For Real-Time Rendering And Simulation", ACM SIGGRAPH 2021 Talks

### 1.2 Brick Maps / Brick Atlases

A practical alternative to full NanoVDB on platforms without compute buffer flexibility:

**Approach:**
- Divide the volume into fixed-size bricks (e.g., 8³ voxels)
- Only allocate bricks that contain non-zero data
- Pack active bricks into a 3D texture atlas
- Use an indirection texture to map volume coordinates → atlas coordinates

**Advantages:**
- Works with standard 3D texture sampling (hardware filtering)
- Compatible with all GPU APIs (no structured buffer requirement)
- Memory proportional to occupied volume, not bounding box
- Simple to implement in Unity's shader system

**Trade-offs:**
- Some wasted space at brick boundaries
- Indirection lookup adds one extra texture fetch per sample
- Brick size is a resolution/memory trade-off

### 1.3 Sparse Voxel Octrees (SVO)

**Concept:** Hierarchical octree where each node either subdivides or stores a value.
Empty subtrees are pruned entirely.

**References:**
- C. Crassin et al., "GigaVoxels: Ray-Guided Streaming for Efficient and Detailed Voxel Rendering", I3D 2009
- V. Kämpe et al., "High Resolution Sparse Voxel DAGs", ACM SIGGRAPH 2013

---

## 2. Real-time Ray Marching Techniques

### 2.1 Fundamental Algorithm

Volume ray marching steps along a ray through the volume, accumulating density
and lighting at each sample point:

```
For each pixel:
  Cast ray from camera through pixel
  For each step along the ray:
    Sample volume density at current position
    If density > 0:
      Compute lighting (shadow ray march toward light)
      Accumulate color and opacity via Beer-Lambert law
    Advance by step size
    Early-out if opacity ≈ 1
```

### 2.2 Empty Space Skipping

**Occupancy Grid:**
- Maintain a coarse 3D grid (e.g., 1/8 resolution) marking which regions contain data
- Before stepping, jump to the next occupied cell
- Reduces wasted iterations from ~90% to ~10% for typical sparse volumes

**DDA (Digital Differential Analyzer):**
- March through the occupancy grid using Amanatides & Woo's DDA algorithm
- Each DDA step skips an entire empty brick
- Only perform fine-grained sampling within occupied bricks

**References:**
- J. Amanatides and A. Woo, "A Fast Voxel Traversal Algorithm for Ray Tracing", Eurographics 1987
- Used extensively in real-time applications (EmberGen, Unreal Engine Volumetrics)

### 2.3 Adaptive Step Size

**Strategies:**
- **Distance-based**: Increase step size with distance from camera (perceptually uniform)
- **Density-based**: Use smaller steps in high-density regions, larger in low-density
- **Gradient-based**: Reduce step size near density boundaries for sharp features
- **Mipmap-based**: Use coarser LOD with larger steps at distance

### 2.4 Jittered Sampling

- Add per-pixel temporal noise (blue noise or interleaved gradient noise) to ray start offset
- Combined with TAA or temporal reprojection, eliminates banding artifacts
- Interleaved Gradient Noise: `fract(52.9829189 * fract(dot(pixel, float2(0.06711056, 0.00583715))))`

**References:**
- J. Jimenez, "Next Generation Post Processing in Call of Duty: Advanced Warfare", ACM SIGGRAPH 2014

### 2.5 Temporal Reprojection for Volumes

**Concept:**
- Store previous frame's accumulated color and transmittance
- Reproject current pixel to previous frame's screen position using motion vectors
- Blend reprojected result with current frame (e.g., 90% previous + 10% current)
- Detect disocclusion via depth/transmittance difference thresholds
- Allows reducing ray march steps per frame (e.g., 1/4 steps with 4-frame accumulation)

**References:**
- Used in Frostbite's volumetric fog system (S. Hillaire, "Physically Based and Unified Volumetric Rendering in Frostbite", SIGGRAPH 2015)

---

## 3. Lighting Models for Volumes

### 3.1 Beer-Lambert Extinction

```
Transmittance(a, b) = exp(-∫[a to b] σ_t(x) dx)
```

Discretized for ray marching:
```
transmittance *= exp(-density * stepSize * extinctionCoeff)
```

### 3.2 Henyey-Greenstein Phase Function

Models anisotropic scattering in participating media:

```
Phase(θ, g) = (1 - g²) / (4π * (1 + g² - 2g·cos(θ))^(3/2))
```

Where `g` ∈ [-1, 1] controls forward/backward scattering bias.

**References:**
- L. G. Henyey and J. L. Greenstein, "Diffuse radiation in the Galaxy", Astrophysical Journal, 1941
- Widely used in real-time volumetric rendering (clouds, fog, smoke)

### 3.3 Multi-Scattering Approximation

Full multiple scattering is prohibitively expensive. Practical approximations:
- **Powder Effect**: Darken edges via `1 - exp(-2 * density)` to approximate multi-scatter
- **Multi-octave approach**: Compute single scatter at multiple scales with decreasing extinction
- **Dual-lobe phase function**: Blend forward and backward HG lobes

**References:**
- A. Schneider, "The Real-time Volumetric Cloudscapes of Horizon Zero Dawn", SIGGRAPH 2015
- S. Hillaire, "A Scalable and Production Ready Sky and Atmosphere Rendering Technique", Eurographics 2020

---

## 4. Production Real-time Implementations

### 4.1 EmberGen (JangaFX)

EmberGen is a real-time volumetric fluid simulation and rendering tool.

**Published Technical Details:**
- GPU-native simulation and rendering pipeline
- Sparse grid representation for simulation
- Ray marching with empty space skipping
- Temporal amortization of lighting computations
- Supports export to game-ready formats (flipbooks, VDB sequences)

**Website:** https://jangafx.com/software/embergen/

### 4.2 Horizon Zero Dawn / Forbidden West Cloud System

**Published approach (GDC/SIGGRAPH presentations):**
- Ray march through atmosphere volume
- Density modeled by noise functions (Perlin-Worley, Worley)
- 64-128 steps primary ray, 6 steps shadow ray
- Temporal reprojection across 16 frames (only ~4 steps per frame)
- HG phase function with powder approximation
- LOD via ray step distance scaling

**References:**
- A. Schneider, "The Real-time Volumetric Cloudscapes of Horizon Zero Dawn", SIGGRAPH 2015
- A. Schneider and N. Tatarchuk, "Nubis: Authoring Real-Time Volumetric Cloudscapes", SIGGRAPH 2017

### 4.3 Frostbite Volumetric Fog

**Published approach:**
- Froxel (frustum voxel) grid for participating media
- Compute shader ray march through froxel grid
- Temporal reprojection with exponential history
- Physically based scattering model

**References:**
- S. Hillaire, "Physically Based and Unified Volumetric Rendering in Frostbite", SIGGRAPH 2015

### 4.4 Unity HDRP Volumetric Fog

Unity's built-in HDRP includes a volumetric fog system:
- Froxel-based density volume
- Compute shader evaluation
- Temporal filtering
- Local fog volumes with 3D textures

---

## 5. Implementation Strategy for This Project

### 5.1 Brick Map Atlas Approach (Explored, Not Adopted)

> **Status**: This approach was prototyped but not integrated into the final implementation.
> The project uses a simpler **occupancy grid** (compute-shader-generated sparse 3D RenderTexture)
> for empty space skipping via DDA traversal. The occupancy grid approach provides similar
> performance benefits with significantly lower implementation complexity.

For maximum Unity compatibility and performance:

1. **CPU-side**: Convert VDB sparse data into a brick map
   - Divide volume into 8³ bricks
   - Identify occupied bricks
   - Pack into a dense 3D atlas texture
   - Generate indirection table (maps volume space → atlas space)

2. **GPU-side**: Two-level sampling
   - First lookup: indirection texture → brick location (or empty flag)
   - Second lookup: atlas texture → actual density value
   - Empty space skipping via indirection texture (skip entire bricks)

### 5.2 Enhanced Ray Marcher

- **DDA-based brick traversal** for empty space skipping
- **Jittered ray start** with blue noise / interleaved gradient noise
- **Adaptive step count** based on distance and brick density
- **Early ray termination** at transmittance < 0.01
- **Temporal reprojection** for amortized quality

### 5.3 Compute Shader Pipeline

```
Pass 1: BrickMapBuild (Compute) - Build brick atlas from VDB data
Pass 2: RayMarch (Compute/Fragment) - Enhanced ray march with brick skipping
Pass 3: TemporalBlend (Compute) - Reproject and blend with history
Pass 4: Composite (Fragment) - Final compositing with scene
```

### 5.4 Performance Targets

| Feature | Benefit |
|---------|---------|
| Brick-map sparse storage | ~5-10x memory reduction |
| Empty space skipping | ~3-5x fewer ray steps |
| Temporal reprojection | ~4x effective quality increase |
| Adaptive stepping | ~2x fewer steps at distance |
| Jittered + TAA | Eliminates banding without more steps |

---

## 6. Key References

### Papers
1. K. Museth, "VDB: High-Resolution Sparse Volumes with Dynamic Topology", ACM TOG 32(3), 2013
2. K. Museth, "NanoVDB: A GPU-Friendly and Portable VDB Data Structure", SIGGRAPH 2021
3. C. Crassin et al., "GigaVoxels: Ray-Guided Streaming for Efficient and Detailed Voxel Rendering", I3D 2009
4. A. Schneider, "The Real-time Volumetric Cloudscapes of Horizon Zero Dawn", SIGGRAPH 2015
5. S. Hillaire, "Physically Based and Unified Volumetric Rendering in Frostbite", SIGGRAPH 2015
6. J. Amanatides and A. Woo, "A Fast Voxel Traversal Algorithm for Ray Tracing", Eurographics 1987

### Open Source Projects
- OpenVDB: https://github.com/AcademySoftwareFoundation/openvdb
- NanoVDB (part of OpenVDB): GPU-friendly VDB subset
- Unity VFX Graph: Uses sparse volume textures internally
- Blender Cycles: Open-source volume rendering implementation

### Industry Presentations
- A. Schneider, N. Tatarchuk, "Nubis: Authoring Real-Time Volumetric Cloudscapes", SIGGRAPH 2017
- S. Hillaire, "A Scalable and Production Ready Sky and Atmosphere Rendering Technique", Eurographics 2020
- B. Wronski, "Volumetric Fog and Lighting", GPU Pro 6, 2015

---

*Last updated: 2026-02-25*
