#ifndef SPRITE_BOMB_BLAST_PASS_INCLUDED
#define SPRITE_BOMB_BLAST_PASS_INCLUDED

// ボムボールの爆発。オブジェクトの中心が爆心、横の大きさの半分が攻撃範囲の半径。
// 円の外は描かない（攻撃範囲だけに出す）。
// ドットは床と同じマス（BlockSession が入れる _FieldCellSize / _FieldGridOrigin）を等分した格子にそろえる。

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
    float2 positionWS : TEXCOORD1;
};

TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);

CBUFFER_START(UnityPerMaterial)
    float _Progress;
    float _Seed;
    float _DotsPerCell;
    half4 _CoreColor;
    half4 _HotColor;
    half4 _FireColor;
    half4 _EmberColor;
    half4 _SmokeColor;
    half4 _RimColor;
    float _FlashTime;
    float _RingTime;
    float _RingWidth;
    float _FireAmount;
CBUFFER_END

// BlockSession が入れるグローバルの値（床と同じ）。
float _FieldCellSize;
float4 _FieldGridOrigin;

Varyings UnlitVertex(Attributes input)
{
    Varyings output;
    float3 world = TransformObjectToWorld(input.positionOS);
    output.positionCS = TransformWorldToHClip(world);
    output.positionWS = world.xy;
    output.uv = input.uv;
    output.color = input.color;
    return output;
}

float BlastHash(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

half4 UnlitFragment(Varyings input) : SV_Target
{
    float2 center = float2(unity_ObjectToWorld._m03, unity_ObjectToWorld._m13);
    float radius = max(length(unity_ObjectToWorld._m00_m10_m20) * 0.5, 0.0001);

    // ドットの格子。床のマスにそろえる（まだ値が無いときは半径から決める）。
    bool useGlobal = _FieldCellSize > 0.0001;
    float cellSize = useGlobal ? _FieldCellSize : radius * 0.4;
    float2 origin = useGlobal ? _FieldGridOrigin.xy : center;
    float dotSize = cellSize / max(round(_DotsPerCell), 1.0);
    float2 dotId = floor((input.positionWS - origin) / dotSize);
    float2 dotCenter = origin + (dotId + 0.5) * dotSize;

    float d = length(dotCenter - center) / radius;   // 0 = 爆心、1 = 攻撃範囲のふち
    if (d > 1.0) discard;

    float p = saturate(_Progress);
    float rimDots = dotSize / radius;                    // 1 ドットぶんの d

    // 閃光：はじめに範囲全体が白く光り、すぐ引く。
    float flash = 1.0 - saturate(p / max(_FlashTime, 0.001));
    flash *= flash;

    // 衝撃の輪：爆心から外へ、急いで広がってから減速する。
    float ringT = saturate(p / max(_RingTime, 0.001));
    float ringPos = 1.0 - pow(1.0 - ringT, 3.0);
    float ringBand = rimDots * max(_RingWidth, 0.5);
    float ring = step(abs(d - ringPos), ringBand) * (1.0 - ringT * 0.6) * step(ringT, 0.999);
    float swept = step(d, ringPos + ringBand);       // 輪が通ったところだけ燃える

    // 炎：ドットごとのむら。時間で少しずつ入れ替わり、外側と後半ほど冷える。
    float step12 = floor(p * 14.0);
    float n = BlastHash(dotId + _Seed * 17.31 + step12 * 3.7);
    float n2 = BlastHash(dotId * 1.7 + _Seed * 5.13);
    float heat = (1.0 - d * 0.75) * 1.15 - p * 1.25 + (n - 0.5) * 0.55 + (n2 - 0.5) * 0.3;
    heat = heat * _FireAmount * swept;

    half4 col = half4(0, 0, 0, 0);
    if (heat > 0.85)       col = _HotColor;
    else if (heat > 0.55)  col = _FireColor;
    else if (heat > 0.3)   col = _EmberColor;
    else if (heat > 0.08)  col = half4(_SmokeColor.rgb, _SmokeColor.a * saturate(1.0 - p) * step(0.35, n2));

    // 輪と閃光を重ねる。
    col = lerp(col, half4(_HotColor.rgb, 1.0), ring);
    col = lerp(col, half4(_CoreColor.rgb, 1.0), flash);

    // 攻撃範囲のふち：1 ドットの線を、消える直前まで薄く出しておく。
    float rim = step(1.0 - rimDots, d) * saturate(1.0 - p * 1.1);
    col = lerp(col, _RimColor, rim * _RimColor.a * step(col.a, 0.6));

    col.rgb *= input.color.rgb;
    col.a *= input.color.a * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a;
    return col;
}

#endif
