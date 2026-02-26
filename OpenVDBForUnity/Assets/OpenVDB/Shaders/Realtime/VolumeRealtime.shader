Shader "OpenVDB/Realtime/Standard"
{
    Properties
    {
        _Volume ("Volume", 3D) = "" {}
        _OccupancyGrid ("Occupancy Grid", 3D) = "" {}

        [Header(Quality)]
        _Intensity ("Intensity", Range(0.1, 5.0)) = 0.5
        _StepDistance ("Step Distance", Range(0.002, 0.05)) = 0.005
        _MaxSteps ("Max Steps", Range(32, 512)) = 200

        [Header(Lighting)]
        _ShadowSteps ("Shadow Steps", Range(1, 16)) = 6
        _ShadowDensity ("Shadow Density", Color) = (0.4, 0.4, 0.4, 1)
        _ShadowThreshold ("Shadow Threshold", Range(0.001, 0.1)) = 0.01
        _PhaseG ("Phase Anisotropy (HG)", Range(-0.9, 0.9)) = 0.3

        [Header(Ambient)]
        _AmbientColor ("Ambient Color", Color) = (0.4, 0.4, 0.5, 1)
        _AmbientDensity ("Ambient Density", Range(0, 1)) = 0.2

        [Header(Adaptive Stepping)]
        _AdaptiveDistScale ("Adaptive Distance Scale", Range(0, 2)) = 0.5
        _MinStepDistance ("Min Step Distance", Range(0.001, 0.01)) = 0.003
        _MaxStepDistance ("Max Step Distance", Range(0.01, 0.1)) = 0.05

        [Header(Features)]
        [Toggle(ENABLE_OCCUPANCY_SKIP)] _EnableOccupancySkip("Empty Space Skipping", Float) = 1
        [Toggle(ENABLE_TEMPORAL_JITTER)] _EnableTemporalJitter("Temporal Jitter (TAA)", Float) = 1
        [Toggle(ENABLE_ADAPTIVE_STEPPING)] _EnableAdaptiveStepping("Adaptive Step Size", Float) = 1
        [Toggle(ENABLE_HG_PHASE)] _EnableHGPhase("Henyey-Greenstein Phase", Float) = 1
        [Toggle(ENABLE_MULTI_SCATTER)] _EnableMultiScatter("Multi-Scattering Approx", Float) = 0
        [Toggle(ENABLE_DIRECTIONAL_LIGHT)] _EnableDirectionalLight("Directional Light", Float) = 1
        [Toggle(ENABLE_AMBIENT_LIGHT)] _EnableAmbientLight("Ambient Light", Float) = 1

        [KeywordEnum(Off, Front, Back)] _Cull("Culling", Int) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
        }
        Cull [_Cull]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Tags
            {
                "LightMode"="ForwardBase"
            }

            ZWrite On

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma shader_feature_local ENABLE_OCCUPANCY_SKIP
            #pragma shader_feature_local ENABLE_TEMPORAL_JITTER
            #pragma shader_feature_local ENABLE_ADAPTIVE_STEPPING
            #pragma shader_feature_local ENABLE_HG_PHASE
            #pragma shader_feature_local ENABLE_MULTI_SCATTER
            #pragma shader_feature_local ENABLE_DIRECTIONAL_LIGHT
            #pragma shader_feature_local ENABLE_AMBIENT_LIGHT

            #define ENABLE_CAMERA_INSIDE_CUBE
            #define ENABLE_SAMPLING_START_OFFSET

            #include "UnityCG.cginc"
            #include "VolumeRealtimeCommon.hlsl"

            // Volume textures
            Texture3D<float> _Volume;
            SamplerState sampler_Volume;

            #ifdef ENABLE_OCCUPANCY_SKIP
            Texture3D<float> _OccupancyGrid;
            uint3 _OccupancyGridSize;
            #endif

            sampler2D _CameraDepthTexture;

            // Material properties
            float _Intensity;
            float _StepDistance;
            int _MaxSteps;
            float _ShadowSteps;
            float _ShadowThreshold;
            float3 _ShadowDensity;
            float3 _AmbientColor;
            float _AmbientDensity;
            float _PhaseG;
            float _AdaptiveDistScale;
            float _MinStepDistance;
            float _MaxStepDistance;

            // Temporal
            float _FrameIndex;

            struct Attributes
            {
                float4 vertex : POSITION;
            };

            struct Varyings
            {
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD1;
                float4 clipPos : TEXCOORD2;
                float4 screenPos : TEXCOORD3;
            };

            struct FragOutput
            {
                float4 color : SV_Target0;
                float depth : SV_Depth;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.clipPos = o.vertex;
                o.screenPos = ComputeScreenPos(o.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            FragOutput Frag(Varyings i)
            {
                // Camera direction
                float3 cameraForward = -UNITY_MATRIX_V[2].xyz;
                float focalLen = abs(UNITY_MATRIX_P[1][1]);
                float2 sp = i.clipPos.xy / i.clipPos.w;
                #if UNITY_UV_STARTS_AT_TOP
                sp.y *= -1.0;
                #endif
                sp.x *= _ScreenParams.x / _ScreenParams.y;
                float3 camRight = UNITY_MATRIX_V[0].xyz;
                float3 camUp = UNITY_MATRIX_V[1].xyz;
                float3 cameraDir = normalize(camRight * sp.x + camUp * sp.y + cameraForward * focalLen);

                // Build ray
                VolumeRay ray;
                ray.dir = normalize(mul((float3x3)unity_WorldToObject, cameraDir));

                float3 rayOriginWorld = i.worldPos;
                float3 cameraPos = _WorldSpaceCameraPos;

                #ifdef ENABLE_CAMERA_INSIDE_CUBE
                float nearClip = _ProjectionParams.y;
                float3 nearCameraPos = cameraPos + (nearClip + 0.01) * cameraDir;
                float3 nearCameraPosLocal = mul(unity_WorldToObject, float4(nearCameraPos, 1)).xyz;
                if (IsInnerCube(nearCameraPosLocal))
                {
                    rayOriginWorld = nearCameraPos;
                }
                #endif

                ray.origin = mul(unity_WorldToObject, float4(rayOriginWorld, 1)).xyz;

                // Scene depth for distance limiting
                float2 screenUV = i.screenPos.xy / i.screenPos.w;
                float sceneDepth = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, screenUV));
                float3 sceneWorldPos = cameraPos + sceneDepth * cameraDir;
                float3 sceneLocalPos = mul(unity_WorldToObject, float4(sceneWorldPos, 1)).xyz;
                float sceneDistLocal = dot(sceneLocalPos - ray.origin, ray.dir);
                float maxRayDist = sceneDistLocal > 0 ? sceneDistLocal : 1000.0;

                // Light direction
                float3 lightDir = normalize(mul((float3x3)unity_WorldToObject, _WorldSpaceLightPos0.xyz));
                float shadowStepSize = 1.0 / max(_ShadowSteps, 1.0);

                // Build params
                VolumeParams params;
                params.intensity = _Intensity;
                params.stepDistance = _StepDistance;
                params.shadowSteps = _ShadowSteps;
                params.shadowThreshold = _ShadowThreshold;
                params.shadowDensity = _ShadowDensity;
                params.ambientColor = _AmbientColor;
                params.ambientDensity = _AmbientDensity;
                params.lightDir = lightDir * shadowStepSize;
                params.lightColor = float3(1, 1, 1); // Unity forward base light color
                params.phaseG = _PhaseG;
                params.adaptiveDistanceScale = _AdaptiveDistScale;
                params.minStepDistance = _MinStepDistance;
                params.maxStepDistance = _MaxStepDistance;

                #ifdef ENABLE_TEMPORAL_JITTER
                params.temporalOffset = TemporalNoise(i.vertex.xy, _FrameIndex);
                #else
                params.temporalOffset = 0.0;
                #endif

                // Ray march
                RayMarchResult marchResult = RayMarchVolume(
                    ray, params,
                    _Volume, sampler_Volume,
                    #ifdef ENABLE_OCCUPANCY_SKIP
                    _OccupancyGrid, _OccupancyGridSize,
                    #endif
                    i.vertex.xy,
                    maxRayDist
                );

                if (!marchResult.hasHit)
                {
                    clip(-1);
                }

                FragOutput o;
                o.color = marchResult.color;
                float4 depthClip = UnityObjectToClipPos(float4(marchResult.firstHitPos, 1.0));
                #if defined(SHADER_TARGET_GLSL) || defined(SHADER_API_GLES) || defined(SHADER_API_GLES3)
                o.depth = (depthClip.z / depthClip.w) * 0.5 + 0.5;
                #else
                o.depth = depthClip.z / depthClip.w;
                #endif
                return o;
            }

            ENDHLSL
        }
    }
}
