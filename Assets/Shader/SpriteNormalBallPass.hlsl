#ifndef SPRITE_NORMAL_BALL_PASS_INCLUDED
#define SPRITE_NORMAL_BALL_PASS_INCLUDED

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
    half4 _ShadowColor;
    half4 _EdgeColor;
    half4 _HighlightColor;
    half4 _OutlineColor;
    float _PixelSize;
    float _Radius;
    float _Bands;
    float _HighlightSize;
    float _OutlineAlpha;
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
    // Knob スプライトは八角形のメッシュで、p はおよそ ±0.6 までしか描かれない。外の線まで収まるよう _Radius は 0.5 前後にする
    float2 p = input.uv * 2.0 - 1.0;
    float pixel = max(_PixelSize, 0.02);
    // ドットの中心で色を決める（ドットの格子にそろえる）
    float2 cell = floor(p / pixel + 0.5) * pixel;
    float dist = length(cell);
    float radius = max(saturate(_Radius), 0.001);

    // ドットごとに球の向きを出し、明るさを段に分ける（光は左上の手前から）
    float2 c = cell / radius;
    float3 n = float3(c, sqrt(saturate(1.0 - dot(c, c))));
    float3 lightDir = normalize(float3(-0.55, 0.6, 0.6));
    float ndl = saturate(dot(n, lightDir));
    float bands = max(floor(_Bands), 1.0);
    float shade = floor(ndl * bands + 0.5) / bands;

    float body = step(dist, radius);
    // 玉の内側のいちばん外の 1 ドットは、右下だけ縁の色にして丸みを出す
    float2 dir = dist > 0.0001 ? cell / dist : float2(0.0, 0.0);
    float lowerRight = step(0.0, dot(dir, normalize(float2(0.7, -0.7))));
    float rim = body * step(radius - pixel * 1.1, dist) * lowerRight;

    // 照りは左上の小さな点
    float3 halfDir = normalize(lightDir + float3(0.0, 0.0, 1.0));
    float highlight = body * (1.0 - rim) * step(1.0 - _HighlightSize, dot(n, halfDir));

    half3 rgb = lerp(_ShadowColor.rgb, _Color.rgb, shade);
    rgb = lerp(rgb, _HighlightColor.rgb, highlight);
    rgb = lerp(rgb, _EdgeColor.rgb, rim);

    // 玉のすぐ外を 1 ドット分だけ暗い線で囲み、明るい床の上でも形が分かるようにする
    float outline = (1.0 - body) * step(dist, radius + pixel);
    rgb = lerp(rgb, _OutlineColor.rgb, outline);
    rgb *= input.color.rgb;

    float alpha = saturate(body + outline * _OutlineAlpha);
    alpha *= input.color.a;
    return half4(rgb, alpha);
}

#endif
