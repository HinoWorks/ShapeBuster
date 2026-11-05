#ifndef BLOCK_LEVEL_FILL_PASS_INCLUDED
#define BLOCK_LEVEL_FILL_PASS_INCLUDED

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
    float2 uv : TEXCOORD0;
    float fillRatio : TEXCOORD1;
};

TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);

CBUFFER_START(UnityPerMaterial)
    half4 _Color;
    half4 _FillColor;
    half4 _UnderColor;
    half4 _OutlineColor;
    float _OutlineWidth;
    float4 _OutlineWidthXY;
    float _DotScale;
    float _DotSize;
    float4 _CellSpan;
    float _Flash;
CBUFFER_END

Varyings UnlitVertex(Attributes input)
{
    Varyings output;
    output.positionCS = TransformObjectToHClip(input.positionOS);
    output.uv = input.uv;
    // SpriteRenderer.color.a は、このブロックの現在レベルの残り割合。
    output.fillRatio = saturate(input.color.a);
    return output;
}

float SampleAlpha(float2 uv)
{
    float2 inside = step(float2(0.0, 0.0), uv) * step(uv, float2(1.0, 1.0));
    return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, saturate(uv)).a * inside.x * inside.y;
}

float Hash21(float2 p)
{
    p = frac(p * float2(123.34, 345.45));
    p += dot(p, p + 34.345);
    return frac(p.x * p.y);
}

half3 DotShade(half3 color, float2 uv)
{
    float2 cells = max(_CellSpan.xy, float2(1.0, 1.0));
    float2 grid = uv * cells * max(_DotScale, 1.0);
    float2 f = frac(grid);
    float gap = (1.0 - saturate(_DotSize)) * 0.5;
    float inside = step(gap, f.x) * step(f.x, 1.0 - gap);
    inside *= step(gap, f.y) * step(f.y, 1.0 - gap);
    float shade = lerp(0.82, 1.0, Hash21(floor(grid)));
    return color * lerp(0.94, shade, inside);
}

half4 UnlitFragment(Varyings input) : SV_Target
{
    float2 uv = input.uv;
    float centerA = SampleAlpha(uv);
    float2 width = _OutlineWidthXY.xy;
    if (width.x < 0.0001) width.x = _OutlineWidth;
    if (width.y < 0.0001) width.y = _OutlineWidth;
    width = max(width, 0.0001);
    float2 diagonal = width * 0.70710678;

    float neighborMin = SampleAlpha(uv + float2(width.x, 0.0));
    neighborMin = min(neighborMin, SampleAlpha(uv + float2(-width.x, 0.0)));
    neighborMin = min(neighborMin, SampleAlpha(uv + float2(0.0, width.y)));
    neighborMin = min(neighborMin, SampleAlpha(uv + float2(0.0, -width.y)));
    neighborMin = min(neighborMin, SampleAlpha(uv + float2(diagonal.x, diagonal.y)));
    neighborMin = min(neighborMin, SampleAlpha(uv + float2(diagonal.x, -diagonal.y)));
    neighborMin = min(neighborMin, SampleAlpha(uv + float2(-diagonal.x, diagonal.y)));
    neighborMin = min(neighborMin, SampleAlpha(uv + float2(-diagonal.x, -diagonal.y)));

    float isOutline = step(0.01, centerA - neighborMin);
    float isFilled = step(uv.y, input.fillRatio);

    half4 fill = _FillColor * _Color;
    half4 under = _UnderColor * _Color;
    half3 body = lerp(DotShade(under.rgb, uv), DotShade(fill.rgb, uv), isFilled);
    half bodyA = lerp(under.a, fill.a, isFilled);

    half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, saturate(uv));
    body *= tex.rgb;
    half3 rgb = lerp(body, _OutlineColor.rgb, isOutline);
    rgb = lerp(rgb, half3(1.0, 1.0, 1.0), saturate(_Flash));
    half alpha = tex.a * lerp(bodyA, _OutlineColor.a, isOutline);
    return half4(rgb, alpha);
}

#endif
