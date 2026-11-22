#ifndef SPRITE_POISON_BALL_PASS_INCLUDED
#define SPRITE_POISON_BALL_PASS_INCLUDED

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
    half4 _CoreColor;
    half4 _EdgeColor;
    half4 _BubbleColor;
    float _PixelSize;
    float _Radius;
    float _CoreRadius;
    float _BubbleSize;
    float _BubbleSpeed;
    float _BubbleStrength;
    float _WobbleSpeed;
    float _Glow;
    float _GlowAlpha;
CBUFFER_END

#define POISON_BUBBLE_COUNT 3

Varyings UnlitVertex(Attributes input)
{
    Varyings output;
    output.positionCS = TransformObjectToHClip(input.positionOS);
    output.uv = input.uv;
    output.color = input.color;
    return output;
}

float Hash11(float2 p)
{
    p += 0.17;
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

// 泡ひとつ分。下から浮いてきて、点 → 小さい輪 → 大きい輪とふくらみ、最後に弾けて散る。
// ドットのマス単位で形を決めるので、小さくても丸く見える。
// x：泡の輪、y：泡の光（左上の点）
float2 Bubble(float2 cell, float pixel, float radius, float time, float index)
{
    float t = time * max(_BubbleSpeed, 0.01) * lerp(0.8, 1.2, Hash11(float2(index, 7.3))) + index * 0.37;
    float cycle = floor(t);
    float life = frac(t);
    float seedX = Hash11(float2(index, cycle));

    // 横はランダム、縦は下から上へ。玉の中に収まるように寄せる
    float2 center;
    center.x = (seedX * 2.0 - 1.0) * radius * 0.45;
    center.y = lerp(-0.6, 0.3, life) * radius;
    center.x += sin(life * 6.2831853 + seedX * 20.0) * pixel * 0.6;
    center = floor(center / pixel + 0.5) * pixel;

    // 泡の中心からのマス数
    float d = length(round((cell - center) / pixel));
    float size = max(_BubbleSize, 0.5);

    float stage0 = step(0.05, life) * step(life, 0.3);
    float stage1 = step(0.3, life) * step(life, 0.58);
    float stage2 = step(0.58, life) * step(life, 0.88);
    float stage3 = step(0.88, life) * step(life, 0.97);

    float fill = step(d, 0.1) * stage0;
    float ring1 = step(abs(d - size), 0.3) * stage1;
    float ring2 = step(size * 1.6, d) * step(d, size * 2.3) * stage2;
    // 弾けたあとは、輪の外に点が散る
    float pop = (step(abs(d - 2.83 * size), 0.15) + step(abs(d - 3.0 * size), 0.1)) * stage3;

    float shine = step(length(round((cell - center) / pixel) - float2(-1.0, 1.0)), 0.1) * stage2;
    return float2(saturate(fill + ring1 + ring2 + pop * 0.7), shine);
}

half4 UnlitFragment(Varyings input) : SV_Target
{
    float2 p = input.uv * 2.0 - 1.0;
    float pixel = max(_PixelSize, 0.02);
    float2 cell = floor(p / pixel + 0.5) * pixel;
    float dist = length(cell);
    float radius = saturate(_Radius);
    float time = _Time.y;

    // 縁がゆっくり波打って、どろっとした感じを出す
    float angle = atan2(cell.y, cell.x + 0.0001);
    float wobble = sin(angle * 3.0 + time * _WobbleSpeed) * pixel * 0.35;
    float edgeRadius = radius + wobble;

    float body = step(dist, edgeRadius);
    float rim = body * step(edgeRadius - pixel * 1.6, dist);
    float coreRadius = min(_CoreRadius, radius - pixel);
    float2 coreOffset = float2(-0.12, 0.12) * radius;
    float core = step(length(cell - coreOffset), coreRadius);

    float bubble = 0.0;
    float shine = 0.0;
    [unroll]
    for (int i = 0; i < POISON_BUBBLE_COUNT; i++)
    {
        float2 b = Bubble(cell, pixel, radius, time, i);
        bubble = max(bubble, b.x);
        shine = max(shine, b.y);
    }
    float inner = body * (1.0 - rim);
    bubble *= inner;
    shine *= inner;

    half3 rgb = lerp(_EdgeColor.rgb, _Color.rgb, saturate(1.0 - dist / max(radius, 0.001) * 0.85));
    rgb = lerp(rgb, _EdgeColor.rgb * 0.7, rim);
    rgb = lerp(rgb, _CoreColor.rgb, core * (1.0 - rim) * 0.55);
    rgb = lerp(rgb, _BubbleColor.rgb, bubble * _BubbleStrength);
    rgb = lerp(rgb, half3(1.0, 1.0, 1.0), shine * _BubbleStrength);
    rgb *= input.color.rgb;

    float glow = (1.0 - body) * (1.0 - smoothstep(radius, radius + max(_Glow, pixel), dist));
    float alpha = saturate(body + glow * _GlowAlpha);
    alpha *= input.color.a;
    return half4(rgb, alpha);
}

#endif
