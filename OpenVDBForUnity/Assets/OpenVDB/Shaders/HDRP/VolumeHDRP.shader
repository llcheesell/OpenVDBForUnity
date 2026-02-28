Shader "OpenVDB/HDRP/Standard"
{
    Properties
    {
        _Volume ("Volume", 3D) = "" {}
        _Intensity ("Intensity", Range(0.1, 2.0)) = 0.3
        _StepDistance ("Step Distance", Range(0.005, 0.1)) = 0.01
        _ShadowSteps ("Shadow Steps", Range(1, 64)) = 32
        _ShadowDensity ("Shadow Density", Color) = (0.4, 0.4, 0.4, 1)
        _ShadowThreshold ("Shadow Threshold", Range(0.001, 0.1)) = 0.001
        _AmbientColor ("Ambient Color", Color) = (0.4, 0.4, 0.5, 1)
        _AmbientDensity ("Ambient Density", Range(0, 1)) = 0.2
        _MainLightDir ("Main Light Direction", Vector) = (0, 1, 0, 0)
        _MainLightColor ("Main Light Color", Color) = (1, 1, 1, 1)
        [KeywordEnum(Off, Front, Back)] _Cull("Culling", Int) = 0
        [Toggle(ENABLE_DIRECTIONAL_LIGHT)] _EnableDirectionalLight("Enable Directional Light", Float) = 1
        [Toggle(ENABLE_AMBIENT_LIGHT)] _EnableAmbientLight("Enable Ambient Light", Float) = 1
        [Toggle(ENABLE_HDRP_LIGHT_DATA)] _EnableHDRPLightData("Auto HDRP Light (requires HDRP light buffer)", Float) = 1

        // Depth options
        [Toggle(ENABLE_DEPTH_WRITE)] _EnableDepthWrite("Write Depth (voxel-accurate)", Float) = 1
        [Toggle(ENABLE_TRACE_DISTANCE_LIMITED)] _EnableSceneDepthClip("Clip Against Scene Depth", Float) = 1

        // Light influence
        _LightInfluence ("Light Influence", Range(0, 5)) = 1.0
        _AmbientInfluence ("Ambient Influence", Range(0, 5)) = 1.0

        // Color ramp
        [Toggle(ENABLE_COLOR_RAMP)] _EnableColorRamp("Enable Color Ramp", Float) = 0
        _ColorRamp ("Color Ramp", 2D) = "white" {}
        _ColorRampIntensity ("Color Ramp Intensity", Range(0, 2)) = 1.0

        // Spot lights (manual uniforms, set from C#)
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

        // Shadow casting
        _ShadowExtraBias ("Shadow Extra Bias", Range(-0.1, 0.1)) = 0.0
        _ShadowDensityThreshold ("Shadow Density Threshold", Range(0.001, 0.1)) = 0.01
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

            #include "VolumeHDRP.hlsl"
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

            #include "VolumeHDRPShadowCaster.hlsl"
            ENDHLSL
        }
    }

    CustomEditor "OpenVDB.Editor.VolumeHDRPShaderGUI"
}
