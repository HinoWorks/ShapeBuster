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
    half4 _FleshColor;
    half4 _DotColor0;
    half4 _DotColor1;
    half4 _DotColor2;
    float _DotScale;
    float _DotDensity;
    float _DotSize;
    float _Flash;
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

float Hash21(float2 p)
{
    p = frac(p * float2(123.34, 345.45));
    p += dot(p, p + 34.345);
    return frac(p.x * p.y);
}

half3 DotColor(float2 cell)
{
    float pick = Hash21(cell + float2(5.2, 1.3));
    half3 color = _DotColor0.rgb;
    color = lerp(color, _DotColor1.rgb, step(0.333333, pick));
    color = lerp(color, _DotColor2.rgb, step(0.666667, pick));
    return color;
}

half3 FleshPattern(float2 uv)
{
    float scale = max(_DotScale, 1.0);
    float2 scaled = uv * scale;
    float2 cell = floor(scaled);
    float2 f = frac(scaled);

    float margin = (1.0 - saturate(_DotSize)) * 0.5;
    float inside = step(margin, f.x) * step(f.x, 1.0 - margin);
    inside *= step(margin, f.y) * step(f.y, 1.0 - margin);

    float isDot = step(Hash21(cell), saturate(_DotDensity)) * inside;
    half3 color = lerp(_FleshColor.rgb, DotColor(cell), isDot);

    float2 fineScaled = uv * (scale * 2.0) + float2(4.2, 1.7);
    float2 fineCell = floor(fineScaled);
    float fineDot = step(Hash21(fineCell + 11.0), saturate(_DotDensity) * 0.4);
    color = lerp(color, DotColor(fineCell + 3.0), fineDot);
    return color;
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
    half3 fillRgb = FleshPattern(uv) * tex.rgb;
    fillRgb = lerp(fillRgb, half3(1.0, 1.0, 1.0), saturate(_Flash));
    half3 rgb = lerp(fillRgb, _OutlineColor.rgb, isOutline);
    half alpha = tex.a * lerp(input.color.a, _OutlineColor.a, isOutline) * mask;
    return half4(rgb, alpha);
}

#endif
