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
            ZWrite On
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

            #define ENABLE_CAMERA_INSIDE_CUBE
            #define ENABLE_SAMPLING_START_OFFSET
            #define ENABLE_TRACE_DISTANCE_LIMITED

            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer

            #include "VolumeHDRP.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "SRPDefaultUnlit"
            Tags
            {
                "LightMode" = "SRPDefaultUnlit"
            }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
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

            #define ENABLE_CAMERA_INSIDE_CUBE
            #define ENABLE_SAMPLING_START_OFFSET
            #define ENABLE_TRACE_DISTANCE_LIMITED

            #include "VolumeHDRP.hlsl"
            ENDHLSL
        }
    }

    CustomEditor "OpenVDB.Editor.VolumeHDRPShaderGUI"
}
