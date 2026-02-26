// VolumeRealtimeShadowCaster.hlsl
// Shadow caster pass for Realtime volume rendering.
// Uses VolumeRealtimeCommon.hlsl structures (VolumeRay, VolumeAABB, IntersectBox, VolumeUV).
#ifndef __VOLUME_REALTIME_SHADOWCASTER_INCLUDED__
#define __VOLUME_REALTIME_SHADOWCASTER_INCLUDED__

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

#include "VolumeRealtimeCommon.hlsl"

#ifndef MAX_SHADOW_MARCH_STEPS
#define MAX_SHADOW_MARCH_STEPS 100
#endif

Texture3D<float> _Volume;
SamplerState sampler_Volume;

float _StepDistance;
float _ShadowExtraBias;
float _ShadowDensityThreshold;

struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD1;
    float3 normalWS : TEXCOORD2;
    UNITY_VERTEX_OUTPUT_STEREO
};

Varyings VertShadow(Attributes input)
{
    Varyings output;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
    output.positionCS = TransformWorldToHClip(output.positionWS);
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
    return output;
}

void FragShadow(Varyings input, out float4 outColor : SV_Target, out float outDepth : SV_Depth)
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    VolumeRay ray;
    ray.origin = mul(UNITY_MATRIX_I_M, float4(input.positionWS, 1)).xyz;

    // Light direction from view matrix (shadow pass renders from light's perspective)
    float3 lightDir = -UNITY_MATRIX_V[2].xyz;
    ray.dir = normalize(mul((float3x3)UNITY_MATRIX_I_M, lightDir));

    VolumeAABB aabb;
    aabb.bmin = float3(-0.5, -0.5, -0.5);
    aabb.bmax = float3(0.5, 0.5, 0.5);

    float2 tHit = IntersectBox(ray, aabb);
    float tfar = tHit.y;

    float3 start = ray.origin;
    float3 end = ray.origin + ray.dir * tfar;

    float dist = length(end - start);
    half stepCount = dist / _StepDistance;
    float3 ds = ray.dir * _StepDistance;

    float3 p = start;
    float3 depth = end;

    [loop]
    for (int iter = 0; iter < MAX_SHADOW_MARCH_STEPS; iter++)
    {
        float3 uv = VolumeUV(p);
        float cursample = _Volume.SampleLevel(sampler_Volume, uv, 0).r;

        if (cursample > _ShadowDensityThreshold)
        {
            depth = p;
            break;
        }
        p += ds;

        if (iter >= stepCount)
        {
            clip(-1);
            break;
        }
    }

    float3 depthWS = TransformObjectToWorld(depth);
    float4 depthCS = TransformWorldToHClip(depthWS);

    // Apply shadow bias
    #if UNITY_REVERSED_Z
    depthCS.z += max(-1.0, min((_ShadowExtraBias) / depthCS.w, 0.0));
    depthCS.z = min(depthCS.z, depthCS.w * UNITY_NEAR_CLIP_VALUE);
    #else
    depthCS.z += saturate((_ShadowExtraBias) / depthCS.w);
    depthCS.z = max(depthCS.z, depthCS.w * UNITY_NEAR_CLIP_VALUE);
    #endif

    outColor = outDepth = depthCS.z / depthCS.w;
}

#endif // __VOLUME_REALTIME_SHADOWCASTER_INCLUDED__
