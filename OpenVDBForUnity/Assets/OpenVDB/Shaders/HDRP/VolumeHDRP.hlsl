#ifndef __VOLUME_HDRP_INCLUDED__
#define __VOLUME_HDRP_INCLUDED__

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/LightDefinition.cs.hlsl"

#include "VolumeHDRPCamera.hlsl"
#include "VolumeHDRPUtils.hlsl"

#ifndef ITERATIONS
#define ITERATIONS 100
#endif

TEXTURE3D(_Volume);
SAMPLER(sampler_Volume);

// _CameraDepthTexture is declared in HDRP's ShaderVariables.hlsl as TEXTURE2D_X.
// Use LoadCameraDepth(uint2) or SampleCameraDepth(float2) to access it.

half _Intensity;
half _ShadowSteps;
half _ShadowThreshold;
half3 _ShadowDensity;
float _StepDistance;

// Light influence
float _LightInfluence;
float _AmbientInfluence;

#ifdef ENABLE_AMBIENT_LIGHT
half3 _AmbientColor;
float _AmbientDensity;
#endif

// Light direction and color - set from C# or read from HDRP light buffer
float3 _MainLightDir;
float3 _MainLightColor;
float _UseHDRPLightData;

// Color ramp
#ifdef ENABLE_COLOR_RAMP
TEXTURE2D(_ColorRamp);
SAMPLER(sampler_ColorRamp);
float _ColorRampIntensity;
#endif

// Spot lights (manual uniforms, max 2)
#ifdef ENABLE_SPOT_LIGHTS
float3 _SpotLight0_Position;
float3 _SpotLight0_Direction;
float3 _SpotLight0_Color;
float4 _SpotLight0_Params; // range, angleScale, angleOffset, intensity
float3 _SpotLight1_Position;
float3 _SpotLight1_Direction;
float3 _SpotLight1_Color;
float4 _SpotLight1_Params;
float _SpotLightCount;
float _SpotLightInfluence;

float ComputeSpotAttenuation(float3 worldPos, float3 lightPos, float3 lightDir, float4 params)
{
    float range = params.x;
    float angleScale = params.y;
    float angleOffset = params.z;

    float3 toLight = lightPos - worldPos;
    float dist = length(toLight);
    float3 L = toLight / max(dist, 0.0001);

    // Distance attenuation (smooth quadratic falloff)
    float distNorm = saturate(dist / max(range, 0.0001));
    float distAtten = 1.0 - distNorm;
    distAtten *= distAtten;

    // Cone attenuation
    float cosAngle = dot(lightDir, -L);
    float coneAtten = saturate(cosAngle * angleScale + angleOffset);
    coneAtten *= coneAtten;

    return distAtten * coneAtten;
}
#endif

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
        // Use HDRP's LoadCameraDepth to read the opaque depth buffer
        // input.positionCS.xy is the pixel coordinate (SV_POSITION semantic)
        uint2 pixelCoords = uint2(input.positionCS.xy);
        float rawDepth = LoadCameraDepth(pixelCoords);

        // Reconstruct world-space position of the scene geometry at this pixel
        // using HDRP's inverse view-projection matrix
        float2 screenUV = input.positionCS.xy * _ScreenSize.zw;
        float3 sceneWorldPos = ComputeWorldSpacePosition(screenUV, rawDepth, UNITY_MATRIX_I_VP);

        // Convert scene world position to volume's local space
        float3 sceneLocalPos = LocalizePosition(sceneWorldPos, UNITY_MATRIX_I_M);

        // Compute the distance from ray origin to scene geometry along the ray direction
        // Use dot product (projection onto ray) instead of raw distance to handle oblique angles
        float3 toScene = sceneLocalPos - ray.origin;
        float tfar2 = dot(toScene, ray.dir);

        // Only limit if scene geometry is in front of us (tfar2 > 0)
        if (tfar2 > 0)
        {
            end = ray.origin + ray.dir * min(tfar, tfar2);
        }
    }
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
    float3 depthPos = end;
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
                depthPos = p;
                depthtest = false;
            }

            // Color ramp lookup
            float3 rampColor = 1.0;
            float rampAlpha = 1.0;
            #ifdef ENABLE_COLOR_RAMP
            {
                float4 rampSample = SAMPLE_TEXTURE2D_LOD(_ColorRamp, sampler_ColorRamp, float2(cursample, 0.5), 0);
                rampColor = rampSample.rgb * _ColorRampIntensity;
                rampAlpha = rampSample.a;
            }
            #endif

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

            curdensity = saturate(cursample * _Intensity * rampAlpha);
            float3 shadowterm = exp(-shadowdist * shadowDensity);
            float3 absorbedlight = shadowterm * curdensity;
            lightenergy += absorbedlight * lightColor * _LightInfluence * rampColor * transmittance;
            transmittance *= 1 - curdensity;

            #ifdef ENABLE_AMBIENT_LIGHT
            shadowdist = 0;

            float3 luv2 = sampleUV + float3(0, 0, 0.05);
            shadowdist += SampleVolume(saturate(luv2));
            luv2 = sampleUV + float3(0, 0, 0.1);
            shadowdist += SampleVolume(saturate(luv2));
            luv2 = sampleUV + float3(0, 0, 0.2);
            shadowdist += SampleVolume(saturate(luv2));
            lightenergy += exp(-shadowdist * _AmbientDensity) * curdensity * _AmbientColor * _AmbientInfluence * rampColor * transmittance;
            #endif

            // Spot lights
            #ifdef ENABLE_SPOT_LIGHTS
            if (_SpotLightCount > 0)
            {
                // World position of current sample
                float3 sampleWS = TransformObjectToWorld(p);

                // Spot light 0
                {
                    float atten = ComputeSpotAttenuation(sampleWS, _SpotLight0_Position, _SpotLight0_Direction, _SpotLight0_Params);
                    if (atten > 0.001)
                    {
                        float3 spotContrib = curdensity * _SpotLight0_Color * _SpotLight0_Params.w * atten * _SpotLightInfluence * rampColor;
                        lightenergy += spotContrib * transmittance;
                    }
                }

                // Spot light 1
                if (_SpotLightCount > 1)
                {
                    float atten = ComputeSpotAttenuation(sampleWS, _SpotLight1_Position, _SpotLight1_Direction, _SpotLight1_Params);
                    if (atten > 0.001)
                    {
                        float3 spotContrib = curdensity * _SpotLight1_Color * _SpotLight1_Params.w * atten * _SpotLightInfluence * rampColor;
                        lightenergy += spotContrib * transmittance;
                    }
                }
            }
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
    // Write depth at the first voxel hit position
    float3 depthWorldPos = TransformObjectToWorld(depthPos);
    float4 depthClipPos = TransformWorldToHClip(depthWorldPos);
    o.depth = depthClipPos.z / depthClipPos.w;
#endif
    return o;
}

#endif
