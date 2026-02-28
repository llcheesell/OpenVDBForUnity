#ifndef __VOLUME_HDRP_INCLUDED__
#define __VOLUME_HDRP_INCLUDED__

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/LightDefinition.cs.hlsl"

#include "VolumeHDRPCamera.hlsl"
#include "VolumeHDRPUtils.hlsl"

#ifndef MAX_ITERATIONS
#define MAX_ITERATIONS 200
#endif

#define MAX_SPOT_LIGHTS 4

TEXTURE3D(_Volume);
SAMPLER(sampler_Volume);

half _Intensity;
int _MaxIterations;
half _ShadowSteps;
half _ShadowThreshold;
half3 _ShadowDensity;
float _StepDistance;

#ifdef ENABLE_AMBIENT_LIGHT
half3 _AmbientColor;
float _AmbientDensity;
#endif

float3 _MainLightDir;
float3 _MainLightColor;

float SampleVolume(float3 uv)
{
    return SAMPLE_TEXTURE3D_LOD(_Volume, sampler_Volume, uv, 0).r;
}

// Smooth distance attenuation that reaches zero at the light range
float DistanceAttenuation(float distSq, float invRangeSq)
{
    float factor = distSq * invRangeSq;
    float smoothFactor = saturate(1.0 - factor * factor);
    return smoothFactor * smoothFactor / max(distSq, 0.0001);
}

// Spot angle attenuation using HDRP's angleScale/angleOffset
float SpotAngleAttenuation(float cosAngle, float angleScale, float angleOffset)
{
    float atten = saturate(cosAngle * angleScale + angleOffset);
    return atten * atten;
}

struct Attributes
{
    float4 positionOS : POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD1;
    float4 positionNDC : TEXCOORD2;
    UNITY_VERTEX_OUTPUT_STEREO
};

struct FragOutput
{
    float4 color : SV_Target0;
#ifdef ENABLE_DEPTH_WRITE
    float depth : SV_Depth;
#endif
};

Varyings Vert(Attributes input)
{
    Varyings output;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
    output.positionCS = TransformWorldToHClip(output.positionWS);
    output.positionNDC = output.positionCS;
    return output;
}

FragOutput Frag(Varyings input)
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    int maxIter = clamp(_MaxIterations, 16, MAX_ITERATIONS);

    Ray ray;

    float3 cameraDir = GetCameraDirectionHDRP(input.positionNDC);
    ray.dir = normalize(mul((float3x3)UNITY_MATRIX_I_M, cameraDir));

    float3 rayOriginWorld = input.positionWS;
    float3 cameraPos = GetCameraPositionHDRP();

    #ifdef ENABLE_CAMERA_INSIDE_CUBE
    float3 nearCameraPos = cameraPos + (GetCameraNearClipHDRP() + 0.01) * cameraDir;
    float3 nearCameraPosLocal = LocalizePosition(nearCameraPos, UNITY_MATRIX_I_M);

    if (IsInnerCube(nearCameraPosLocal))
    {
        rayOriginWorld = nearCameraPos;
    }
    #endif

    ray.origin = LocalizePosition(rayOriginWorld, UNITY_MATRIX_I_M);

    AABB aabb;
    aabb.bmin = float3(-0.5, -0.5, -0.5);
    aabb.bmax = float3(0.5, 0.5, 0.5);

    float tfar = IntersectAABB(ray, aabb);

    // Calculate start offset for consistent sampling
    #ifdef ENABLE_SAMPLING_START_OFFSET
    float3 cameraForward = GetCameraForwardHDRP();
    float stepDist = _StepDistance / dot(cameraDir, cameraForward);

    float cameraDist = length(rayOriginWorld - cameraPos);
    float startOffset = stepDist - fmod(cameraDist, stepDist);
    float3 start = ray.origin + mul((float3x3)UNITY_MATRIX_I_M, cameraDir * startOffset);
    #else
    float stepDist = _StepDistance;
    float3 start = ray.origin;
    #endif

    float3 end = ray.origin + ray.dir * tfar;

    #ifdef ENABLE_TRACE_DISTANCE_LIMITED
    {
        uint2 pixelCoords = uint2(input.positionCS.xy);
        float rawDepth = LoadCameraDepth(pixelCoords);

        float2 screenUV = input.positionCS.xy * _ScreenSize.zw;
        float3 sceneWorldPos = ComputeWorldSpacePosition(screenUV, rawDepth, UNITY_MATRIX_I_VP);

        float3 sceneLocalPos = LocalizePosition(sceneWorldPos, UNITY_MATRIX_I_M);

        float3 toScene = sceneLocalPos - ray.origin;
        float tfar2 = dot(toScene, ray.dir);

        if (tfar2 > 0)
        {
            end = ray.origin + ray.dir * min(tfar, tfar2);
        }
    }
    #endif

    float dist = length(end - start);
    half stepCount = dist / stepDist;
    float3 ds = ray.dir * stepDist;

    // Directional light setup
    float3 lightDir;
    float3 lightColor;

    #ifdef ENABLE_HDRP_LIGHT_DATA
    if (_DirectionalLightCount > 0)
    {
        DirectionalLightData mainLight = _DirectionalLightDatas[0];
        lightDir = normalize(mul((float3x3)UNITY_MATRIX_I_M, -mainLight.forward)) * (1.0 / (float)_ShadowSteps);
        lightColor = mainLight.color;
    }
    else
    {
        lightDir = normalize(mul((float3x3)UNITY_MATRIX_I_M, _MainLightDir)) * (1.0 / (float)_ShadowSteps);
        lightColor = _MainLightColor;
    }
    #else
    lightDir = normalize(mul((float3x3)UNITY_MATRIX_I_M, _MainLightDir)) * (1.0 / (float)_ShadowSteps);
    lightColor = _MainLightColor;
    #endif

    float shadowstepsize = 1.0 / (float)_ShadowSteps;
    float3 shadowDensity = 1.0 / _ShadowDensity * shadowstepsize;
    float shadowThreshold = -log(_ShadowThreshold) / length(shadowDensity);

    // Precompute spotlight data in object space
    #ifdef ENABLE_HDRP_LIGHT_DATA
    int spotLightCount = min((int)_PunctualLightCount, MAX_SPOT_LIGHTS);

    float3 spotPosOS[MAX_SPOT_LIGHTS];
    float3 spotFwdOS[MAX_SPOT_LIGHTS];
    float3 spotColor[MAX_SPOT_LIGHTS];
    float spotInvRangeSq[MAX_SPOT_LIGHTS];
    float spotAngleScale[MAX_SPOT_LIGHTS];
    float spotAngleOffset[MAX_SPOT_LIGHTS];
    int actualSpotCount = 0;

    for (int sl = 0; sl < spotLightCount; sl++)
    {
        LightData pLight = _PunctualLightDatas[sl];
        // HDRP lightType: Spot = 1
        if (pLight.lightType == 1)
        {
            spotPosOS[actualSpotCount] = LocalizePosition(pLight.positionRWS, UNITY_MATRIX_I_M);
            spotFwdOS[actualSpotCount] = normalize(mul((float3x3)UNITY_MATRIX_I_M, pLight.forward));
            spotColor[actualSpotCount] = pLight.color;
            float range = pLight.range;
            spotInvRangeSq[actualSpotCount] = 1.0 / max(range * range, 0.0001);
            spotAngleScale[actualSpotCount] = pLight.angleScale;
            spotAngleOffset[actualSpotCount] = pLight.angleOffset;
            actualSpotCount++;
            if (actualSpotCount >= MAX_SPOT_LIGHTS) break;
        }
    }
    #endif

    float3 p = start;
    float3 depthPos = end;
    bool depthtest = true;

    float curdensity = 0.0;
    float transmittance = 1;
    float3 lightenergy = 0;

    // Object-to-world matrix for transforming sample positions
    float4x4 objToWorld = UNITY_MATRIX_M;

    [loop]
    for (int iter = 0; iter < MAX_ITERATIONS; iter++)
    {
        if (iter >= maxIter) break;

        float3 sampleUV = GetUV(p);
        float cursample = SampleVolume(sampleUV);

        if (cursample > 0.01)
        {
            float3 lpos = p;
            float shadowdist = 0;

            if (depthtest)
            {
                depthPos = p;
                depthtest = false;
            }

            #ifdef ENABLE_DIRECTIONAL_LIGHT
            [loop]
            for (int s = 0; s < _ShadowSteps; s++)
            {
                lpos += lightDir;
                float3 luv = GetUV(lpos);
                float lsample = SampleVolume(saturate(luv));

                shadowdist += lsample;

                float3 shadowboxtest = floor(0.5 + (abs(0.5 - luv)));
                float exitshadowbox = shadowboxtest.x + shadowboxtest.y + shadowboxtest.z;

                if (shadowdist > shadowThreshold || exitshadowbox >= 1)
                {
                    break;
                }
            }
            #endif

            curdensity = saturate(cursample * _Intensity);
            float3 shadowterm = exp(-shadowdist * shadowDensity);
            float3 absorbedlight = shadowterm * curdensity;
            lightenergy += absorbedlight * transmittance;

            // Spotlight contribution
            #ifdef ENABLE_HDRP_LIGHT_DATA
            for (int si = 0; si < actualSpotCount; si++)
            {
                float3 toLight = spotPosOS[si] - p;
                float distSq = dot(toLight, toLight);

                // Apply the volume's object scale to get correct world-space distance
                // The local-space distance needs to be adjusted by the object scale
                float3 toLightWS = mul((float3x3)objToWorld, toLight);
                float distSqWS = dot(toLightWS, toLightWS);

                float distAtten = DistanceAttenuation(distSqWS, spotInvRangeSq[si]);

                float3 toLightDirOS = normalize(toLight);
                float cosAngle = dot(toLightDirOS, -spotFwdOS[si]);
                float spotAtten = SpotAngleAttenuation(cosAngle, spotAngleScale[si], spotAngleOffset[si]);

                float3 spotContrib = spotColor[si] * distAtten * spotAtten * curdensity;
                lightenergy += spotContrib * transmittance;
            }
            #endif

            transmittance *= 1 - curdensity;

            #ifdef ENABLE_AMBIENT_LIGHT
            shadowdist = 0;

            float3 luv2 = sampleUV + float3(0, 0, 0.05);
            shadowdist += SampleVolume(saturate(luv2));
            luv2 = sampleUV + float3(0, 0, 0.1);
            shadowdist += SampleVolume(saturate(luv2));
            luv2 = sampleUV + float3(0, 0, 0.2);
            shadowdist += SampleVolume(saturate(luv2));
            lightenergy += exp(-shadowdist * _AmbientDensity) * curdensity * _AmbientColor * transmittance;
            #endif
        }
        p += ds;

        if (iter >= stepCount)
        {
            break;
        }

        if (transmittance < 0.01)
        {
            break;
        }
    }

    if (depthtest)
    {
        clip(-1);
    }

    FragOutput o;
    o.color = float4(lightenergy, 1 - transmittance);

#ifdef ENABLE_DEPTH_WRITE
    float3 depthWorldPos = TransformObjectToWorld(depthPos);
    float4 depthClipPos = TransformWorldToHClip(depthWorldPos);
    o.depth = depthClipPos.z / depthClipPos.w;
#endif
    return o;
}

#endif
