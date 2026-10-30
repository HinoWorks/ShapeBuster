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
    float2 beam : TEXCOORD1;
    float phase : TEXCOORD2;
};

TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);

CBUFFER_START(UnityPerMaterial)
    half4 _Color;
    half4 _CoreColor;
    half4 _EdgeColor;
    float _PixelSize;
    float _CoreWidth;
    float _GlowWidth;
    float _FlowScale;
    float _FlowSpeed;
    float _StreakWidth;
    float _StreakStrength;
    float _SparkRate;
    float _EndFade;
    float _EdgeAlpha;
CBUFFER_END

Varyings UnlitVertex(Attributes input)
{
    Varyings output;
    output.positionCS = TransformObjectToHClip(input.positionOS);
    output.uv = input.uv;
    output.color = input.color * _Color;

    float3 origin = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
    float2 axisX = TransformObjectToWorld(float3(1.0, 0.0, 0.0)).xy - origin.xy;
    float2 axisY = TransformObjectToWorld(float3(0.0, 1.0, 0.0)).xy - origin.xy;
    float lenX = max(length(axisX), 0.0001);
    float lenY = max(length(axisY), 0.0001);
    float2 delta = TransformObjectToWorld(input.positionOS).xy - origin.xy;
    output.beam = float2(dot(delta, axisX / lenX), dot(delta, axisY / lenY));
    output.phase = dot(origin.xy, float2(0.17, 0.13));
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

half4 UnlitFragment(Varyings input) : SV_Target
{
    float pixel = max(_PixelSize, 0.001);
    float2 cell = floor(input.beam / pixel + float2(0.0, 0.5));
    float dist = abs(cell.y) * pixel;
    float time = _Time.y + input.phase;

    float glowWidth = max(_GlowWidth, _CoreWidth + pixel);
    float falloff = 1.0 - smoothstep(_CoreWidth, glowWidth, dist);
    float core = step(dist, max(_CoreWidth, pixel * 0.51));

    float along = cell.x * pixel;
    float cycle = frac(along * _FlowScale - time * _FlowSpeed);
    float streak = step(cycle, saturate(_StreakWidth));
    float spark = step(0.94, Hash11(cell + floor(time * max(_SparkRate, 0.0))));
    spark *= step(dist, max(_CoreWidth, pixel) + pixel);
    float grain = lerp(0.84, 1.0, Hash11(cell + floor(time * 8.0)));

    half3 beam = input.color.rgb;
    half3 edge = _EdgeColor.rgb * beam;
    half3 rgb = lerp(edge, beam, saturate(falloff));
    rgb *= lerp(grain, 1.0, core);
    rgb = lerp(rgb, _CoreColor.rgb, core);
    rgb = lerp(rgb, _CoreColor.rgb, streak * _StreakStrength * saturate(falloff + 0.2));
    rgb = lerp(rgb, _CoreColor.rgb, spark);

    float end = min(input.uv.x, 1.0 - input.uv.x);
    float cap = smoothstep(0.0, max(_EndFade, 0.0001), end);
    float shape = SampleAlpha(input.uv);
    float alpha = lerp(_EdgeAlpha, 1.0, saturate(falloff));
    alpha = max(alpha, core);
    alpha *= shape * cap * input.color.a;
    return half4(rgb, saturate(alpha));
}

#endif
