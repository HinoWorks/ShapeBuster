#ifndef SPRITE_GLASS_SHIELD_PASS_INCLUDED
#define SPRITE_GLASS_SHIELD_PASS_INCLUDED

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
    float fillRatio : TEXCOORD1;
};

TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);

CBUFFER_START(UnityPerMaterial)
    half4 _Color;
    half4 _OutlineColor;
    float _OutlineWidth;
    half4 _GlassColor;
    float _GlassAlpha;
    float _GlassShade;
    float _GlassLine;
    half4 _ShineColor;
    float _ShineInterval;
    float _ShineDuration;
    float _ShineWidth;
    float _ShineStrength;
    float4 _ShineDirection;
    float _SheenStrength;
    float _Flash;
CBUFFER_END

Varyings UnlitVertex(Attributes input)
{
    Varyings output;
    output.positionCS = TransformObjectToHClip(input.positionOS);
    output.uv = input.uv;
    // SpriteRenderer.color.a は敵ごとの塗り割合。マテリアルに置くと全敵で共有される。
    output.fillRatio = input.color.a;
    output.color = half4(input.color.rgb, 1.0) * _Color;
    return output;
}

float SampleAlpha(float2 uv)
{
    float2 inside = step(float2(0.0, 0.0), uv) * step(uv, float2(1.0, 1.0));
    return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, saturate(uv)).a * inside.x * inside.y;
}

half3 GlassBody(float2 uv, half3 tint)
{
    float shade = lerp(_GlassShade, 1.0, saturate(uv.y));
    half3 rgb = tint * shade;

    float lines = 0.5 + 0.5 * sin((uv.y + uv.x * 0.12) * 34.0);
    rgb += tint * lines * _GlassLine;
    return rgb;
}

float StaticSheen(float2 uv)
{
    float coord = uv.x * 0.28 + uv.y;
    float band = saturate(1.0 - abs(coord - 0.78) / 0.14);
    return band * band * _SheenStrength;
}

float TravelingShine(float2 uv)
{
    float interval = max(_ShineInterval, 0.05);
    float duration = clamp(_ShineDuration, 0.05, interval);
    float phase = frac(_Time.y / interval);
    float window = duration / interval;
    float t = saturate(phase / window);
    float envelope = sin(t * 3.14159265);

    float2 dir = _ShineDirection.xy;
    float span = max(abs(dir.x) + abs(dir.y), 0.001);
    float coord = dot(uv, dir) / span;
    float center = lerp(-0.2, 1.2, t);
    float dist = abs(coord - center);
    float width = max(_ShineWidth, 0.01);
    float band = saturate(1.0 - dist / width);
    float core = saturate(1.0 - dist / (width * 0.32));
    float gleam = band * band * 0.55 + core * core;
    return gleam * envelope * step(phase, window) * _ShineStrength;
}

half4 UnlitFragment(Varyings input) : SV_Target
{
    float2 uv = input.uv;
    float centerA = SampleAlpha(uv);
    float width = max(_OutlineWidth, 0.0001);
    float diagonal = width * 0.70710678;

    float neighborMin = SampleAlpha(uv + float2(width, 0.0));
    neighborMin = min(neighborMin, SampleAlpha(uv + float2(-width, 0.0)));
    neighborMin = min(neighborMin, SampleAlpha(uv + float2(0.0, width)));
    neighborMin = min(neighborMin, SampleAlpha(uv + float2(0.0, -width)));
    neighborMin = min(neighborMin, SampleAlpha(uv + float2(diagonal, diagonal)));
    neighborMin = min(neighborMin, SampleAlpha(uv + float2(diagonal, -diagonal)));
    neighborMin = min(neighborMin, SampleAlpha(uv + float2(-diagonal, diagonal)));
    neighborMin = min(neighborMin, SampleAlpha(uv + float2(-diagonal, -diagonal)));

    float isOutline = step(0.01, centerA - neighborMin);
    float isFilled = step(uv.y, input.fillRatio);
    float mask = max(isOutline, (1.0 - isOutline) * isFilled);

    half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, saturate(uv));
    half3 tint = _GlassColor.rgb * input.color.rgb;
    half3 rgb = GlassBody(uv, tint);

    float rim = saturate(1.0 - neighborMin) * (1.0 - isOutline);
    rgb += tint * rim * 0.65;

    float surface = saturate(1.0 - abs(uv.y - max(input.fillRatio - 0.02, 0.0)) / 0.04);
    surface *= isFilled * (1.0 - isOutline);
    rgb += half3(1.0, 1.0, 1.0) * surface * 0.55;

    float shine = StaticSheen(uv) + TravelingShine(uv);
    shine *= isFilled;
    rgb += _ShineColor.rgb * shine;

    rgb = lerp(rgb, _OutlineColor.rgb, isOutline);
    rgb = lerp(rgb, half3(1.0, 1.0, 1.0), saturate(_Flash));

    float alpha = _GlassAlpha;
    alpha = lerp(alpha, max(alpha, 0.85), rim);
    alpha = max(alpha, surface * 0.9);
    alpha = max(alpha, saturate(shine) * 0.75);
    alpha = lerp(alpha, _OutlineColor.a, isOutline);
    alpha *= tex.a * mask;
    return half4(rgb, alpha);
}

#endif
