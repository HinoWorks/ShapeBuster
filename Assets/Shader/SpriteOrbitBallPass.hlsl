#ifndef SPRITE_ORBIT_BALL_PASS_INCLUDED
#define SPRITE_ORBIT_BALL_PASS_INCLUDED

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
    half4 _MoonColor;
    half4 _TrackColor;
    float _PixelSize;
    float _Radius;
    float _CoreRadius;
    float _OrbitRadius;
    float _MoonSize;
    float _OrbitSpeed;
    float _TrackWidth;
    float _TrackAlpha;
CBUFFER_END

Varyings UnlitVertex(Attributes input)
{
    Varyings output;
    output.positionCS = TransformObjectToHClip(input.positionOS);
    output.uv = input.uv;
    output.color = input.color;
    return output;
}

half4 UnlitFragment(Varyings input) : SV_Target
{
    float2 p = input.uv * 2.0 - 1.0;
    float pixel = max(_PixelSize, 0.02);
    float2 cell = floor(p / pixel + 0.5) * pixel;
    float dist = length(cell);
    float radius = saturate(_Radius);
    float coreRadius = min(saturate(_CoreRadius), radius);
    float orbitRadius = max(saturate(_OrbitRadius), radius + pixel);
    float time = _Time.y;

    float body = step(dist, radius);
    float core = step(dist, coreRadius);
    float shade = 1.0 - smoothstep(coreRadius, radius, dist);
    float track = step(abs(dist - orbitRadius), max(_TrackWidth, pixel * 0.5));

    float moon = 0.0;
    float moonCore = 0.0;
    float moonRadius = max(_MoonSize, pixel * 0.5);
    [unroll]
    for (int i = 0; i < 3; i++)
    {
        float angle = time * _OrbitSpeed + i * 2.0943951;
        float2 center = float2(cos(angle), sin(angle)) * orbitRadius;
        center = floor(center / pixel + 0.5) * pixel;
        float moonDist = length(cell - center);
        moon = max(moon, step(moonDist, moonRadius));
        moonCore = max(moonCore, step(moonDist, moonRadius * 0.45));
    }

    half3 rgb = lerp(_EdgeColor.rgb, _Color.rgb, saturate(shade));
    rgb = lerp(rgb, _CoreColor.rgb, core);
    rgb = lerp(rgb, _TrackColor.rgb, track * (1.0 - moon));
    rgb = lerp(rgb, _MoonColor.rgb, moon);
    rgb = lerp(rgb, _CoreColor.rgb, moonCore);
    rgb *= input.color.rgb;

    float alpha = body * lerp(0.45, 1.0, saturate(shade));
    alpha = max(alpha, track * _TrackAlpha);
    alpha = max(alpha, moon);
    alpha *= input.color.a;
    return half4(rgb, alpha);
}

#endif
