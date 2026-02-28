#ifndef __VOLUME_SHADOWCASTER_INCLUDED__
#define __VOLUME_SHADOWCASTER_INCLUDED__

#include "UnityCG.cginc"
#include "Camera.cginc"
#include "Utils.cginc"

#ifndef ITERATIONS
#define ITERATIONS 100
#endif

uniform sampler3D _Volume;
float _StepDistance;

float SampleVolume(float3 uv)
{
    return tex3D(_Volume, uv).r;
}

struct appdata
{
    float4 vertex : POSITION;
    float3 normal : NORMAL;
};

struct v2f
{
    V2F_SHADOW_CASTER;
    float3 world : TEXCOORD1;
    float3 normal : TEXCOORD2;
};

v2f vert(appdata v)
{
    v2f o;
    o.pos = UnityObjectToClipPos(v.vertex);
    o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
    o.normal = mul(unity_ObjectToWorld, v.normal);
    return o;
}

void frag(v2f i, out float4 outColor : SV_Target, out float outDepth : SV_Depth)
{
    Ray ray;
    ray.origin = Localize(i.world);

    // Light direction: in shadow pass, the view matrix is from the light's perspective
    float3 lightDir = -UNITY_MATRIX_V[2].xyz;
    ray.dir = normalize(mul((float3x3) unity_WorldToObject, lightDir));

    AABB aabb;
    aabb.min = float3(-0.5, -0.5, -0.5);
    aabb.max = float3(0.5, 0.5, 0.5);

    float tfar = Intersect(ray, aabb);

    float3 start = ray.origin;
    float3 end = ray.origin + ray.dir * tfar;

    float dist = length(end - start);
    half stepCount = dist / _StepDistance;
    float3 ds = ray.dir * _StepDistance;

    float3 p = start;
    float3 depth = end;
    bool found = false;

    [loop]
    for (int iter = 0; iter < ITERATIONS; iter++)
    {
        float3 uv = GetUV(p);
        float cursample = SampleVolume(uv);

        if (cursample > 0.01)
        {
            depth = p;
            found = true;
            break;
        }
        p += ds;

        if (iter >= stepCount)
        {
            break;
        }
    }

    // Discard if no volume data was hit - prevents bounding box shadow
    if (!found)
    {
        clip(-1);
    }

    float4 opos = UnityClipSpaceShadowCasterPos(depth, i.normal);
    opos = UnityApplyLinearShadowBias(opos);

    outColor = outDepth = opos.z / opos.w;
}

#endif
