#ifndef _VOLUME_HDRP_UTILS_H_
#define _VOLUME_HDRP_UTILS_H_

struct Ray
{
    float3 origin;
    float3 dir;
};

struct AABB
{
    float3 bmin;
    float3 bmax;
};

inline bool IsInnerCube(float3 pos)
{
    return all(max(0.5 - abs(pos), 0.0));
}

inline float IntersectAABB(Ray r, AABB aabb)
{
    float3 invR = 1.0 / r.dir;
    float3 tbot = invR * (aabb.bmin - r.origin);
    float3 ttop = invR * (aabb.bmax - r.origin);
    float3 tmax = max(ttop, tbot);
    float2 t = min(tmax.xx, tmax.yz);
    return min(t.x, t.y);
}

inline float3 LocalizePosition(float3 p, float4x4 worldToObject)
{
    return mul(worldToObject, float4(p, 1)).xyz;
}

inline float3 GetUV(float3 p)
{
    return (p + 0.5);
}

inline float ComputeOutputDepth(float4 clippos)
{
#if UNITY_REVERSED_Z
    return clippos.z / clippos.w;
#else
    return (clippos.z / clippos.w) * 0.5 + 0.5;
#endif
}

#endif
