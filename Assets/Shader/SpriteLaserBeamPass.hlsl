#ifndef SPRITE_LASER_BEAM_PASS_INCLUDED
#define SPRITE_LASER_BEAM_PASS_INCLUDED

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
};

TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);

CBUFFER_START(UnityPerMaterial)
    half4 _Color;
    float _EndPower;
    float _GradientAngle;
    float _PixelMix;
    float _DotsPerCell;
    float _ColorSteps;
    float _DotWorldSize;
    float _FadeCool;
    float _FadeCrumble;
    float _FadeSpark;
    float _BeamFade;
CBUFFER_END

// ブロックの 1 マスの大きさ（BlockSession が入れる）。0 ならマテリアルの _DotWorldSize を使う。
float _FieldCellSize;

Varyings UnlitVertex(Attributes input)
{
    Varyings output;
    output.positionCS = TransformObjectToHClip(input.positionOS);
    output.uv = input.uv;
    output.color = input.color;
    return output;
}

float BeamHash(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

/// ビームの中の位置 local（ワールドの長さ）から、白〜色の割合を返す。
float EndAmount(float2 local, float2 halfSize, float2 axis)
{
    float reach = dot(halfSize, abs(axis));
    float along = abs(dot(local, axis)) / max(reach, 0.0001);
    return pow(saturate(along), max(_EndPower, 0.01));
}

half4 UnlitFragment(Varyings input) : SV_Target
{
    // 消え方の進み具合（0 → 1）。BlockSession が MaterialPropertyBlock で入れる。
    // スプライトの色は Unity 6 では頂点カラーに来ないので読まない（色はマテリアルの _Color で決める）。
    float fade = saturate(_BeamFade);

    // 0度はビームの上下。90度は左右。角度は画面上の見た目に合わせる。
    float rad = radians(_GradientAngle);
    float2 axis = float2(sin(rad), cos(rad));
    // 大きさは行列の列から取る（行から取ると、回したビームで縦横が入れ替わる）。
    float2 halfSize = float2(
        length(float3(unity_ObjectToWorld[0].x, unity_ObjectToWorld[1].x, unity_ObjectToWorld[2].x)),
        length(float3(unity_ObjectToWorld[0].y, unity_ObjectToWorld[1].y, unity_ObjectToWorld[2].y))) * 0.5;
    halfSize = max(halfSize, 0.0001);
    float2 local = (input.uv * 2.0 - 1.0) * halfSize;
    half3 tint = _Color.rgb;
    float alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a * _Color.a;

    // 消え始めると、白い芯が先に冷めて色に変わる。
    float cool = fade * _FadeCool;

    // なめらかな表現（もとの見た目）。
    half3 smoothRgb = lerp(half3(1.0, 1.0, 1.0), tint, saturate(EndAmount(local, halfSize, axis) + cool));

    // ドットの表現：ブロックと同じ大きさのドットに区切り、色を段にする。
    // ドットはビームの真ん中にそろえるので、細くなっていくときも左右が同じに欠ける。
    float cellSize = _FieldCellSize > 0.0001 ? _FieldCellSize / max(_DotsPerCell, 1.0) : _DotWorldSize;
    float px = max(cellSize, 0.002);
    float2 dotIndex = floor(local / px + 0.5);
    float2 dotCenter = dotIndex * px;
    float steps = max(_ColorSteps, 1.0);
    float end = saturate(EndAmount(dotCenter, halfSize, axis) + cool);
    end = saturate(ceil(end * steps - 0.001) / steps);
    half3 pixelRgb = lerp(half3(1.0, 1.0, 1.0), tint, end);
    // 中心がビームの外に出たドットは消す（端が段々になる）。真ん中の列は必ず残す。
    float2 inside = step(abs(dotCenter), halfSize + px * 0.001);
    inside = max(inside, step(abs(dotCenter), 0.0001));
    float pixelMask = inside.x * inside.y;

    float pixelMix = saturate(_PixelMix);
    half3 rgb = lerp(smoothRgb, pixelRgb, pixelMix);
    alpha *= lerp(1.0, pixelMask, pixelMix);

    // 崩れて消える：ドットごとに消える順番を決める。外側ほど先に、長さの方向には数ドットのかたまりで抜ける。
    float2 seed = float2(unity_ObjectToWorld[0].w, unity_ObjectToWorld[1].w) * 7.31;
    float2 lengthAxis = float2(abs(axis.y), abs(axis.x));
    float acrossIndex = abs(dot(dotIndex, abs(axis)));
    float alongIndex = dot(dotIndex, lengthAxis);
    float reachDots = max(dot(halfSize, abs(axis)) / px, 0.5);
    float outer = saturate(acrossIndex / reachDots);
    float chunk = BeamHash(float2(floor(alongIndex / 3.0), acrossIndex * 0.37) + seed);
    float grain = BeamHash(dotIndex + seed + 17.0);
    float order = lerp(grain, chunk, 0.55) * 0.75 + (1.0 - outer) * 0.25;
    // はじめはゆっくり、終わりにかけて一気に崩れる。
    float crumble = fade * fade * _FadeCrumble;
    float keep = step(crumble, order);
    // 崩れる直前のドットは一瞬だけ白く光る。
    float spark = step(order, crumble + 0.08) * keep * step(0.001, fade) * _FadeSpark;
    rgb = lerp(rgb, half3(1.0, 1.0, 1.0), saturate(spark));

    // 全体の薄まり方はゆっくり。消えるのは主に崩れで見せる。
    alpha *= keep * (1.0 - fade * fade * fade);
    return half4(rgb, alpha);
}

#endif
