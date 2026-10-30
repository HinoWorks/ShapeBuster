#ifndef SPRITE_POTION_PURIFY_PASS_INCLUDED
#define SPRITE_POTION_PURIFY_PASS_INCLUDED

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
TEXTURE2D(_BubbleTex);
SAMPLER(sampler_BubbleTex);
float4 _BubbleTex_TexelSize;

CBUFFER_START(UnityPerMaterial)
    float4 _BubbleTex_ST;
    half4 _Color;
    half4 _DeepColor;
    half4 _ShallowColor;
    half4 _FoamColor;
    half4 _GlowColor;
    float _LiquidAlpha;
    float _RippleSpeed;
    float _RippleScale;
    float _RippleStrength;
    float _BubbleScale;
    float _BubbleSpeed;
    float _BubbleDensity;
    float _DotCount;
    float _EdgeWidth;
CBUFFER_END

Varyings UnlitVertex(Attributes input)
{
    Varyings output;
    output.positionCS = TransformObjectToHClip(input.positionOS);
    output.uv = input.uv;
    output.color = input.color * _Color;
    return output;
}

float SampleAlpha(float2 uv)
{
    float2 inside = step(float2(0.0, 0.0), uv) * step(uv, float2(1.0, 1.0));
    return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, saturate(uv)).a * inside.x * inside.y;
}

float Hash11(float2 p)
{
    p += 0.17;
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float2 Hash22(float2 p)
{
    return float2(Hash11(p), Hash11(p + 19.19));
}

float SurfaceHeight(float2 p, float time)
{
    float2 drop0 = float2(sin(time * 0.37), cos(time * 0.29)) * 0.12;
    float2 drop1 = float2(sin(time * 0.23 + 1.7), cos(time * 0.31 + 2.2)) * 0.16;
    float r0 = length(p - drop0);
    float r1 = length(p - drop1);
    float freq = max(_RippleScale, 1.0);
    float speed = _RippleSpeed;
    float ring0 = sin(r0 * freq - time * speed) * exp(-r0 * 2.4);
    float ring1 = sin(r1 * freq * 1.31 - time * speed * 0.77) * exp(-r1 * 2.1);
    float slosh = sin(p.x * 6.5 + p.y * 3.2 + time * speed * 0.45);
    slosh += sin(p.y * 8.0 - p.x * 2.4 - time * speed * 0.32) * 0.6;
    return (ring0 * 0.55 + ring1 * 0.4 + slosh * 0.22) * _RippleStrength;
}

float NeighborMin(float2 uv, float width)
{
    float diagonal = width * 0.70710678;
    float neighbor = SampleAlpha(uv + float2(width, 0.0));
    neighbor = min(neighbor, SampleAlpha(uv + float2(-width, 0.0)));
    neighbor = min(neighbor, SampleAlpha(uv + float2(0.0, width)));
    neighbor = min(neighbor, SampleAlpha(uv + float2(0.0, -width)));
    neighbor = min(neighbor, SampleAlpha(uv + float2(diagonal, diagonal)));
    neighbor = min(neighbor, SampleAlpha(uv + float2(diagonal, -diagonal)));
    neighbor = min(neighbor, SampleAlpha(uv + float2(-diagonal, diagonal)));
    neighbor = min(neighbor, SampleAlpha(uv + float2(-diagonal, -diagonal)));
    return neighbor;
}

half4 SampleBubbleSprite(float2 delta, float radius, float visible)
{
    float size = max(radius * 2.0, 0.001);
    float2 bubbleUv = delta / size + 0.5;
    float2 inside = step(float2(0.0, 0.0), bubbleUv) * step(bubbleUv, float2(1.0, 1.0));
    float2 texelCount = max(_BubbleTex_TexelSize.zw, float2(1.0, 1.0));
    float2 pointUv = (floor(saturate(bubbleUv) * texelCount) + 0.5) / texelCount;
    pointUv = pointUv * _BubbleTex_ST.xy + _BubbleTex_ST.zw;
    half4 sprite = SAMPLE_TEXTURE2D(_BubbleTex, sampler_BubbleTex, pointUv);
    sprite.rgb *= _FoamColor.rgb;
    sprite.a *= inside.x * inside.y * visible;
    return sprite;
}

half4 PickBubble(half4 current, half4 next)
{
    return lerp(current, next, step(current.a, next.a));
}

// 指定画像を、マスごとにぷくぷく浮かせて貼る。
half4 BubbleLayer(float2 uv, float time, float scale, float speed, float density)
{
    half4 result = 0.0;
    float2 grid = uv * max(scale, 1.0);
    float2 cellId = floor(grid);
    float2 cellUv = frac(grid);

    [unroll]
    for (int y = -1; y <= 1; y++)
    {
        [unroll]
        for (int x = -1; x <= 1; x++)
        {
            float2 cell = cellId + float2(x, y);
            float seed = Hash11(cell);
            float alive = step(seed, density);
            float2 rnd = Hash22(cell + 3.7);
            float life = frac(time * speed * lerp(0.72, 1.28, Hash11(cell + 8.2)) + seed);

            float grow = smoothstep(0.0, 0.16, life);
            float swell = lerp(1.0, 1.28, smoothstep(0.6, 0.84, life));
            float fade = 1.0 - smoothstep(0.84, 1.0, life);
            float pulse = 1.0 + 0.08 * sin(life * 24.0 + seed * 30.0);
            float radius = lerp(0.12, 0.28, rnd.x) * grow * swell * pulse;
            float visible = fade * alive * step(0.02, radius);

            float2 center = rnd * 0.55 + 0.22;
            center.x += sin(life * 6.2831853 + seed * 20.0) * 0.05;
            center.y += life * 0.46;

            float2 delta = cellUv - (float2(x, y) + center);
            result = PickBubble(result, SampleBubbleSprite(delta, radius, visible));
        }
    }

    return result;
}

half4 UnlitFragment(Varyings input) : SV_Target
{
    float dotCount = max(_DotCount, 1.0);
    float dotSize = 1.0 / dotCount;
    float2 uv = (floor(input.uv * dotCount) + 0.5) * dotSize;
    float2 p = uv - 0.5;
    float phase = dot(TransformObjectToWorld(float3(0.0, 0.0, 0.0)).xy, float2(0.17, 0.13));
    float time = _Time.y + phase;

    float height = SurfaceHeight(p, time);
    float sampleStep = 0.006;
    float slopeX = SurfaceHeight(p + float2(sampleStep, 0.0), time) - height;
    float slopeY = SurfaceHeight(p + float2(0.0, sampleStep), time) - height;
    float3 normal = normalize(float3(-slopeX, -slopeY, 0.22));
    float2 wobble = clamp(float2(slopeX, slopeY) * 0.28, -0.02, 0.02);
    float2 liquidUv = (floor((uv + wobble) * dotCount) + 0.5) * dotSize;

    float width = max(_EdgeWidth, dotSize);
    float centerAlpha = SampleAlpha(liquidUv);
    float tight = NeighborMin(liquidUv, width);
    float wide = NeighborMin(liquidUv, width * 3.2);
    float shape = smoothstep(0.04, 0.22, centerAlpha);
    float edge = shape * smoothstep(0.3, 0.02, tight);
    float shore = shape * smoothstep(0.4, 0.05, wide) * (1.0 - edge);

    float3 keyLight = normalize(float3(-0.28, 0.62, 0.78));
    float3 fillLight = normalize(float3(0.55, -0.22, 0.8));
    float key = saturate(dot(normal, keyLight));
    float spec = pow(key, 22.0);
    float fillSpec = pow(saturate(dot(normal, fillLight)), 36.0);
    float depth = saturate(1.0 - length(p) * 1.9);
    float shade = saturate(0.22 + (1.0 - depth) * 0.48 + key * 0.4);
    float crest = smoothstep(0.42, 0.9, height * 0.5 + 0.5);

    half3 liquid = lerp(_DeepColor.rgb, _ShallowColor.rgb, shade);
    liquid = lerp(liquid, _ShallowColor.rgb, crest * 0.16);
    liquid *= lerp(1.0, 0.74, shore);

    half4 bubbles = BubbleLayer(uv, time, _BubbleScale, _BubbleSpeed, _BubbleDensity);
    half4 fizz = BubbleLayer(uv + 0.37, time + 2.4, _BubbleScale * 1.7, _BubbleSpeed * 1.45, saturate(_BubbleDensity * 0.85));
    bubbles = PickBubble(bubbles, fizz);

    half3 rgb = liquid;
    rgb = lerp(rgb, bubbles.rgb, bubbles.a);
    rgb += _GlowColor.rgb * spec * 0.85;
    rgb += _FoamColor.rgb * fillSpec * 0.4;
    rgb += (_GlowColor.rgb * 0.75 + _FoamColor.rgb * 0.35) * edge;

    float alpha = _LiquidAlpha * shape;
    alpha = max(alpha, bubbles.a * shape);
    alpha = max(alpha, edge * 0.92);
    alpha = max(alpha, spec * 0.35 * shape);
    alpha *= input.color.a;
    rgb *= input.color.rgb;
    return half4(rgb, saturate(alpha));
}

#endif
