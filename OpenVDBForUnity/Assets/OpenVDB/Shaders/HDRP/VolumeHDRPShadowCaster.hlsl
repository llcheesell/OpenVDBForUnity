#ifndef __VOLUME_HDRP_SHADOWCASTER_INCLUDED__
#define __VOLUME_HDRP_SHADOWCASTER_INCLUDED__

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

#include "VolumeHDRPCamera.hlsl"
#include "VolumeHDRPUtils.hlsl"

#ifndef ITERATIONS
#define ITERATIONS 100
#endif

TEXTURE3D(_Volume);
SAMPLER(sampler_Volume);

float _StepDistance;
float _ShadowExtraBias;

float SampleVolumeShadow(float3 uv)
{
    return SAMPLE_TEXTURE3D_LOD(_Volume, sampler_Volume, uv, 0).r;
}

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

    Ray ray;
    ray.origin = LocalizePosition(input.positionWS, UNITY_MATRIX_I_M);

    // Light direction from view matrix (shadow pass renders from light's perspective)
    float3 lightDir = -UNITY_MATRIX_V[2].xyz;
    ray.dir = normalize(mul((float3x3)UNITY_MATRIX_I_M, lightDir));

    AABB aabb;
    aabb.bmin = float3(-0.5, -0.5, -0.5);
    aabb.bmax = float3(0.5, 0.5, 0.5);

    float tfar = IntersectAABB(ray, aabb);

    float3 start = ray.origin;
    float3 end = ray.origin + ray.dir * tfar;

    float dist = length(end - start);
    half stepCount = dist / _StepDistance;
    float3 ds = ray.dir * _StepDistance;

    float3 p = start;
    float3 depth = end;

    [loop]
    for (int iter = 0; iter < ITERATIONS; iter++)
    {
        float3 uv = GetUV(p);
        float cursample = SampleVolumeShadow(uv);

        if (cursample > 0.01)
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

#endif
