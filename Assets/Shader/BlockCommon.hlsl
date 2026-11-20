#ifndef BLOCK_COMMON_INCLUDED
#define BLOCK_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

// BlockLevelFill と BlockBossFill で共通の処理。
// 頂点、輪郭の判定、レベルの塗り分け、最後の合成をまとめる。
// CBUFFER はシェーダーごとに持つので、ここの関数は値を引数で受け取る。

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

Varyings UnlitVertex(Attributes input)
{
    Varyings output;
    output.positionCS = TransformObjectToHClip(input.positionOS);
    output.uv = input.uv;
    // SpriteRenderer.color.a は、このブロックの残り HP の割合（出現したときの総 HP に対して）。
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

float ValueNoise(float2 p)
{
    float2 cell = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = Hash21(cell);
    float b = Hash21(cell + float2(1.0, 0.0));
    float c = Hash21(cell + float2(0.0, 1.0));
    float d = Hash21(cell + float2(1.0, 1.0));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

/// 輪郭なら 1。widthXY が 0 のときは width を使う。
float OutlineMask(float2 uv, float width1, float2 widthXY)
{
    float centerA = SampleAlpha(uv);
    float2 width = widthXY;
    if (width.x < 0.0001) width.x = width1;
    if (width.y < 0.0001) width.y = width1;
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

    return step(0.01, centerA - neighborMin);
}

/// 現在レベルの色で塗られている部分なら 1。下から塗る。
float FillMask(float2 uv, float fillRatio)
{
    return step(uv.y, fillRatio);
}

/// スプライトを掛け、輪郭と被弾フラッシュを重ねる。
half4 ComposeBlock(float2 uv, half3 body, half bodyA, half4 outlineColor, float isOutline, float flash)
{
    half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, saturate(uv));
    body *= tex.rgb;
    half3 rgb = lerp(body, outlineColor.rgb, isOutline);
    rgb = lerp(rgb, half3(1.0, 1.0, 1.0), saturate(flash));
    half alpha = tex.a * lerp(bodyA, outlineColor.a, isOutline);
    return half4(rgb, alpha);
}

#endif
