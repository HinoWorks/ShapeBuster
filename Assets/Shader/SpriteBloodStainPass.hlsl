#ifndef SPRITE_BLOOD_STAIN_PASS_INCLUDED
#define SPRITE_BLOOD_STAIN_PASS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

struct Attributes
{
    float3 positionOS : POSITION;
    float4 color : COLOR;
    float2 uv : TEXCOORD0;
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float4 color : COLOR;
    float2 uv : TEXCOORD0;
};

TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);

CBUFFER_START(UnityPerMaterial)
    half4 _Color;
    float _Seed;
CBUFFER_END

Varyings UnlitVertex(Attributes input)
{
    Varyings output;
    output.positionCS = TransformObjectToHClip(input.positionOS);
    output.color = input.color;
    output.uv = input.uv;
    return output;
}

float Hash21(float2 p)
{
    p = frac(p * float2(123.34, 345.45));
    p += dot(p, p + 34.345);
    return frac(p.x * p.y);
}

half4 UnlitFragment(Varyings input) : SV_Target
{
    float2 uv = input.uv;
    float n = Hash21(uv * 2.5 + _Seed);
    float2 p = (uv - 0.5) * float2(1.25, 0.92);
    p += (n - 0.5) * 0.05;
    float dist = length(p);
    float alpha = saturate(1.0 - smoothstep(0.1, 0.46, dist));
    half4 tint = _Color * input.color;
    return half4(tint.rgb, tint.a * alpha);
}

#endif
