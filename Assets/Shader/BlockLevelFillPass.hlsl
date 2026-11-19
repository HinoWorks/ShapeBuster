#ifndef BLOCK_LEVEL_FILL_PASS_INCLUDED
#define BLOCK_LEVEL_FILL_PASS_INCLUDED

#include "BlockCommon.hlsl"

// 通常ブロック。フィールドの背景や壁もこのシェーダーを使う。
// BlockStoneFill（BLOCK_NO_ZOMBIE）は顔と腐敗を描かない。
// _Zombie が 1 のときだけ、ドットの格子に合わせて顔と腐敗を描く。
// 顔の形と腐敗の模様は _Seed で個体ごとに変える。
// _Regen が 1 のマテリアル（再生ブロック）は、額に青い種と、そこから伸びる血管を描く。
// 種の明るさは _RegenGlow、血管の長さは _RegenCharge、回復した瞬間の光は _HealPulse。
// _ArmorSide が 2（左右の殻）のときは、殻に隠れないよう目を内側へ寄せる。
// 殻そのものは BlockShell で、ブロックの外へはみ出して描く。
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

#include "BlockZombie.hlsl"

half3 DotShade(half3 color, float2 grid)
{
    float2 f = frac(grid);
    float gap = (1.0 - saturate(_DotSize)) * 0.5;
    float inside = step(gap, f.x) * step(f.x, 1.0 - gap);
    inside *= step(gap, f.y) * step(f.y, 1.0 - gap);
    float shade = lerp(0.82, 1.0, Hash21(floor(grid) + _Seed * _Zombie * 17.0));
    return color * lerp(0.94, shade, inside);
}

half4 UnlitFragment(Varyings input) : SV_Target
{
    float2 uv = input.uv;
    float isOutline = OutlineMask(uv, _OutlineWidth, _OutlineWidthXY.xy);
    float isFilled = FillMask(uv, input.fillRatio);

    float2 cells = max(_CellSpan.xy, float2(1.0, 1.0));
    float scale = max(_DotScale, 1.0);
    float2 grid = uv * cells * scale;
    ZombieMask m = BuildZombie(floor(grid), cells * scale, cells);

    half4 fill = _FillColor * _Color;
    half4 under = _UnderColor * _Color;
    half3 fillRgb = DotShade(ZombieColor(fill.rgb, m), grid);
    half3 underRgb = DotShade(ZombieColor(under.rgb, m), grid);
    half3 body = lerp(underRgb, fillRgb, isFilled);
    half bodyA = lerp(under.a, fill.a, isFilled);

    return ComposeBlock(uv, body, bodyA, _OutlineColor, isOutline, _Flash);
}

#endif
