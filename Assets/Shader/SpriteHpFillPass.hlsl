#ifndef SPRITE_HP_FILL_PASS_INCLUDED
#define SPRITE_HP_FILL_PASS_INCLUDED

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
    half4 color : COLOR;
    float2 uv : TEXCOORD0;
    float fillRatio : TEXCOORD1;
};

TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);

CBUFFER_START(UnityPerMaterial)
    half4 _Color;
    half4 _OutlineColor;
    float _OutlineWidth;
CBUFFER_END

Varyings UnlitVertex(Attributes input)
{
    Varyings output;
    output.positionCS = TransformObjectToHClip(input.positionOS);
    output.uv = input.uv;
    // SpriteRenderer.color.a は敵ごとの塗り割合。マテリアルに置くと全敵で共有される。
    output.fillRatio = input.color.a;
    output.color = half4(input.color.rgb, 1.0) * _Color;
    return output;
}

float SampleAlpha(float2 uv)
{
    float2 inside = step(float2(0.0, 0.0), uv) * step(uv, float2(1.0, 1.0));
    return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, saturate(uv)).a * inside.x * inside.y;
}

half4 UnlitFragment(Varyings input) : SV_Target
{
    float2 uv = input.uv;
    float centerA = SampleAlpha(uv);
    float width = max(_OutlineWidth, 0.0001);
    float diagonal = width * 0.70710678;

    float neighborMin = SampleAlpha(uv + float2(width, 0.0));
    neighborMin = min(neighborMin, SampleAlpha(uv + float2(-width, 0.0)));
    neighborMin = min(neighborMin, SampleAlpha(uv + float2(0.0, width)));
    neighborMin = min(neighborMin, SampleAlpha(uv + float2(0.0, -width)));
    neighborMin = min(neighborMin, SampleAlpha(uv + float2(diagonal, diagonal)));
    neighborMin = min(neighborMin, SampleAlpha(uv + float2(diagonal, -diagonal)));
    neighborMin = min(neighborMin, SampleAlpha(uv + float2(-diagonal, diagonal)));
    neighborMin = min(neighborMin, SampleAlpha(uv + float2(-diagonal, -diagonal)));

    float isOutline = step(0.01, centerA - neighborMin);
    float isFilled = step(input.uv.y, input.fillRatio);
    float mask = max(isOutline, (1.0 - isOutline) * isFilled);

    half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, saturate(uv));
    half3 fillRgb = input.color.rgb * tex.rgb;
    half3 rgb = lerp(fillRgb, _OutlineColor.rgb, isOutline);
    half alpha = tex.a * lerp(input.color.a, _OutlineColor.a, isOutline) * mask;
    return half4(rgb, alpha);
}

#endif
