#ifndef __VOLUME_HDRP_INCLUDED__
#define __VOLUME_HDRP_INCLUDED__

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/LightDefinition.hlsl"

#include "VolumeHDRPCamera.hlsl"
#include "VolumeHDRPUtils.hlsl"

#ifndef ITERATIONS
#define ITERATIONS 100
#endif

TEXTURE3D(_Volume);
SAMPLER(sampler_Volume);

#ifdef ENABLE_TRACE_DISTANCE_LIMITED
TEXTURE2D(_CameraDepthTexture);
SAMPLER(sampler_CameraDepthTexture);
#endif

half _Intensity;
half _ShadowSteps;
half _ShadowThreshold;
half3 _ShadowDensity;
float _StepDistance;

#ifdef ENABLE_AMBIENT_LIGHT
half3 _AmbientColor;
float _AmbientDensity;
#endif

// Light direction and color - set from C# or read from HDRP light buffer
float3 _MainLightDir;
float3 _MainLightColor;
float _UseHDRPLightData;

float SampleVolume(float3 uv)
{
    return SAMPLE_TEXTURE3D_LOD(_Volume, sampler_Volume, uv, 0).r;
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
    float4 screenPos : TEXCOORD3;
    UNITY_VERTEX_OUTPUT_STEREO
};

struct FragOutput
{
    float4 color : SV_Target0;
    float depth : SV_Depth;
};

Varyings Vert(Attributes input)
{
    Varyings output;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
    output.positionCS = TransformWorldToHClip(output.positionWS);
    output.positionNDC = output.positionCS;
    output.screenPos = ComputeScreenPos(output.positionCS);
    return output;
}

FragOutput Frag(Varyings input)
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

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
    float2 uv = input.screenPos.xy / input.screenPos.w;
    float rawDepth = SAMPLE_TEXTURE2D_LOD(_CameraDepthTexture, sampler_CameraDepthTexture, uv, 0).r;
    float sceneDepth = LinearEyeDepth(rawDepth, _ZBufferParams);

    float tfar2 = length(ray.origin - LocalizePosition(sceneDepth * cameraDir + cameraPos, UNITY_MATRIX_I_M));
    end = ray.origin + ray.dir * min(tfar, tfar2);
    #endif

    float dist = length(end - start);
    half stepCount = dist / stepDist;
    float3 ds = ray.dir * stepDist;

    // Light direction - use HDRP directional light data or fallback
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

    float3 p = start;
    float3 depth = end;
    bool depthtest = true;

    float curdensity = 0.0;
    float transmittance = 1;
    float3 lightenergy = 0;

    [loop]
    for (int iter = 0; iter < ITERATIONS; iter++)
    {
        float3 sampleUV = GetUV(p);
        float cursample = SampleVolume(sampleUV);

        if (cursample > 0.01)
        {
            float3 lpos = p;
            float shadowdist = 0;

            if (depthtest)
            {
                depth = p;
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

    float4 depthClipPos = TransformWorldToHClip(TransformObjectToWorld(depth));
    o.depth = ComputeOutputDepth(depthClipPos);
    return o;
}

#endif
