#ifndef SPRITE_LAUNCH_MARK_PASS_INCLUDED
#define SPRITE_LAUNCH_MARK_PASS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

struct Attributes
{
    float3 positionOS : POSITION;
    float2 uv : TEXCOORD0;
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
};

TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);

CBUFFER_START(UnityPerMaterial)
    half4 _Color;
    half4 _Accent;
    float _FillAlpha;
    float _DotsPerCell;
    float _DotWorldSize;
    float _RingRadius;
    float _RingScale;
    float _Spin;
    float _Pulse;
    float _Burst;
    float _Fade;
CBUFFER_END

// ブロックの 1 マスの大きさ（BlockSession が入れる）。0 ならマテリアルの _DotWorldSize を使う。
float _FieldCellSize;

Varyings UnlitVertex(Attributes input)
{
    Varyings output;
    output.positionCS = TransformObjectToHClip(input.positionOS);
    output.uv = input.uv;
    return output;
}

/// 半径 radius の、1 ドット幅の輪。
float RingAt(float r, float radius, float px)
{
    return step(abs(r - radius), px * 0.55);
}

half4 UnlitFragment(Varyings input) : SV_Target
{
    // 大きさは行列の列から取る（ワールドの長さ）。
    float2 size = float2(
        length(float3(unity_ObjectToWorld[0].x, unity_ObjectToWorld[1].x, unity_ObjectToWorld[2].x)),
        length(float3(unity_ObjectToWorld[0].y, unity_ObjectToWorld[1].y, unity_ObjectToWorld[2].y)));
    float2 p = (input.uv - 0.5) * size;

    // ドットの中心で判定する。中心は 4 つのドットの角にくるので、上下左右が同じ形になる。
    float cellSize = _FieldCellSize > 0.0001 ? _FieldCellSize / max(_DotsPerCell, 1.0) : _DotWorldSize;
    float px = max(cellSize, 0.002);
    float2 q = (floor(p / px) + 0.5) * px;
    float r = length(q);
    float angle = atan2(q.y, q.x);

    float radius = max(_RingRadius * _RingScale, px * 1.5);
    float burst = saturate(_Burst);
    float pulse = saturate(_Pulse);
    float stay = 1.0 - burst;

    // 輪：脈に合わせて少し明るくなる。
    float ring = RingAt(r, radius, px) * stay;
    // まわる弧：輪の外に 4 つ。輪からの間は半径に合わせる（最低 1.5 ドット）。
    float seg = frac((angle + _Spin) / (PI * 0.5));
    float arcs = RingAt(r, radius + max(px * 1.5, radius * 0.83), px) * step(seg, 0.3) * stay;
    // 内側：うすく塗る。
    float fill = step(r, radius - px) * _FillAlpha * lerp(0.6, 1.0, pulse) * stay;
    // 中心の点（2×2 ドット）。
    float center = step(r, px) * stay;
    // 撃ったときの波紋：輪が 3 倍まで広がりながら消える。
    float wave = RingAt(r, radius * lerp(1.0, 3.0, burst), px) * (1.0 - burst) * step(0.001, burst);

    half3 ringRgb = lerp(_Accent.rgb, _Color.rgb, lerp(0.6, 1.0, pulse));
    half3 rgb = _Accent.rgb;
    float alpha = fill;
    rgb = lerp(rgb, _Accent.rgb, arcs);
    alpha = max(alpha, arcs * 0.9);
    rgb = lerp(rgb, ringRgb, ring);
    alpha = max(alpha, ring);
    rgb = lerp(rgb, _Color.rgb, max(center, wave));
    alpha = max(alpha, max(center, wave));

    alpha *= SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a * saturate(_Fade) * _Color.a;
    return half4(rgb, alpha);
}

#endif
