Shader "OpenVDB/Realtime/HDRP"
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
        _MainLightDir ("Main Light Direction", Vector) = (0, 1, 0, 0)
        _MainLightColor ("Main Light Color", Color) = (1, 1, 1, 1)

        [Header(Ambient)]
        _AmbientColor ("Ambient Color", Color) = (0.4, 0.4, 0.5, 1)
        _AmbientDensity ("Ambient Density", Range(0, 1)) = 0.2

        [Header(Light Influence)]
        _LightInfluence ("Light Influence", Range(0, 5)) = 1.0
        _AmbientInfluence ("Ambient Influence", Range(0, 5)) = 1.0

        [Header(Adaptive Stepping)]
        _AdaptiveDistScale ("Adaptive Distance Scale", Range(0, 2)) = 0.5
        _MinStepDistance ("Min Step Distance", Range(0.001, 0.01)) = 0.003
        _MaxStepDistance ("Max Step Distance", Range(0.01, 0.1)) = 0.05

        [Header(Color Ramp)]
        [Toggle(ENABLE_COLOR_RAMP)] _EnableColorRamp("Enable Color Ramp", Float) = 0
        _ColorRamp ("Color Ramp", 2D) = "white" {}
        _ColorRampIntensity ("Color Ramp Intensity", Range(0, 2)) = 1.0

        [Header(Spot Lights)]
        [Toggle(ENABLE_SPOT_LIGHTS)] _EnableSpotLights("Enable Spot Lights", Float) = 0
        _SpotLight0_Position ("Spot Light 0 Position", Vector) = (0, 0, 0, 0)
        _SpotLight0_Direction ("Spot Light 0 Direction", Vector) = (0, -1, 0, 0)
        _SpotLight0_Color ("Spot Light 0 Color", Color) = (1, 1, 1, 1)
        _SpotLight0_Params ("Spot Light 0 Params (range, angleScale, angleOffset, intensity)", Vector) = (10, 1, 0, 1)
        _SpotLight1_Position ("Spot Light 1 Position", Vector) = (0, 0, 0, 0)
        _SpotLight1_Direction ("Spot Light 1 Direction", Vector) = (0, -1, 0, 0)
        _SpotLight1_Color ("Spot Light 1 Color", Color) = (1, 1, 1, 1)
        _SpotLight1_Params ("Spot Light 1 Params (range, angleScale, angleOffset, intensity)", Vector) = (10, 1, 0, 1)
        _SpotLightCount ("Spot Light Count", Float) = 0
        _SpotLightInfluence ("Spot Light Influence", Range(0, 5)) = 1.0

        [Header(Shadow Casting)]
        _ShadowExtraBias ("Shadow Extra Bias", Range(-0.1, 0.1)) = 0.0
        _ShadowDensityThreshold ("Shadow Density Threshold", Range(0.001, 0.1)) = 0.01

        [Header(Features)]
        [Toggle(ENABLE_OCCUPANCY_SKIP)] _EnableOccupancySkip("Empty Space Skipping", Float) = 1
        [Toggle(ENABLE_TEMPORAL_JITTER)] _EnableTemporalJitter("Temporal Jitter (TAA)", Float) = 1
        [Toggle(ENABLE_ADAPTIVE_STEPPING)] _EnableAdaptiveStepping("Adaptive Step Size", Float) = 1
        [Toggle(ENABLE_HG_PHASE)] _EnableHGPhase("Henyey-Greenstein Phase", Float) = 1
        [Toggle(ENABLE_MULTI_SCATTER)] _EnableMultiScatter("Multi-Scattering Approx", Float) = 0
        [Toggle(ENABLE_DIRECTIONAL_LIGHT)] _EnableDirectionalLight("Directional Light", Float) = 1
        [Toggle(ENABLE_AMBIENT_LIGHT)] _EnableAmbientLight("Ambient Light", Float) = 1
        [Toggle(ENABLE_HDRP_LIGHT_DATA)] _EnableHDRPLightData("Auto HDRP Light", Float) = 1
        [Toggle(ENABLE_DEPTH_WRITE)] _EnableDepthWrite("Write Depth", Float) = 1
        [Toggle(ENABLE_TRACE_DISTANCE_LIMITED)] _EnableSceneDepthClip("Clip Against Scene Depth", Float) = 1

        [KeywordEnum(Off, Front, Back)] _Cull("Culling", Int) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "HDRenderPipeline"
            "Queue" = "Transparent+0"
            "RenderType" = "Transparent"
        }

        Pass
        {
            Name "ForwardOnly"
            Tags
            {
                "LightMode" = "ForwardOnly"
            }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma only_renderers d3d11 vulkan metal playstation xboxone xboxseries switch

            #pragma vertex Vert
            #pragma fragment Frag

            #pragma shader_feature_local ENABLE_OCCUPANCY_SKIP
            #pragma shader_feature_local ENABLE_TEMPORAL_JITTER
            #pragma shader_feature_local ENABLE_ADAPTIVE_STEPPING
            #pragma shader_feature_local ENABLE_HG_PHASE
            #pragma shader_feature_local ENABLE_MULTI_SCATTER
            #pragma shader_feature_local ENABLE_DIRECTIONAL_LIGHT
            #pragma shader_feature_local ENABLE_AMBIENT_LIGHT
            #pragma shader_feature_local ENABLE_HDRP_LIGHT_DATA
            #pragma shader_feature_local ENABLE_DEPTH_WRITE
            #pragma shader_feature_local ENABLE_TRACE_DISTANCE_LIMITED
            #pragma shader_feature_local ENABLE_COLOR_RAMP
            #pragma shader_feature_local ENABLE_SPOT_LIGHTS

            #define ENABLE_CAMERA_INSIDE_CUBE
            #define ENABLE_SAMPLING_START_OFFSET

            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/LightDefinition.cs.hlsl"

            #include "VolumeRealtimeCommon.hlsl"

            // Volume textures
            Texture3D<float> _Volume;
            SamplerState sampler_Volume;

            #ifdef ENABLE_OCCUPANCY_SKIP
            Texture3D<float> _OccupancyGrid;
            uint3 _OccupancyGridSize;
            #endif

            // Color ramp
            #ifdef ENABLE_COLOR_RAMP
            Texture2D _ColorRamp;
            SamplerState sampler_ColorRamp;
            float _ColorRampIntensity;
            #endif

            // Spot lights
            #ifdef ENABLE_SPOT_LIGHTS
            float3 _SpotLight0_Position;
            float3 _SpotLight0_Direction;
            float3 _SpotLight0_Color;
            float4 _SpotLight0_Params;
            float3 _SpotLight1_Position;
            float3 _SpotLight1_Direction;
            float3 _SpotLight1_Color;
            float4 _SpotLight1_Params;
            float _SpotLightCount;
            #endif

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
            float3 _MainLightDir;
            float3 _MainLightColor;
            float _LightInfluence;
            float _AmbientInfluence;

            // Temporal
            float _FrameIndex;

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

                // Camera setup
                float3 cameraPos = GetCurrentViewPosition();
                float3 cameraForward = -UNITY_MATRIX_V[2].xyz;
                float3 camRight = UNITY_MATRIX_V[0].xyz;
                float3 camUp = UNITY_MATRIX_V[1].xyz;
                float focalLen = abs(UNITY_MATRIX_P[1][1]);

                float2 sp = input.positionNDC.xy / input.positionNDC.w;
                #if UNITY_UV_STARTS_AT_TOP
                sp.y *= -1.0;
                #endif
                sp.x *= _ScreenParams.x / _ScreenParams.y;

                float3 cameraDir = normalize(camRight * sp.x + camUp * sp.y + cameraForward * focalLen);

                // Build ray in local space
                VolumeRay ray;
                ray.dir = normalize(mul((float3x3)UNITY_MATRIX_I_M, cameraDir));

                float3 rayOriginWorld = input.positionWS;

                #ifdef ENABLE_CAMERA_INSIDE_CUBE
                float nearClip = _ProjectionParams.y;
                float3 nearCameraPos = cameraPos + (nearClip + 0.01) * cameraDir;
                float3 nearLocal = mul(UNITY_MATRIX_I_M, float4(nearCameraPos, 1)).xyz;
                if (IsInnerCube(nearLocal))
                {
                    rayOriginWorld = nearCameraPos;
                }
                #endif

                ray.origin = mul(UNITY_MATRIX_I_M, float4(rayOriginWorld, 1)).xyz;

                // Scene depth clipping
                float maxRayDist = 1000.0;
                #ifdef ENABLE_TRACE_DISTANCE_LIMITED
                {
                    uint2 pixelCoords = uint2(input.positionCS.xy);
                    float rawDepth = LoadCameraDepth(pixelCoords);
                    float2 screenUV = input.positionCS.xy * _ScreenSize.zw;
                    float3 sceneWorldPos = ComputeWorldSpacePosition(screenUV, rawDepth, UNITY_MATRIX_I_VP);
                    float3 sceneLocalPos = mul(UNITY_MATRIX_I_M, float4(sceneWorldPos, 1)).xyz;
                    float3 toScene = sceneLocalPos - ray.origin;
                    float sceneDist = dot(toScene, ray.dir);
                    if (sceneDist > 0)
                        maxRayDist = sceneDist;
                }
                #endif

                // Light direction
                float3 lightDir;
                float3 lightColor;
                #ifdef ENABLE_HDRP_LIGHT_DATA
                if (_DirectionalLightCount > 0)
                {
                    DirectionalLightData mainLight = _DirectionalLightDatas[0];
                    lightDir = normalize(mul((float3x3)UNITY_MATRIX_I_M, -mainLight.forward));
                    lightColor = mainLight.color;
                }
                else
                {
                    lightDir = normalize(mul((float3x3)UNITY_MATRIX_I_M, _MainLightDir));
                    lightColor = _MainLightColor;
                }
                #else
                lightDir = normalize(mul((float3x3)UNITY_MATRIX_I_M, _MainLightDir));
                lightColor = _MainLightColor;
                #endif

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
                params.lightColor = lightColor;
                params.phaseG = _PhaseG;
                params.adaptiveDistanceScale = _AdaptiveDistScale;
                params.minStepDistance = _MinStepDistance;
                params.maxStepDistance = _MaxStepDistance;
                params.lightInfluence = _LightInfluence;
                params.ambientInfluence = _AmbientInfluence;
                params.spotLightInfluence = _SpotLightInfluence;

                #ifdef ENABLE_TEMPORAL_JITTER
                params.temporalOffset = TemporalNoise(input.positionCS.xy, _FrameIndex);
                #else
                params.temporalOffset = 0.0;
                #endif

                // Build spot light data
                #ifdef ENABLE_SPOT_LIGHTS
                SpotLightData spotLightsArr[2];
                spotLightsArr[0].position = _SpotLight0_Position;
                spotLightsArr[0].direction = _SpotLight0_Direction;
                spotLightsArr[0].color = _SpotLight0_Color;
                spotLightsArr[0].params = _SpotLight0_Params;
                spotLightsArr[1].position = _SpotLight1_Position;
                spotLightsArr[1].direction = _SpotLight1_Direction;
                spotLightsArr[1].color = _SpotLight1_Color;
                spotLightsArr[1].params = _SpotLight1_Params;
                #endif

                // Ray march
                RayMarchResult marchResult = RayMarchVolume(
                    ray, params,
                    _Volume, sampler_Volume,
                    #ifdef ENABLE_OCCUPANCY_SKIP
                    _OccupancyGrid, _OccupancyGridSize,
                    #endif
                    #ifdef ENABLE_COLOR_RAMP
                    _ColorRamp, sampler_ColorRamp, _ColorRampIntensity,
                    #endif
                    #ifdef ENABLE_SPOT_LIGHTS
                    spotLightsArr, (int)_SpotLightCount,
                    #endif
                    input.positionCS.xy,
                    maxRayDist
                );

                if (!marchResult.hasHit)
                {
                    clip(-1);
                }

                FragOutput o;
                o.color = marchResult.color;

                #ifdef ENABLE_DEPTH_WRITE
                float3 depthWorldPos = TransformObjectToWorld(marchResult.firstHitPos);
                float4 depthClipPos = TransformWorldToHClip(depthWorldPos);
                o.depth = depthClipPos.z / depthClipPos.w;
                #endif

                return o;
            }

            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags
            {
                "LightMode" = "ShadowCaster"
            }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma only_renderers d3d11 vulkan metal playstation xboxone xboxseries switch

            #pragma vertex VertShadow
            #pragma fragment FragShadow

            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer

            #include "VolumeRealtimeShadowCaster.hlsl"
            ENDHLSL
        }
    }

    CustomEditor "OpenVDB.Editor.VolumeRealtimeHDRPShaderGUI"
}
