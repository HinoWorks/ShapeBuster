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
    float _Bloat;
    half4 _BoilColor;
    float _Split;
    half4 _StitchColor;
    half4 _PatchTint;
    float _SplitSide;
    float _Ghost;
    float _GhostFade;
    float _PodCrack;
    float _Glass;
    float _GlassCrack;
    half4 _GlassTint;
    half4 _GlassInner;
    float _Chest;
    half4 _ChestWood;
    half4 _ChestMetal;
    half4 _ChestLight;
    float _ChestDamage;
CBUFFER_END

#include "BlockZombie.hlsl"

half3 DotShade(half3 color, float2 grid)
{
    float2 f = frac(grid);
    // ブロック（ゾンビ・石・ガラス・宝箱）は、地のドットを目や口などの絵と同じ 1 マスまるごとの大きさにする
    // （隙間を空けると地のドットだけ小さく見える）。隙間を残すのは、フィールドの背景や壁だけ。
    float fullCell = saturate(step(0.5, _Zombie) + step(0.5, _Glass) + step(0.5, _Chest));
#ifdef BLOCK_NO_ZOMBIE
    fullCell = 1.0;
#endif
    float gap = (1.0 - saturate(_DotSize)) * 0.5 * (1.0 - fullCell);
    float inside = step(gap, f.x) * step(f.x, 1.0 - gap);
    inside *= step(gap, f.y) * step(f.y, 1.0 - gap);
    float shade = lerp(0.82, 1.0, Hash21(floor(grid) + _Seed * _Zombie * 17.0));
    return color * lerp(0.94, shade, inside);
}

/// ガラス：中に閉じ込めたゾンビ（1 マスに 1 体）を青白く透かして描き、上にガラスの光と、ひびを重ねる。
/// ひびは _GlassCrack（受けたダメージの割合）で増える。HP の色の段は使わない。
half4 GlassFragment(float2 uv, float2 grid, float2 cells, float scale, float isOutline)
{
    float2 cell = floor(grid);
    float2 total = cells * scale;

    // 中のゾンビ：マスごとに別の種で、1 マスの顔を描く。
    float2 slot = floor(cell / scale);
    float2 local = cell - slot * scale;
    float seed = frac(_Seed + slot.x * 0.37 + slot.y * 0.61);
    ZombieMask m = BuildZombieSeeded(local, float2(scale, scale), float2(1.0, 1.0), seed);
    half3 inner = ZombieColor(_GlassInner.rgb, m);
    // マスの境目は暗い隙間にして、1 体ずつに見せる。
    float2 edge = min(local, scale - 1.0 - local);
    float gap = step(min(edge.x, edge.y), 0.25);
    inner = lerp(inner, inner * 0.35, gap);
    inner = DotShade(inner, grid);

    // ガラスの色を薄く重ね（顔の部品は透けて見えるよう弱めに）、左上から斜めの光の帯を 2 本入れる。
    float parts = saturate(m.eye + m.mouth + m.teeth + m.shadow);
    half3 body = lerp(inner, inner * 0.7 + _GlassTint.rgb * 0.45, _GlassTint.a * (1.0 - parts * 0.6));
    float diag = (cell.x + (total.y - 1.0 - cell.y)) / (total.x + total.y);
    float glare = step(abs(diag - 0.22), 0.03) + step(abs(diag - 0.31), 0.012);
    body = lerp(body, half3(1.0, 1.0, 1.0), saturate(glare) * 0.45 * (1.0 - parts));
    // 内枠：外から 2 ドット目を明るくして、厚いガラスの縁に見せる。
    float2 rimEdge = min(cell, total - 1.0 - cell);
    float rim = step(abs(min(rimEdge.x, rimEdge.y) - 1.0), 0.25);
    body = lerp(body, half3(0.85, 0.97, 1.0), rim * 0.6);

    // ひび：当たった点から放射状にのびる 1 ドットの筋。ダメージが増えるほど長く、本数も増える。
    // 割れる直前は、当たった点のまわりが欠けて暗くなる。
    float crackAmount = saturate(_GlassCrack);
    float2 hit = total * (0.35 + 0.3 * float2(Hash21(float2(_Seed * 3.3, 1.1)), Hash21(float2(_Seed * 4.7, 2.9))));
    float2 d = cell + 0.5 - hit;
    float dist = length(d);
    float crack = 0.0;
    [unroll] for (int k = 0; k < 6; k++)
    {
        float ang = (k + Hash21(float2(_Seed * 7.1, k))) * 1.0472;
        float2 dir = float2(cos(ang), sin(ang));
        float along = dot(d, dir);
        float side = abs(d.x * dir.y - d.y * dir.x);
        // 筋は少しずつ折れ曲がる。
        side = abs(side - (ValueNoise(float2(along * 0.4, k * 3.1 + _Seed)) - 0.5) * 1.6);
        float reach = total.x * crackAmount * lerp(0.5, 0.9, Hash21(float2(k, _Seed)));
        float on = step(side, 0.55) * step(0.0, along) * step(along, reach);
        float appear = step((float)k / 6.0, crackAmount * 1.4);
        crack = max(crack, on * appear);
    }
    float chip = step(dist, crackAmount * crackAmount * 3.0) * step(0.45, Hash21(cell + _Seed * 5.1));
    body = lerp(body, half3(0.95, 1.0, 1.0), crack * 0.9);
    body = lerp(body, half3(0.06, 0.1, 0.14), chip);

    return ComposeBlock(uv, body, 1.0, _OutlineColor, isOutline, _Flash);
}

/// 宝箱：木の箱に、金の合わせ目の帯・四隅の角金具・真ん中の錠前。ふたは合わせ目より上。
/// 合わせ目のすぐ下は暗い隙間で、ダメージ（_ChestDamage、受けたダメージの割合）が増えるほど、そこから光が漏れる。
/// ダメージが増えると、当たった点から折れ曲がったひびがのび、大きいと穴があく。金具には、ときどき斜めの光が走る。HP の色の段は使わない。
half4 ChestFragment(float2 uv, float2 grid, float2 cells, float scale, float isOutline)
{
    float2 total = cells * scale;
    float2 cell = floor(grid);
    float2 edge = min(cell, total - 1.0 - cell);
    float dmg = saturate(_ChestDamage);

    float seamRow = floor(total.y * 0.58);
    float gapRow = seamRow - 1.0;
    float lid = step(seamRow + 0.5, cell.y);

    // 木の板：箱とふたに 1 本ずつ板目。板ごとに少し明るさを変え、ふたは少し明るく。
    float boxGrain = floor(gapRow * 0.45);
    float lidGrain = seamRow + 1.0 + floor((total.y - seamRow - 1.0) * 0.5);
    float plank = cell.y < boxGrain ? 0.0 : (cell.y < gapRow ? 1.0 : (cell.y < lidGrain ? 2.0 : 3.0));
    half3 wood = _ChestWood.rgb * lerp(0.88, 1.05, Hash21(float2(plank, 3.7 + _Seed)));
    wood *= lerp(1.0, 1.14, lid);
    float grain = step(abs(cell.y - boxGrain), 0.25) + step(abs(cell.y - lidGrain), 0.25);
    wood = lerp(wood, wood * 0.68, saturate(grain));
    // ふたの一番上の段は明るく、丸みに見せる。
    wood = lerp(wood, wood * 1.18, step(abs(cell.y - (total.y - 2.0)), 0.25));

    // 金具：合わせ目の帯、四隅の角金具、真ん中の錠前。
    float seam = step(abs(cell.y - seamRow), 0.25);
    float corner = step(edge.x, 0.25) * step(edge.y, 1.25) + step(edge.y, 0.25) * step(edge.x, 1.25);
    float center = floor(total.x * 0.5);
    float lockBody = step(center - 2.0, cell.x) * step(cell.x, center + 1.0)
        * step(seamRow - 2.0, cell.y) * step(cell.y, seamRow + 1.0);
    float keyhole = lockBody * step(center - 1.0, cell.x) * step(cell.x, center) * step(abs(cell.y - gapRow), 0.25);
    float metal = saturate(seam + corner + lockBody);

    // 金具の明るさ：上側を明るく、下側を暗く。斜めの光がゆっくり走る。
    half3 gold = _ChestMetal.rgb * lerp(0.82, 1.12, cell.y / max(1.0, total.y - 1.0));
    float diag = (cell.x + cell.y) / (total.x + total.y);
    float sweep = frac(_Time.y * 0.3 + _Seed) * 2.2 - 0.6;
    gold = lerp(gold, half3(1.0, 1.0, 0.9), step(abs(diag - sweep), 0.045) * 0.7);

    // 合わせ目のすぐ下の暗い隙間。ダメージが増えるほど光が漏れ、ちらつく。
    float gap = step(abs(cell.y - gapRow), 0.25) * (1.0 - lockBody) * (1.0 - corner);
    half3 dark = _ChestWood.rgb * 0.3;
    float flicker = 0.8 + 0.2 * sin(_Time.y * 10.0 + cell.x * 1.9);
    half3 gapColor = lerp(dark, _ChestLight.rgb, saturate(dmg * 1.3) * flicker);

    half3 body = lerp(wood, gold, metal);
    body = lerp(body, gapColor, gap);
    body = lerp(body, half3(0.12, 0.07, 0.04), keyhole);

    // ひび：当たった点から、折れ曲がった 1 ドットの筋がのびる。ダメージが増えるほど長く、本数も増える。
    // 筋のすぐ横は明るくして、割れた木の縁に見せる。ダメージが大きいと、当たった点のまわりが欠けて暗い穴になる。
    float2 hit = total * (0.3 + 0.4 * float2(Hash21(float2(_Seed * 3.3, 1.1)), Hash21(float2(_Seed * 4.7, 2.9))));
    float2 d = cell + 0.5 - hit;
    float crack = 0.0;
    float rim = 0.0;
    [unroll] for (int k = 0; k < 5; k++)
    {
        float ang = (k + Hash21(float2(_Seed * 7.1, k))) * 1.2566;
        float2 dir = float2(cos(ang), sin(ang));
        float along = dot(d, dir);
        float side = abs(d.x * dir.y - d.y * dir.x);
        side = abs(side - (ValueNoise(float2(along * 0.5, k * 3.1 + _Seed)) - 0.5) * 1.6);
        float reach = total.x * (0.15 + dmg * 0.75) * lerp(0.6, 1.0, Hash21(float2(k, _Seed)));
        float inRange = step(0.0, along) * step(along, reach);
        float appear = step((float)k / 5.0, dmg * 1.6) * step(0.04, dmg);
        crack = max(crack, step(side, 0.5) * inRange * appear);
        rim = max(rim, step(0.5, side) * step(side, 1.2) * inRange * appear);
    }
    float hole = step(length(d), dmg * dmg * 2.2) * step(0.3, dmg);
    half3 crackColor = half3(0.13, 0.07, 0.04);
    body = lerp(body, body * 1.25 + 0.05, rim * (1.0 - crack) * 0.6 * (1.0 - keyhole));
    body = lerp(body, crackColor, saturate(crack + hole) * (1.0 - keyhole));
    // 光のにじみ：隙間の上下の段にも、ダメージが大きいと少し光が乗る。
    float near = step(abs(cell.y - gapRow), 1.25) * (1.0 - gap) * (1.0 - lockBody);
    body = lerp(body, _ChestLight.rgb, near * dmg * dmg * 0.35 * flicker);

    body = DotShade(body, grid);
    return ComposeBlock(uv, body, 1.0, _OutlineColor, isOutline, _Flash);
}

half4 UnlitFragment(Varyings input) : SV_Target
{
    float2 uv = input.uv;
    float isOutline = OutlineMask(uv, _OutlineWidth, _OutlineWidthXY.xy);
    float isFilled = FillMask(uv, input.fillRatio);

    float2 cells = max(_CellSpan.xy, float2(1.0, 1.0));
    float scale = max(_DotScale, 1.0);
    float2 grid = uv * cells * scale;
    if (_Glass > 0.5)
        return GlassFragment(uv, grid, cells, scale, isOutline);
    if (_Chest > 0.5)
        return ChestFragment(uv, grid, cells, scale, isOutline);
    ZombieMask m = BuildZombie(floor(grid), cells * scale, cells);

    half4 fill = _FillColor * _Color;
    half4 under = _UnderColor * _Color;
    half3 fillRgb = DotShade(ZombieColor(fill.rgb, m), grid);
    half3 underRgb = DotShade(ZombieColor(under.rgb, m), grid);
    half3 body = lerp(underRgb, fillRgb, isFilled);
    half bodyA = lerp(under.a, fill.a, isFilled);

    half4 color = ComposeBlock(uv, body, bodyA, _OutlineColor, isOutline, _Flash);

    // 幽霊：裾へ向かって薄くなる。すり抜けている間（_GhostFade）は全体が透け、ドットの段がちらつく。
    if (_Ghost > 0.5)
    {
        color.a *= lerp(0.6, 1.0, saturate(uv.y * 3.0));
        float flicker = step(0.5, frac(floor(grid.y) * 0.5 + _Time.y * 3.0));
        color.a *= lerp(1.0, lerp(0.16, 0.32, flicker), saturate(_GhostFade));
        color.rgb = lerp(color.rgb, color.rgb * 0.5 + half3(0.4, 0.62, 0.75), saturate(_GhostFade) * 0.6);
    }
    return color;
}

#endif
