#ifndef __VOLUME_HDRP_SHADOW_INCLUDED__
#define __VOLUME_HDRP_SHADOW_INCLUDED__

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

#include "VolumeHDRPUtils.hlsl"

#ifndef MAX_ITERATIONS
#define MAX_ITERATIONS 200
#endif

TEXTURE3D(_Volume);
SAMPLER(sampler_Volume);

float _StepDistance;
int _MaxIterations;

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
    UNITY_VERTEX_OUTPUT_STEREO
};

Varyings Vert(Attributes input)
{
    Varyings output;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
    output.positionCS = TransformWorldToHClip(output.positionWS);
    return output;
}

void FragShadow(Varyings input, out float4 outColor : SV_Target, out float outDepth : SV_Depth)
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    int maxIter = clamp(_MaxIterations, 16, MAX_ITERATIONS);

    // Light direction: in shadow pass, the view matrix is from the light's perspective
    float3 lightDir = -UNITY_MATRIX_V[2].xyz;

    Ray ray;
    ray.origin = LocalizePosition(input.positionWS, UNITY_MATRIX_I_M);
    ray.dir = normalize(mul((float3x3)UNITY_MATRIX_I_M, lightDir));

    AABB aabb;
    aabb.bmin = float3(-0.5, -0.5, -0.5);
    aabb.bmax = float3(0.5, 0.5, 0.5);

    float tfar = IntersectAABB(ray, aabb);

    float3 start = ray.origin;
    float3 end = ray.origin + ray.dir * tfar;

    float dist = length(end - start);
    float stepCount = dist / _StepDistance;
    float3 ds = ray.dir * _StepDistance;

    float3 p = start;
    float3 hitPos = end;
    bool found = false;

    [loop]
    for (int iter = 0; iter < MAX_ITERATIONS; iter++)
    {
        if (iter >= maxIter) break;

        float3 uv = GetUV(p);
        float sample_val = SampleVolumeShadow(uv);

        if (sample_val > 0.01)
        {
            hitPos = p;
            found = true;
            break;
        }
        p += ds;

        if (iter >= stepCount)
        {
            break;
        }
    }

    // Discard if no volume data was hit - this prevents the bounding box from casting shadow
    if (!found)
    {
        clip(-1);
    }

    // Write depth at the volume hit position
    float3 hitWorldPos = TransformObjectToWorld(hitPos);
    float4 hitClipPos = TransformWorldToHClip(hitWorldPos);

    outDepth = hitClipPos.z / hitClipPos.w;
    outColor = 0;
}

#endif
