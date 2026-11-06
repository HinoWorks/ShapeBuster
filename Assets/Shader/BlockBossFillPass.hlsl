#ifndef BLOCK_BOSS_FILL_PASS_INCLUDED
#define BLOCK_BOSS_FILL_PASS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

// 塗り・輪郭・被弾フラッシュは BlockLevelFill と同じ。
// 表面だけ、重いドット、板の継ぎ目、這う亀裂、中央の裂け目、脈打つ内枠にする。
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
    half4 _VeinColor;
    half4 _CoreColor;
    float _Corruption;
    float _VeinStrength;
    float _VeinSpeed;
    float _PulseSpeed;
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

float Heartbeat(float time)
{
    float cycle = frac(time * 0.42);
    float first = smoothstep(0.0, 0.035, cycle) * (1.0 - smoothstep(0.035, 0.11, cycle));
    float second = smoothstep(0.16, 0.19, cycle) * (1.0 - smoothstep(0.19, 0.30, cycle));
    return saturate(first + second * 0.7);
}

half3 Dots(half3 color, float2 cellUv)
{
    float scale = max(_DotScale, 1.0);
    float2 grid = cellUv * scale;
    float2 f = frac(grid);
    float gap = (1.0 - saturate(_DotSize)) * 0.5;
    float inside = step(gap, f.x) * step(f.x, 1.0 - gap);
    inside *= step(gap, f.y) * step(f.y, 1.0 - gap);
    float shade = lerp(0.5, 1.0, Hash21(floor(grid)));
    float pit = lerp(0.38, shade, inside);
    return color * pit;
}

float Seam(float2 cellUv)
{
    float2 f = frac(cellUv);
    float w = 0.035;
    float seam = step(f.x, w) + step(f.y, w);
    float2 outer = step(cellUv, float2(w, w));
    return saturate(seam * (1.0 - saturate(outer.x + outer.y)));
}

float Scar(float2 cellUv)
{
    float n = ValueNoise(cellUv * 1.7 + float2(2.4, 7.1));
    float crack = 1.0 - smoothstep(0.015, 0.055, abs(n - 0.5));
    float region = step(0.62, Hash21(floor(cellUv * 1.5)));
    return crack * region;
}

float2 Slit(float2 cellUv, float2 cells)
{
    float2 p = cellUv - cells * 0.5;
    float halfH = min(cells.y * 0.28, 0.62);
    float along = abs(p.y) / max(halfH, 0.0001);
    float jag = Hash21(float2(floor(p.y * 14.0 + 4.0), 3.0));
    float halfW = (0.05 + jag * 0.035) * lerp(1.0, 0.25, saturate(along));
    float core = step(abs(p.x), halfW) * step(abs(p.y), halfH);
    float rim = step(abs(p.x), halfW + 0.045) * step(abs(p.y), halfH + 0.04);
    return float2(core, saturate(rim - core));
}

float InnerRim(float2 cellUv, float2 cells)
{
    float2 edge = min(cellUv, cells - cellUv);
    float dist = min(edge.x, edge.y);
    float inner = max(_OutlineWidth, 0.02) + 0.015;
    return step(inner, dist) * step(dist, inner + 0.07);
}

half3 Paint(half3 color, float2 patternUv, float seam, float scar, float ember, float glitch)
{
    half3 voidColor = half3(0.025, 0.008, 0.035);
    half3 base = lerp(voidColor, color, 1.0 - saturate(_Corruption));
    base = Dots(base, patternUv);
    base *= lerp(1.0, 0.32, seam);
    base = lerp(base, base * 0.15, scar);
    half3 hot = lerp(_VeinColor.rgb, half3(1.0, 0.86, 0.9), 0.4);
    base = lerp(base, hot, ember * saturate(_VeinStrength));
    base *= lerp(1.0, 0.5, glitch);
    return base;
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

    float2 cells = max(_CellSpan.xy, float2(1.0, 1.0));
    float2 cellUv = uv * cells;
    float row = floor(cellUv.y * 8.0);
    float glitch = step(0.988, Hash21(float2(row, floor(_Time.y * 5.0))));
    float2 patternUv = cellUv;
    patternUv.x += glitch * 0.12;

    float seam = Seam(cellUv);
    float scar = Scar(cellUv);
    float flow = ValueNoise(cellUv * 3.1 + float2(_Time.y * _VeinSpeed, _Time.y * _VeinSpeed * 0.41));
    float ember = scar * step(0.74, flow);

    half4 fill = _FillColor * _Color;
    half4 under = _UnderColor * _Color;
    half3 body = lerp(
        Paint(under.rgb, patternUv, seam, scar, ember, glitch),
        Paint(fill.rgb, patternUv, seam, scar, ember, glitch),
        isFilled);
    half bodyA = lerp(under.a, fill.a, isFilled);

    float beat = Heartbeat(_Time.y * max(_PulseSpeed, 0.0));
    body = lerp(body, _VeinColor.rgb, InnerRim(cellUv, cells) * lerp(0.22, 1.0, beat));

    float2 slit = Slit(cellUv, cells);
    half3 hot = lerp(_VeinColor.rgb, half3(1.0, 0.88, 0.92), 0.45);
    body = lerp(body, hot, slit.y * lerp(0.35, 1.0, beat));
    body = lerp(body, _CoreColor.rgb, slit.x);
    body *= lerp(0.96, 1.06, beat);

    half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, saturate(uv));
    body *= tex.rgb;
    half3 rgb = lerp(body, _OutlineColor.rgb, isOutline);
    rgb = lerp(rgb, half3(1.0, 1.0, 1.0), saturate(_Flash));
    half alpha = tex.a * lerp(bodyA, _OutlineColor.a, isOutline);
    return half4(rgb, alpha);
}

#endif
