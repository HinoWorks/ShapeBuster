#ifndef BLOCK_ZOMBIE_INCLUDED
#define BLOCK_ZOMBIE_INCLUDED

// ゾンビの顔と腐敗。BlockLevelFill と BlockBossFill で共通。
// 使う側の CBUFFER に次の値を宣言してから include する。
// _DotScale, _Zombie, _Seed, _EyeColor, _MouthColor, _TeethColor, _RotColor, _RotAmount, _WoundColor,
// _Regen, _PodColor, _PodDim, _VeinAmount, _RegenGlow, _RegenCharge, _HealPulse, _ArmorSide, _Bloat, _BoilColor, _Split, _StitchColor, _PatchTint, _SplitSide, _Ghost

struct ZombieMask
{
    float rot;
    float wound;
    float eye;
    float shadow;
    float mouth;
    float teeth;
    float pod;
    float podLight;
    float podDark;
    float vein;
    float brow;
    float boil;
    float boilShade;
    float sheen;
    float seam;
    float stitch;
    float halfShade;
};

float Box(float2 p, float x0, float y0, float w, float h)
{
    return step(x0, p.x) * step(p.x, x0 + w - 0.5) * step(y0, p.y) * step(p.y, y0 + h - 0.5);
}

/// 左右の殻がブロックの内側に入り込む列数。BlockPiece の殻の配置と合わせる。
float ShellInnerCols(float totalX)
{
    return max(2.0, floor(totalX * 0.3));
}

/// ドット番号 cell、ブロック全体のドット数 total、マス数 cells から顔と腐敗の位置を求める。
ZombieMask BuildZombie(float2 cell, float2 total, float2 cells)
{
    ZombieMask m = (ZombieMask)0;
#ifdef BLOCK_NO_ZOMBIE
    return m;
#endif
    if (_Zombie < 0.5) return m;

    float s0 = Hash21(float2(_Seed * 91.7, 3.1));
    float s1 = Hash21(float2(_Seed * 57.3, 8.9));
    float s2 = Hash21(float2(_Seed * 23.9, 1.7));
    float s3 = Hash21(float2(_Seed * 71.1, 5.3));

    // 腐敗：ノイズのまだらを暗く緑寄りにし、ところどころに傷のドットを置く。
    float n = ValueNoise(cell * 0.3 + _Seed * 37.0);
    m.rot = step(1.0 - saturate(_RotAmount), n);
    m.wound = m.rot * step(0.9, Hash21(cell + _Seed * 13.1));

    // 顔：短い辺のドット数を一辺とする正方形。縦長は上寄せ、それ以外は中央。
    float scale = max(_DotScale, 1.0);
    float size = max(3.0, round(min(cells.x, cells.y) * scale));
    float ox = floor((total.x - size) * 0.5);
    float oy = cells.y > cells.x + 0.5 ? floor(total.y - size) : floor((total.y - size) * 0.5);
    float2 p = cell - float2(ox, oy);
    float inFace = Box(p, 0.0, 0.0, size, size);

    // 目：左右で高さや大きさをずらして虚ろにする。
    float eyeSize = max(1.0, floor(size / 7.0));
    float eyeRow = floor(size * 0.55);
    float inset = max(1.0, floor(size * 0.2));
    float leftCol = inset;
    float rightCol = size - inset - eyeSize;
    if (_ArmorSide > 1.5)
    {
        // 左右の殻に目が隠れないよう、内側へ寄せる。
        float shellCols = ShellInnerCols(total.x);
        leftCol = max(leftCol, shellCols - ox);
        rightCol = min(rightCol, total.x - shellCols - ox - eyeSize);
    }
    float leftRow = eyeRow - (s0 < 0.35 ? 1.0 : 0.0);
    float rightRow = eyeRow - (s0 >= 0.35 && s0 < 0.7 ? 1.0 : 0.0);
    float big = size >= 8.0 && s1 > 0.55 ? 1.0 : 0.0;
    float leftSize = eyeSize + (s0 >= 0.35 ? big : 0.0);
    float rightSize = eyeSize + (s0 < 0.35 ? big : 0.0);
    rightCol -= rightSize - eyeSize;

    float eye = saturate(Box(p, leftCol, leftRow, leftSize, leftSize) + Box(p, rightCol, rightRow, rightSize, rightSize));
    float shadow = saturate(Box(p, leftCol, leftRow - 1.0, leftSize, 1.0) + Box(p, rightCol, rightRow - 1.0, rightSize, 1.0));

    // 眉（BLOCK_ZOMBIE_BROW のときだけ）：目の上に、内側へ下がる怒り眉を置く。
    float brow = 0.0;
#ifdef BLOCK_ZOMBIE_BROW
    {
        float bw = leftSize + 1.0;
        float half1 = floor(bw * 0.5);
        float lTop = leftRow + leftSize + 1.0;
        brow += Box(p, leftCol - 1.0, lTop, half1, 1.0) + Box(p, leftCol - 1.0 + half1, lTop - 1.0, bw - half1, 1.0);
        bw = rightSize + 1.0;
        half1 = floor(bw * 0.5);
        float rTop = rightRow + rightSize + 1.0;
        brow += Box(p, rightCol, rTop - 1.0, bw - half1, 1.0) + Box(p, rightCol + bw - half1, rTop, half1, 1.0);
        brow = saturate(brow);
    }
#endif

    // 口：歯抜けの笑い、開いた穴、ゆがんだ線の三種類。
    float mouthRow = max(1.0, floor(size * 0.22));
    float halfW = max(1.0, floor(size * 0.25));
    float shift = size >= 8.0 ? (s2 < 0.33 ? -1.0 : (s2 > 0.66 ? 1.0 : 0.0)) : 0.0;
    float mx = floor(size * 0.5) - halfW + shift;
    float mw = halfW * 2.0;
    float mouthType = floor(s3 * 3.0);

    float mouth = 0.0;
    float teeth = 0.0;
    if (mouthType < 0.5)
    {
        float row1 = Box(p, mx, mouthRow, mw, 1.0);
        float missing = floor(Hash21(float2(_Seed * 5.7, 2.2)) * mw);
        float k = p.x - mx;
        float isTooth = step(frac(k * 0.5), 0.25) * (1.0 - step(abs(k - missing), 0.25));
        teeth = row1 * isTooth;
        mouth = row1 * (1.0 - isTooth);
    }
    else if (mouthType < 1.5)
    {
        float h = mouthRow >= 2.0 ? 2.0 : 1.0;
        mouth = Box(p, mx, mouthRow - h + 1.0, mw, h);
        float fangX = s2 < 0.5 ? mx : mx + mw - 1.0;
        teeth = size >= 8.0 ? mouth * Box(p, fangX, mouthRow, 1.0, 1.0) : 0.0;
        mouth *= 1.0 - teeth;
    }
    else
    {
        float endX = s2 < 0.5 ? mx - 1.0 : mx + mw;
        mouth = saturate(Box(p, mx, mouthRow, mw, 1.0) + Box(p, endX, mouthRow + 1.0, 1.0, 1.0));
    }

    // 再生の種：額に埋まった、角を落とした 4×4 以上の塊。
    // 左上に光のドット、右下に影の縁を付けて宝石のように見せ、真下を 1 ドット暗くする。
    float pod = 0.0;
    float podLight = 0.0;
    float podDark = 0.0;
    float podRim = 0.0;
    if (_Regen > 0.5)
    {
        float pw = max(4.0, round(size * 0.4));
        float ph = pw;
        float px0 = floor((size - pw) * 0.5);
        // 上の輪郭に隠れないよう、上端から 1 ドット空ける。
        float py0 = max(0.0, min(eyeRow + 1.0, size - 2.0 - ph));
        pod = Box(p, px0, py0, pw, ph);
        float corners = Box(p, px0, py0, 1.0, 1.0) + Box(p, px0 + pw - 1.0, py0, 1.0, 1.0)
            + Box(p, px0, py0 + ph - 1.0, 1.0, 1.0) + Box(p, px0 + pw - 1.0, py0 + ph - 1.0, 1.0, 1.0);
        pod *= 1.0 - saturate(corners);
        podLight = Box(p, px0 + 1.0, py0 + ph - 2.0, 1.0, 1.0);
        podDark = saturate(Box(p, px0 + 1.0, py0, pw - 1.0, 1.0) + Box(p, px0 + pw - 1.0, py0 + 1.0, 1.0, ph - 2.0));
        podRim = Box(p, px0 + 1.0, py0 - 1.0, pw - 2.0, 1.0);

        // 血管：種から伸びる 1 ドット幅の筋。回復が溜まるほど遠くまで伸びる。
        float2 podCenter = float2(px0 + pw * 0.5, py0 + ph * 0.5);
        float dist = length(p + 0.5 - podCenter) / size;
        float reach = lerp(0.3, 0.85, saturate(_RegenCharge));
        float vn = ValueNoise(cell * 0.5 + _Seed * 11.0);
        m.vein = step(abs(vn - 0.5), _VeinAmount) * step(dist, reach) * (1.0 - pod);
    }

    m.pod = pod * inFace;
    m.podLight = podLight * m.pod;
    m.podDark = podDark * m.pod;
    m.eye = eye * inFace * (1.0 - m.pod);
    m.shadow = saturate(shadow + podRim) * inFace * (1.0 - m.eye) * (1.0 - m.pod);
    m.brow = brow * inFace * (1.0 - m.eye);
    m.mouth = mouth * inFace;
    m.teeth = teeth * inFace;
    // 膨張：膿の粒を散らし、左上に丸みの光沢を置く。粒は顔の部品の上には出さない。
    if (_Bloat > 0.5)
    {
        float parts = saturate(m.eye + m.shadow + m.mouth + m.teeth + m.brow);
        float boil = step(0.93, Hash21(cell + _Seed * 7.3)) * (1.0 - parts);
        float above = step(0.93, Hash21(cell + float2(0.0, 1.0) + _Seed * 7.3));
        m.boil = boil;
        m.boilShade = (1.0 - boil) * above * (1.0 - parts);
        float2 q = (cell + 0.5) / max(total, 1.0);
        m.sheen = step(length(q - float2(0.3, 0.74)), 0.1) * (1.0 - parts) * (1.0 - boil);
        m.rot *= 0.5;
    }

    // 分裂：別の死体を縫い合わせた継ぎはぎ。
    // 親（_SplitSide 0）は真ん中を縦に縫い、右半分を別の肌の色にする。
    // 子（_SplitSide -1 が左、1 が右）は、切られた側の縁に縫い目が残り、右の子は全身が別の肌の色。
    if (_Split > 0.5)
    {
        float inside = step(1.0, cell.y) * step(cell.y, total.y - 2.0);
        // 縫い糸は 2 ドットおき。縫い目をまたぐ横向きの糸にする。
        float stitchRow = step(abs(fmod(cell.y + floor(total.y * 0.5), 3.0) - 1.0), 0.25);
        float dx;
        if (abs(_SplitSide) < 0.5)
        {
            float col = floor(total.x * 0.5);
            dx = cell.x - col;
            m.halfShade = step(col + 0.5, cell.x);
            m.stitch = step(abs(dx), 1.25) * stitchRow * inside;
        }
        else
        {
            // 子の縫い目は、輪郭のすぐ内側の列。糸は内側へ 1 ドットだけ伸びる。
            float col = _SplitSide < 0.0 ? total.x - 2.0 : 1.0;
            dx = cell.x - col;
            m.halfShade = step(0.0, _SplitSide);
            float toward = _SplitSide < 0.0 ? -dx : dx;
            m.stitch = step(abs(dx), 0.25) * stitchRow * inside
                + step(abs(toward - 1.0), 0.25) * stitchRow * inside;
        }
        m.seam = step(abs(dx), 0.25) * inside * (1.0 - m.stitch);
        m.rot *= 0.5;
    }

    // 幽霊：腐敗と傷はほとんど出さない。
    if (_Ghost > 0.5)
        m.rot *= 0.3;

    // 顔の部品の上には傷を出さない。
    float face = saturate(m.eye + m.shadow + m.mouth + m.teeth + m.pod + m.vein + m.brow);
    m.wound *= 1.0 - face;

    return m;
}

half3 ZombieColor(half3 color, ZombieMask m)
{
    // 膨張は肌を黄ばませる。
    color = lerp(color, color * half3(1.12, 1.08, 0.62), saturate(_Bloat) * 0.6);
    // 幽霊は肌を青白くする。HP の色の段が読めるよう、元の色も少し残す。
    half grey = dot(color, half3(0.3, 0.55, 0.15));
    color = lerp(color, grey * half3(0.8, 1.0, 1.15) + half3(0.18, 0.24, 0.3), saturate(_Ghost) * 0.6);
    color = lerp(color, color * _PatchTint.rgb, m.halfShade);
    color = lerp(color, color * _RotColor.rgb, m.rot);
    color = lerp(color, color * 0.25, m.seam);
    color = lerp(color, _StitchColor.rgb, m.stitch);
    color = lerp(color, color * 1.25 + 0.06, m.sheen);
    color = lerp(color, color * 0.6, m.boilShade);
    color = lerp(color, _BoilColor.rgb, m.boil);
    color = lerp(color, _WoundColor.rgb, m.wound);
    half3 veinColor = lerp(_PodDim.rgb, _PodColor.rgb, saturate(_RegenGlow) * 0.6);
    veinColor = lerp(veinColor, half3(1.0, 1.0, 1.0), saturate(_HealPulse) * 0.5);
    color = lerp(color, veinColor, m.vein * 0.85);
    color = lerp(color, color * 0.55, m.shadow);
    color = lerp(color, _MouthColor.rgb, saturate(m.mouth + m.brow));
    color = lerp(color, _TeethColor.rgb, m.teeth);
    // 継ぎはぎの側の目は、白く濁った別人の目。
    half3 eyeColor = lerp(_EyeColor.rgb, half3(0.92, 0.93, 0.88), m.halfShade * saturate(_Split));
    color = lerp(color, eyeColor, m.eye);
    half3 podColor = lerp(_PodDim.rgb, _PodColor.rgb, saturate(_RegenGlow));
    podColor = lerp(podColor, _PodDim.rgb * 0.6, m.podDark);
    podColor = lerp(podColor, half3(1.0, 1.0, 1.0), m.podLight * 0.7);
    podColor = lerp(podColor, half3(1.0, 1.0, 1.0), saturate(_HealPulse) * 0.7);
    color = lerp(color, podColor, m.pod);

    return color;
}

#endif
