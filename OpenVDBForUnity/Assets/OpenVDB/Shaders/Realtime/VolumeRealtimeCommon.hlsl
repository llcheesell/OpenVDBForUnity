// VolumeRealtimeCommon.hlsl
// Shared utilities for real-time volumetric ray marching.
// Used by both Standard and HDRP rendering pipelines.
#ifndef __VOLUME_REALTIME_COMMON_INCLUDED__
#define __VOLUME_REALTIME_COMMON_INCLUDED__

// ============================================================================
// Configurable defines (set before including this file):
//   ENABLE_BRICK_MAP          - Use sparse brick atlas instead of dense texture
//   ENABLE_OCCUPANCY_SKIP     - Use occupancy grid for empty space skipping
//   ENABLE_TEMPORAL_JITTER    - Apply temporal jitter to ray start
//   ENABLE_ADAPTIVE_STEPPING  - Distance-based adaptive step size
//   ENABLE_HG_PHASE           - Use Henyey-Greenstein phase function
//   ENABLE_MULTI_SCATTER      - Approximate multi-scattering
//   ENABLE_COLOR_RAMP         - Density-to-color gradient mapping
//   ENABLE_SPOT_LIGHTS        - Up to 2 spot light contributions
//   MAX_STEPS                 - Maximum ray march steps (default 200)
//   SHADOW_STEPS              - Maximum shadow ray steps (default 6)
// ============================================================================

#ifndef MAX_STEPS
#define MAX_STEPS 200
#endif

#ifndef SHADOW_STEPS
#define SHADOW_STEPS 6
#endif

// ============================================================================
// Structures
// ============================================================================

struct VolumeRay
{
    float3 origin;    // Ray origin in volume local space [-0.5, 0.5]
    float3 dir;       // Ray direction in volume local space (normalized)
    float tMin;       // Entry distance
    float tMax;       // Exit distance
};

struct VolumeAABB
{
    float3 bmin;
    float3 bmax;
};

struct RayMarchResult
{
    float4 color;         // RGB + alpha (1 - transmittance)
    float3 firstHitPos;   // Position of first hit in local space
    bool hasHit;          // Whether any voxel was hit
    float transmittance;  // Final transmittance
};

struct VolumeParams
{
    float intensity;
    float stepDistance;
    float shadowSteps;
    float shadowThreshold;
    float3 shadowDensity;
    float3 ambientColor;
    float ambientDensity;
    float3 lightDir;       // In local space
    float3 lightColor;
    float phaseG;          // HG phase function anisotropy
    float temporalOffset;  // Per-pixel jitter offset
    float adaptiveDistanceScale; // How much to scale steps with distance
    float minStepDistance;  // Minimum step size
    float maxStepDistance;  // Maximum step size
    float lightInfluence;   // Multiplier for directional light contribution
    float ambientInfluence; // Multiplier for ambient light contribution
};

// ============================================================================
// Spot Light Data
// ============================================================================

#ifdef ENABLE_SPOT_LIGHTS
struct SpotLightData
{
    float3 position;   // World space position
    float3 direction;  // World space direction (forward)
    float3 color;      // Light color (intensity in params.w)
    float4 params;     // (range, angleScale, angleOffset, intensity)
};

float ComputeSpotAttenuation(float3 worldPos, SpotLightData light)
{
    float range = light.params.x;
    float angleScale = light.params.y;
    float angleOffset = light.params.z;

    float3 toLight = light.position - worldPos;
    float dist = length(toLight);
    float3 L = toLight / max(dist, 0.0001);

    // Distance attenuation (smooth quadratic falloff)
    float distNorm = saturate(dist / max(range, 0.0001));
    float distAtten = 1.0 - distNorm;
    distAtten *= distAtten;

    // Cone attenuation
    float cosAngle = dot(light.direction, -L);
    float coneAtten = saturate(cosAngle * angleScale + angleOffset);
    coneAtten *= coneAtten;

    return distAtten * coneAtten;
}
#endif

// ============================================================================
// Noise functions for jittered sampling
// ============================================================================

// Interleaved Gradient Noise (Jimenez 2014)
float InterleavedGradientNoise(float2 pixelCoord)
{
    return frac(52.9829189 * frac(dot(pixelCoord, float2(0.06711056, 0.00583715))));
}

// Temporal noise that changes per frame
float TemporalNoise(float2 pixelCoord, float frameIndex)
{
    return frac(InterleavedGradientNoise(pixelCoord) + frameIndex * 0.6180339887);
}

// ============================================================================
// AABB Intersection
// ============================================================================

inline bool IsInnerCube(float3 pos)
{
    return all(max(0.5 - abs(pos), 0.0));
}

// Returns (tNear, tFar). tNear < 0 means camera is inside.
float2 IntersectBox(VolumeRay ray, VolumeAABB aabb)
{
    float3 invDir = 1.0 / ray.dir;
    float3 t0 = (aabb.bmin - ray.origin) * invDir;
    float3 t1 = (aabb.bmax - ray.origin) * invDir;
    float3 tmin = min(t0, t1);
    float3 tmax = max(t0, t1);
    float tNear = max(max(tmin.x, tmin.y), tmin.z);
    float tFar = min(min(tmax.x, tmax.y), tmax.z);
    return float2(max(tNear, 0.0), tFar);
}

inline float3 VolumeUV(float3 localPos)
{
    return localPos + 0.5;
}

// ============================================================================
// Henyey-Greenstein Phase Function
// ============================================================================

float HenyeyGreenstein(float cosTheta, float g)
{
    float g2 = g * g;
    float denom = 1.0 + g2 - 2.0 * g * cosTheta;
    return (1.0 - g2) / (4.0 * 3.14159265 * pow(max(denom, 0.0001), 1.5));
}

// Dual-lobe phase function: blend forward and back scattering
float DualLobePhase(float cosTheta, float g)
{
    return lerp(HenyeyGreenstein(cosTheta, g), HenyeyGreenstein(cosTheta, -g * 0.5), 0.2);
}

// ============================================================================
// Multi-scattering approximation (Schneider 2015)
// ============================================================================

float PowderEffect(float density)
{
    return 1.0 - exp(-2.0 * density);
}

// ============================================================================
// Occupancy Grid DDA Traversal
// ============================================================================

#ifdef ENABLE_OCCUPANCY_SKIP

// DDA step through occupancy grid to find next occupied cell
// Returns distance to next occupied cell, or -1 if none found
float DDAFindNextOccupied(
    float3 rayOrigin, float3 rayDir,
    Texture3D<float> occupancyGrid, uint3 gridSize,
    float cellSize, float maxDist, float threshold,
    out float3 hitCellCenter)
{
    // Grid-space ray
    float3 gridOrigin = (rayOrigin + 0.5) * gridSize;
    float3 gridDir = rayDir * gridSize;

    // Current cell
    int3 cell = int3(floor(gridOrigin));
    cell = clamp(cell, int3(0, 0, 0), int3(gridSize) - 1);

    // Step direction
    int3 step = int3(sign(gridDir));
    step = max(step, int3(-1, -1, -1)); // ensure no zero step
    step = int3(
        gridDir.x >= 0 ? 1 : -1,
        gridDir.y >= 0 ? 1 : -1,
        gridDir.z >= 0 ? 1 : -1
    );

    // Distance to next cell boundary
    float3 tDelta = abs(1.0 / (gridDir + 0.00001));
    float3 boundary = float3(cell) + float3(max(step, int3(0, 0, 0)));
    float3 tMax = (boundary - gridOrigin) * float3(1.0 / (gridDir + 0.00001));
    tMax = abs(tMax);

    float traveled = 0;
    int maxIter = int(gridSize.x + gridSize.y + gridSize.z);

    for (int i = 0; i < maxIter && traveled < maxDist; i++)
    {
        // Check if current cell is occupied
        if (all(cell >= int3(0, 0, 0)) && all(cell < int3(gridSize)))
        {
            float occ = occupancyGrid[uint3(cell)];
            if (occ > threshold)
            {
                hitCellCenter = (float3(cell) + 0.5) / float3(gridSize) - 0.5;
                return traveled / float(max(gridSize.x, max(gridSize.y, gridSize.z)));
            }
        }

        // DDA step: advance to next cell
        if (tMax.x < tMax.y)
        {
            if (tMax.x < tMax.z)
            {
                cell.x += step.x;
                traveled = tMax.x;
                tMax.x += tDelta.x;
            }
            else
            {
                cell.z += step.z;
                traveled = tMax.z;
                tMax.z += tDelta.z;
            }
        }
        else
        {
            if (tMax.y < tMax.z)
            {
                cell.y += step.y;
                traveled = tMax.y;
                tMax.y += tDelta.y;
            }
            else
            {
                cell.z += step.z;
                traveled = tMax.z;
                tMax.z += tDelta.z;
            }
        }

        // Out of bounds check
        if (any(cell < int3(0, 0, 0)) || any(cell >= int3(gridSize)))
            break;
    }

    hitCellCenter = float3(0, 0, 0);
    return -1.0;
}

#endif // ENABLE_OCCUPANCY_SKIP

// ============================================================================
// Core Ray March
// ============================================================================

RayMarchResult RayMarchVolume(
    VolumeRay ray,
    VolumeParams params,
    Texture3D<float> volumeTex,
    SamplerState volumeSampler,
#ifdef ENABLE_OCCUPANCY_SKIP
    Texture3D<float> occupancyGrid,
    uint3 occupancySize,
#endif
#ifdef ENABLE_COLOR_RAMP
    Texture2D colorRampTex,
    SamplerState colorRampSampler,
    float colorRampIntensity,
#endif
#ifdef ENABLE_SPOT_LIGHTS
    SpotLightData spotLights[2],
    int spotLightCount,
#endif
    float2 pixelCoord,
    float sceneDepthDist) // distance to scene geometry along ray (for depth clipping)
{
    RayMarchResult result;
    result.color = float4(0, 0, 0, 0);
    result.firstHitPos = ray.origin;
    result.hasHit = false;
    result.transmittance = 1.0;

    VolumeAABB aabb;
    aabb.bmin = float3(-0.5, -0.5, -0.5);
    aabb.bmax = float3(0.5, 0.5, 0.5);

    float2 tHit = IntersectBox(ray, aabb);
    if (tHit.x >= tHit.y)
        return result; // miss

    float tStart = tHit.x;
    float tEnd = min(tHit.y, sceneDepthDist);

    if (tStart >= tEnd)
        return result;

    // Base step distance
    float baseStep = params.stepDistance;

    // Apply temporal jitter to reduce banding
    float jitter = 0.0;
#ifdef ENABLE_TEMPORAL_JITTER
    jitter = params.temporalOffset * baseStep;
#endif

    float t = tStart + jitter;

    // Shadow ray parameters
    float shadowStepSize = 1.0 / max(params.shadowSteps, 1.0);
    float3 lightVec = params.lightDir * shadowStepSize;
    float3 shadowExtinction = 1.0 / max(params.shadowDensity, float3(0.001, 0.001, 0.001)) * shadowStepSize;
    float shadowThresholdDist = -log(params.shadowThreshold) / max(length(shadowExtinction), 0.001);

    // Phase function
    float cosTheta = dot(normalize(ray.dir), normalize(params.lightDir));

    float3 lightenergy = float3(0, 0, 0);
    float transmittance = 1.0;

    int stepCount = 0;

    [loop]
    for (int iter = 0; iter < MAX_STEPS; iter++)
    {
        if (t >= tEnd)
            break;

        float3 pos = ray.origin + ray.dir * t;
        float3 uv = VolumeUV(pos);

        // Check bounds
        if (any(uv < 0.0) || any(uv > 1.0))
        {
            t += baseStep;
            continue;
        }

#ifdef ENABLE_OCCUPANCY_SKIP
        // Check occupancy grid first
        uint3 occCell = uint3(uv * float3(occupancySize));
        occCell = min(occCell, occupancySize - 1);
        float occ = occupancyGrid[occCell];

        if (occ < 0.001)
        {
            // Empty cell - skip ahead to next cell boundary
            float cellSize = 1.0 / float(max(occupancySize.x, max(occupancySize.y, occupancySize.z)));
            t += cellSize * 1.5; // Jump past this cell
            continue;
        }
#endif

        // Sample volume
        float density = volumeTex.SampleLevel(volumeSampler, uv, 0).r;

        if (density > 0.01)
        {
            if (!result.hasHit)
            {
                result.firstHitPos = pos;
                result.hasHit = true;
            }

            // Color ramp lookup
            float3 rampColor = float3(1, 1, 1);
            float rampAlpha = 1.0;
#ifdef ENABLE_COLOR_RAMP
            {
                float4 rampSample = colorRampTex.SampleLevel(colorRampSampler, float2(density, 0.5), 0);
                rampColor = rampSample.rgb * colorRampIntensity;
                rampAlpha = rampSample.a;
            }
#endif

            float curDensity = saturate(density * params.intensity * rampAlpha);

            // Shadow ray march
            float shadowDist = 0.0;

#ifdef ENABLE_DIRECTIONAL_LIGHT
            {
                float3 lpos = pos;

                [loop]
                for (int s = 0; s < SHADOW_STEPS; s++)
                {
                    lpos += lightVec;
                    float3 luv = VolumeUV(lpos);

                    // Check if shadow ray left the volume
                    float3 boxTest = floor(0.5 + abs(0.5 - luv));
                    if (boxTest.x + boxTest.y + boxTest.z >= 1.0)
                        break;

                    float lsample = volumeTex.SampleLevel(volumeSampler, saturate(luv), 0).r;
                    shadowDist += lsample;

                    if (shadowDist > shadowThresholdDist)
                        break;
                }
            }
#endif

            // Compute lighting
            float3 shadowTerm = exp(-shadowDist * shadowExtinction);

            // Phase function
            float phase = 1.0;
#ifdef ENABLE_HG_PHASE
            phase = DualLobePhase(cosTheta, params.phaseG);
#endif

            // Multi-scattering approximation
            float powder = 1.0;
#ifdef ENABLE_MULTI_SCATTER
            powder = PowderEffect(density);
#endif

            float3 directLight = shadowTerm * curDensity * phase * powder * params.lightColor * params.lightInfluence * rampColor;
            lightenergy += directLight * transmittance;

            // Ambient lighting
#ifdef ENABLE_AMBIENT_LIGHT
            {
                float ambientShadow = 0.0;
                float3 luv1 = uv + float3(0, 0.05, 0);
                float3 luv2 = uv + float3(0, 0.1, 0);
                float3 luv3 = uv + float3(0, 0.2, 0);
                ambientShadow += volumeTex.SampleLevel(volumeSampler, saturate(luv1), 0).r;
                ambientShadow += volumeTex.SampleLevel(volumeSampler, saturate(luv2), 0).r;
                ambientShadow += volumeTex.SampleLevel(volumeSampler, saturate(luv3), 0).r;
                lightenergy += exp(-ambientShadow * params.ambientDensity) * curDensity * params.ambientColor * params.ambientInfluence * rampColor * transmittance;
            }
#endif

            // Spot lights
#ifdef ENABLE_SPOT_LIGHTS
            if (spotLightCount > 0)
            {
                float3 sampleWS = mul(UNITY_MATRIX_M, float4(pos, 1)).xyz;

                // Spot light 0
                {
                    float atten = ComputeSpotAttenuation(sampleWS, spotLights[0]);
                    if (atten > 0.001)
                    {
                        float3 spotContrib = curDensity * spotLights[0].color * spotLights[0].params.w * atten * params.lightInfluence * rampColor;
                        lightenergy += spotContrib * transmittance;
                    }
                }

                // Spot light 1
                if (spotLightCount > 1)
                {
                    float atten = ComputeSpotAttenuation(sampleWS, spotLights[1]);
                    if (atten > 0.001)
                    {
                        float3 spotContrib = curDensity * spotLights[1].color * spotLights[1].params.w * atten * params.lightInfluence * rampColor;
                        lightenergy += spotContrib * transmittance;
                    }
                }
            }
#endif

            transmittance *= (1.0 - curDensity);

            // Early ray termination
            if (transmittance < 0.01)
                break;
        }

        // Advance ray position
        float currentStep = baseStep;
#ifdef ENABLE_ADAPTIVE_STEPPING
        // Increase step size with distance from camera
        float distFactor = 1.0 + t * params.adaptiveDistanceScale;
        currentStep = clamp(baseStep * distFactor, params.minStepDistance, params.maxStepDistance);
#endif
        t += currentStep;
        stepCount++;
    }

    result.color = float4(lightenergy, 1.0 - transmittance);
    result.transmittance = transmittance;
    return result;
}

#endif // __VOLUME_REALTIME_COMMON_INCLUDED__
