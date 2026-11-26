#ifndef BLOCK_BOSS_FILL_PASS_INCLUDED
#define BLOCK_BOSS_FILL_PASS_INCLUDED

#include "BlockCommon.hlsl"

// ボス。塗り・輪郭・被弾フラッシュは BlockCommon、顔と腐敗は BlockZombie。
// 通常ブロックと同じドットの格子に、大きなゾンビの顔を描く。
// ボスらしさとして、侵食で暗くなった肌、ドット単位で這う亀裂、脈打つ内枠と光る目を重ねる。
// _Shape が 0 より大きいと、形（台形・菱形）をドット単位で計算し、輪郭も 1 ドットの段々で描く。
// _BossShell が 1（甲羅ボス）だと、_ShellOpen 以外の面に、内側 3 ドットの殻を描く。開いた面は弱点として光る。
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
    float _Bloat;
    half4 _BoilColor;
    float _Split;
    half4 _StitchColor;
    half4 _PatchTint;
    float _SplitSide;
    float _Ghost;
    float _GhostFade;
    float _PodCrack;
    float _Shape;
    float _BossShell;
    half4 _ShellTint;
    float _ShellOpen;
    float _ShellWarn;
    float _ShellHit;
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

/// ドット cell が形の中なら 1。1 台形（上がせまい）、2 菱形（先を落とす）、3 砂時計、4 王冠、
/// 5 十字（四隅が欠ける）、6 逆台形（上が広い）、7 六角形（左右がとがる）。それ以外は四角。
/// 当たり判定の形（BossShapeColliders）と合わせる。
float ShapeInside(float2 c, float2 total)
{
    float inBox = step(0.0, c.x) * step(0.0, c.y) * step(c.x, total.x - 1.0) * step(c.y, total.y - 1.0);
    if (_Shape < 0.5) return inBox;
    if (_Shape < 1.5)
    {
        float inset = floor(c.y * 0.3);
        return inBox * step(inset, c.x) * step(c.x, total.x - 1.0 - inset);
    }
    float2 d = abs(c + 0.5 - total * 0.5) / (total * 0.5);
    if (_Shape < 2.5) return inBox * step(d.x + d.y, 1.25);
    if (_Shape < 3.5)
    {
        // 砂時計：真ん中の段で左右から 1/4 ずつくびれる。
        float inset = floor((1.0 - d.y) * total.x * 0.24);
        return inBox * step(inset, c.x) * step(c.x, total.x - 1.0 - inset);
    }
    if (_Shape < 4.5)
    {
        // 王冠：下の 3/4 は四角。上の 1/4 に、左右の端と真ん中の 3 本のトゲ。
        float baseTop = floor(total.y * 0.74);
        float k = (c.y - baseTop) / max(1.0, total.y - 1.0 - baseTop);
        float w = lerp(total.x * 0.13, 0.6, k);
        float spike = step(abs(c.x + 0.5 - total.x * 0.11), w) + step(abs(c.x + 0.5 - total.x * 0.5), w)
            + step(abs(c.x + 0.5 - total.x * 0.89), w);
        return inBox * saturate(step(c.y, baseTop) + spike);
    }
    if (_Shape < 5.5)
    {
        // 十字：四隅を、横 2/9・縦 2/9 ずつ欠く。
        float2 cut = floor(total * float2(0.22, 0.22));
        float corner = (1.0 - step(cut.x, c.x) * step(c.x, total.x - 1.0 - cut.x))
            * (1.0 - step(cut.y, c.y) * step(c.y, total.y - 1.0 - cut.y));
        return inBox * (1.0 - corner);
    }
    if (_Shape < 6.5)
    {
        // 逆台形：下ほどせまい。
        float inset = floor((total.y - 1.0 - c.y) * 0.3);
        return inBox * step(inset, c.x) * step(c.x, total.x - 1.0 - inset);
    }
    // 六角形：上下は平らで、左右の真ん中がとがる。
    float inset6 = floor(d.y * total.x * 0.24);
    return inBox * step(inset6, c.x) * step(c.x, total.x - 1.0 - inset6);
}

/// 形の外までのドット数（1 なら一番外側の 1 ドット）。5 以上は 5。
float EdgeDist(float2 c, float2 total)
{
    if (ShapeInside(c, total) < 0.5) return 0.0;
    [unroll] for (int r = 1; r <= 4; r++)
    {
        float k = (float)r;
        float m = ShapeInside(c + float2(k, 0.0), total) * ShapeInside(c - float2(k, 0.0), total)
            * ShapeInside(c + float2(0.0, k), total) * ShapeInside(c - float2(0.0, k), total)
            * ShapeInside(c + float2(k, k), total) * ShapeInside(c - float2(k, k), total)
            * ShapeInside(c + float2(k, -k), total) * ShapeInside(c - float2(k, -k), total);
        if (m < 0.5) return k;
    }
    return 5.0;
}

/// ドットがどの面か。0 下、1 右、2 上、3 左。中心から見て、縦横をならした向きで決める（BlockPiece.ShellFace と同じ）。
float FaceOf(float2 c, float2 total)
{
    float2 n = (c + 0.5) / total - 0.5;
    if (abs(n.y) > abs(n.x)) return n.y < 0.0 ? 0.0 : 2.0;
    return n.x > 0.0 ? 1.0 : 3.0;
}

half3 DotShade(half3 color, float2 grid)
{
    float2 f = frac(grid);
    // ゾンビは、地のドットを目や口と同じ 1 マスまるごとの大きさにする（隙間を空けると地のドットだけ小さく見える）。
    float gap = (1.0 - saturate(_DotSize)) * 0.5 * (1.0 - step(0.5, _Zombie));
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

    float shaped = step(0.5, _Shape);
    float inside = 1.0;
    float rim = InnerRim(cell, total);
    float edge = 5.0;
    if (shaped > 0.5)
    {
        inside = ShapeInside(cell, total);
        edge = EdgeDist(cell, total);
        isOutline = inside * step(edge, 1.5);
        rim = inside * step(abs(edge - 2.0), 0.25);
    }

    if (_BossShell > 0.5)
    {
        // 殻：外から 2〜4 ドット目。開いた面は殻がなく、内枠が弱点として強く光る。
        float band = inside * step(1.5, edge) * step(edge, 4.5);
        float open = step(abs(FaceOf(cell, total) - _ShellOpen), 0.25);
        float shell = band * (1.0 - open);
        // 甲羅の板：斜めにずらした継ぎ目で区切り、外側ほど明るくする。
        float seam = step(frac((cell.x + floor(cell.y / 3.0) * 2.0) / 6.0), 0.17);
        half3 plate = _ShellTint.rgb * lerp(1.15, 0.72, (edge - 2.0) / 2.0);
        plate = lerp(plate, _ShellTint.rgb * 0.38, seam * step(2.5, edge));
        plate *= lerp(0.9, 1.05, Hash21(cell + 3.7));
        float blink = saturate(_ShellWarn) * step(0.5, frac(_Time.y * 7.0));
        plate = lerp(plate, half3(1.0, 0.95, 0.8), saturate(blink * 0.7 + _ShellHit));
        body = lerp(body, plate, shell);
        rim = 0.0;
        // 弱点：殻のない面の 3 ドットが、外側ほど明るく脈打つ。
        float weak = band * open;
        half3 hotWeak = lerp(_VeinColor.rgb, half3(1.0, 0.92, 0.95), lerp(0.55, 0.1, (edge - 2.0) / 2.0));
        float pulse = lerp(0.6, 1.0, 0.5 + 0.5 * sin(_Time.y * 9.0));
        body = lerp(body, hotWeak * pulse, weak * lerp(1.0, 0.55, (edge - 2.0) / 2.0));
    }

    body = lerp(body, _VeinColor.rgb, rim * lerp(0.25, 0.9, beat));
    body *= lerp(0.96, 1.06, beat);
    body *= lerp(1.0, 0.6, glitch);

    half4 color = ComposeBlock(uv, body, bodyA, _OutlineColor, isOutline, _Flash);
    color.a *= inside;
    return color;
}

#endif
