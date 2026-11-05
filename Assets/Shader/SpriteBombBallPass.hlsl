#ifndef SPRITE_BOMB_BALL_PASS_INCLUDED
#define SPRITE_BOMB_BALL_PASS_INCLUDED

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
    half4 _RingColor;
    float _PixelSize;
    float _Radius;
    float _CoreRadius;
    float _PulseSpeed;
    float _RingThickness;
    float _RingStrength;
    float _EmberRate;
    float _EmberStrength;
    float _Glow;
    float _GlowAlpha;
CBUFFER_END

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

half4 UnlitFragment(Varyings input) : SV_Target
{
    float2 p = input.uv * 2.0 - 1.0;
    float pixel = max(_PixelSize, 0.02);
    float2 cell = floor(p / pixel + 0.5) * pixel;
    float dist = length(cell);
    float radius = saturate(_Radius);
    float time = _Time.y;

    float body = step(dist, radius);
    float rim = body * step(radius - pixel * 1.6, dist);
    float core = step(dist, min(_CoreRadius, radius - pixel));
    float pulse = frac(time * max(_PulseSpeed, 0.01));
    float ringDist = abs(dist - pulse * radius);
    float ring = step(ringDist, max(_RingThickness, pixel)) * step(dist, radius + pixel);
    float ember = step(0.84, Hash11(floor(p / pixel) + floor(time * max(_EmberRate, 0.0))));
    ember *= body * (1.0 - core) * (1.0 - rim);

    half3 rgb = lerp(_EdgeColor.rgb, _Color.rgb, saturate(1.0 - dist / max(radius, 0.001)));
    rgb = lerp(rgb, _EdgeColor.rgb * 0.55, rim);
    rgb = lerp(rgb, _CoreColor.rgb, core);
    rgb = lerp(rgb, _RingColor.rgb, ring * (1.0 - pulse) * _RingStrength);
    rgb = lerp(rgb, _CoreColor.rgb, ember * _EmberStrength);
    rgb *= input.color.rgb;

    float glow = (1.0 - body) * (1.0 - smoothstep(radius, radius + max(_Glow, pixel), dist));
    float alpha = saturate(body + glow * _GlowAlpha + ring * (1.0 - body) * 0.35);
    alpha *= input.color.a;
    return half4(rgb, alpha);
}

#endif
