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
    // 0度はビームの上下。90度は左右。角度は画面上の見た目に合わせる。
    float rad = radians(_GradientAngle);
    float2 axis = float2(sin(rad), cos(rad));
    float2 halfSize = float2(length(unity_ObjectToWorld[0].xyz), length(unity_ObjectToWorld[1].xyz)) * 0.5;
    halfSize = max(halfSize, 0.0001);
    float2 local = (input.uv * 2.0 - 1.0) * halfSize;
    float reach = dot(halfSize, abs(axis));
    float along = abs(dot(local, axis)) / max(reach, 0.0001);
    float end = pow(saturate(along), max(_EndPower, 0.01));
    half3 tint = _Color.rgb * input.color.rgb;
    half3 rgb = lerp(half3(1.0, 1.0, 1.0), tint, end);
    float alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a * input.color.a * _Color.a;
    return half4(rgb, alpha);
}

#endif
