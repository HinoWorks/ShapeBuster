#ifndef SPRITE_LASER_BALL_PASS_INCLUDED
#define SPRITE_LASER_BALL_PASS_INCLUDED

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
    float _PixelSize;
    float _Radius;
    float _CoreWidth;
    float _GlowWidth;
    float _FlowScale;
    float _FlowSpeed;
    float _StreakWidth;
    float _StreakStrength;
    float _SparkRate;
    float _BeamAlpha;
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
    float axial = abs(cell.x);

    float body = step(dist, radius);
    float coreWidth = max(_CoreWidth, pixel);
    float core = body * step(axial, coreWidth);
    float glow = 1.0 - smoothstep(coreWidth, max(_GlowWidth, coreWidth + pixel), axial);
    float along = cell.y;
    float cycle = frac(along * _FlowScale - time * max(_FlowSpeed, 0.0));
    float streak = body * step(cycle, saturate(_StreakWidth));
    float spark = step(0.93, Hash11(floor(p / pixel) + floor(time * max(_SparkRate, 0.0))));
    spark *= core;
    float beam = step(axial, coreWidth) * (1.0 - body);

    half3 rgb = lerp(_EdgeColor.rgb, _Color.rgb, saturate(glow));
    rgb = lerp(rgb, _CoreColor.rgb, core);
    rgb = lerp(rgb, _CoreColor.rgb, streak * _StreakStrength * saturate(glow));
    rgb = lerp(rgb, _CoreColor.rgb, spark);
    rgb = lerp(rgb, _CoreColor.rgb, beam);
    rgb *= input.color.rgb;

    float alpha = saturate(body * lerp(0.35, 1.0, saturate(glow)) + beam * _BeamAlpha);
    alpha *= input.color.a;
    return half4(rgb, alpha);
}

#endif
