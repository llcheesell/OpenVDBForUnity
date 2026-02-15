#ifndef _VOLUME_HDRP_CAMERA_H_
#define _VOLUME_HDRP_CAMERA_H_

// HDRP camera utilities
// Uses HDRP's built-in shader variables from ShaderVariables.hlsl

inline float3 GetCameraPositionHDRP()
{
    return GetCurrentViewPosition();
}

inline float3 GetCameraForwardHDRP()
{
    return -UNITY_MATRIX_V[2].xyz;
}

inline float3 GetCameraUpHDRP()
{
    return UNITY_MATRIX_V[1].xyz;
}

inline float3 GetCameraRightHDRP()
{
    return UNITY_MATRIX_V[0].xyz;
}

inline float GetCameraFocalLengthHDRP()
{
    return abs(UNITY_MATRIX_P[1][1]);
}

inline float GetCameraNearClipHDRP()
{
    return _ProjectionParams.y;
}

inline float GetCameraFarClipHDRP()
{
    return _ProjectionParams.z;
}

inline float3 _GetCameraDirectionHDRP(float2 sp)
{
    float3 camDir   = GetCameraForwardHDRP();
    float3 camUp    = GetCameraUpHDRP();
    float3 camSide  = GetCameraRightHDRP();
    float  focalLen = GetCameraFocalLengthHDRP();

    return normalize((camSide * sp.x) + (camUp * sp.y) + (camDir * focalLen));
}

inline float3 GetCameraDirectionHDRP(float4 screenPos)
{
#if UNITY_UV_STARTS_AT_TOP
    screenPos.y *= -1.0;
#endif
    screenPos.x *= _ScreenParams.x / _ScreenParams.y;
    screenPos.xy /= screenPos.w;

    return _GetCameraDirectionHDRP(screenPos.xy);
}

#endif
