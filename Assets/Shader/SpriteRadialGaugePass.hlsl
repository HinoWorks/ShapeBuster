#ifndef SPRITE_RADIAL_GAUGE_PASS_INCLUDED
#define SPRITE_RADIAL_GAUGE_PASS_INCLUDED

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
    float _InnerRadius;
    float _OuterRadius;
    float _Edge;
CBUFFER_END

Varyings UnlitVertex(Attributes input)
{
    Varyings output;
    output.positionCS = TransformObjectToHClip(input.positionOS);
    output.uv = input.uv;
    // SpriteRenderer.color.a は個体ごとの進捗。マテリアルに置くと全カーソルで共有される。
    output.fillRatio = input.color.a;
    output.color = half4(input.color.rgb, 1.0);
    return output;
}

half4 UnlitFragment(Varyings input) : SV_Target
{
    float2 d = input.uv - 0.5;
    float radius = length(d);
    float fill = saturate(input.fillRatio);

    float ring = smoothstep(_InnerRadius - 0.008, _InnerRadius, radius);
    ring *= 1.0 - smoothstep(_OuterRadius, _OuterRadius + 0.008, radius);

    // 上を 0 にして時計回り。frac で左上の負角を 0.5–1 に畳む。
    float turn = atan2(d.x, d.y) * (1.0 / (2.0 * PI));
    float arc = saturate((fill - frac(turn)) / max(_Edge, 0.0001));
    arc = lerp(arc, 1.0, step(0.999, fill));
    arc *= step(0.001, fill);

    half spriteA = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, saturate(input.uv)).a;
    half alpha = spriteA * ring * arc * _Color.a;
    return half4(input.color.rgb * _Color.rgb, alpha);
}

#endif
