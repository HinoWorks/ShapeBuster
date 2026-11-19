#ifndef BLOCK_BOSS_FILL_PASS_INCLUDED
#define BLOCK_BOSS_FILL_PASS_INCLUDED

#include "BlockCommon.hlsl"

// ボス。塗り・輪郭・被弾フラッシュは BlockCommon、顔と腐敗は BlockZombie。
// 通常ブロックと同じドットの格子に、大きなゾンビの顔を描く。
// ボスらしさとして、侵食で暗くなった肌、ドット単位で這う亀裂、脈打つ内枠と光る目を重ねる。
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
    float _Corruption;
    float _VeinStrength;
    float _VeinSpeed;
    float _PulseSpeed;
    float _Flash;
    float _Zombie;
    float _Seed;
    half4 _EyeColor;
    half4 _MouthColor;
    half4 _TeethColor;
    half4 _RotColor;
    float _RotAmount;
    half4 _WoundColor;
    float _Regen;
    half4 _PodColor;
    half4 _PodDim;
    float _VeinAmount;
    float _RegenGlow;
    float _RegenCharge;
    float _HealPulse;
    float _ArmorSide;
CBUFFER_END

#define BLOCK_ZOMBIE_BROW
#include "BlockZombie.hlsl"

float Heartbeat(float time)
{
    float cycle = frac(time * 0.42);
    float first = smoothstep(0.0, 0.035, cycle) * (1.0 - smoothstep(0.035, 0.11, cycle));
    float second = smoothstep(0.16, 0.19, cycle) * (1.0 - smoothstep(0.19, 0.30, cycle));
    return saturate(first + second * 0.7);
}

/// ドットの中心で亀裂を判定する。亀裂は 1 ドット幅で、ブロックの一部の領域にだけ出る。
float Scar(float2 cell, float scale)
{
    float2 p = (cell + 0.5) / scale;
    float n = ValueNoise(p * 1.7 + float2(2.4, 7.1) + _Seed * 5.0);
    float crack = step(abs(n - 0.5), 0.045);
    float region = step(0.55, Hash21(floor(p * 1.5) + _Seed * 3.0));
    return crack * region;
}

/// 外から 2 ドット目の輪。心拍に合わせて光る。
float InnerRim(float2 cell, float2 total)
{
    float2 edge = min(cell, total - 1.0 - cell);
    return step(abs(min(edge.x, edge.y) - 1.0), 0.25);
}

half3 DotShade(half3 color, float2 grid)
{
    float2 f = frac(grid);
    float gap = (1.0 - saturate(_DotSize)) * 0.5;
    float inside = step(gap, f.x) * step(f.x, 1.0 - gap);
    inside *= step(gap, f.y) * step(f.y, 1.0 - gap);
    float shade = lerp(0.78, 1.0, Hash21(floor(grid) + _Seed * 17.0));
    return color * lerp(0.9, shade, inside);
}

half3 Paint(half3 color, ZombieMask m, float scar, float ember, float beat, float2 grid)
{
    half3 voidColor = half3(0.04, 0.015, 0.05);
    half3 base = lerp(color, voidColor, saturate(_Corruption));
    // 顔の部品より先に亀裂を入れ、目や口の上には亀裂を出さない。
    float face = saturate(m.eye + m.mouth + m.teeth + m.brow + m.shadow);
    half3 hot = lerp(_VeinColor.rgb, half3(1.0, 0.86, 0.9), 0.4);
    base = lerp(base, base * 0.2, scar * (1.0 - face));
    base = lerp(base, hot, ember * saturate(_VeinStrength) * (1.0 - face));
    base = ZombieColor(base, m);
    base = DotShade(base, grid);
    // 目は暗いドットの影響を受けず、心拍で明るくなる。
    half3 eye = lerp(_EyeColor.rgb, half3(1.0, 1.0, 1.0), beat * 0.45);
    return lerp(base, eye, m.eye);
}

half4 UnlitFragment(Varyings input) : SV_Target
{
    float2 uv = input.uv;
    float isOutline = OutlineMask(uv, _OutlineWidth, _OutlineWidthXY.xy);
    float isFilled = FillMask(uv, input.fillRatio);

    float2 cells = max(_CellSpan.xy, float2(1.0, 1.0));
    float scale = max(_DotScale, 1.0);
    float2 total = cells * scale;
    float2 grid = uv * total;

    // ときどき 1 行だけ横にずれるノイズ。
    float row = floor(grid.y);
    float glitch = step(0.992, Hash21(float2(row, floor(_Time.y * 5.0))));
    grid.x += glitch;
    float2 cell = floor(grid);

    ZombieMask m = BuildZombie(cell, total, cells);
    float scar = Scar(cell, scale);
    float flow = ValueNoise((cell + 0.5) / scale * 3.1 + float2(_Time.y * _VeinSpeed, _Time.y * _VeinSpeed * 0.41));
    float ember = scar * step(0.7, flow);
    float beat = Heartbeat(_Time.y * max(_PulseSpeed, 0.0));

    half4 fill = _FillColor * _Color;
    half4 under = _UnderColor * _Color;
    half3 body = lerp(
        Paint(under.rgb, m, scar, ember, beat, grid),
        Paint(fill.rgb, m, scar, ember, beat, grid),
        isFilled);
    half bodyA = lerp(under.a, fill.a, isFilled);

    body = lerp(body, _VeinColor.rgb, InnerRim(cell, total) * lerp(0.25, 0.9, beat));
    body *= lerp(0.96, 1.06, beat);
    body *= lerp(1.0, 0.6, glitch);

    return ComposeBlock(uv, body, bodyA, _OutlineColor, isOutline, _Flash);
}

#endif
