#ifndef SPRITE_HEAVY_BALL_PASS_INCLUDED
#define SPRITE_HEAVY_BALL_PASS_INCLUDED

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
    half4 _RimColor;
    half4 _BandColor;
    half4 _RivetColor;
    half4 _OutlineColor;
    float _PixelSize;
    float _Radius;
    float _Bands;
    float _HighlightSize;
    float _BandWidth;
    float _BandTilt;
    float _RivetCount;
    float _RollSpeed;
    float _GlintSpeed;
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
    float2 cell = floor(p / pixel + 0.5) * pixel;
    float dist = length(cell);
    float radius = max(saturate(_Radius), 0.001);
    float time = _Time.y;

    // ドットごとに球の向きを出す（光は左上の手前から）
    float2 c = cell / radius;
    float3 n = float3(c, sqrt(saturate(1.0 - dot(c, c))));
    float3 lightDir = normalize(float3(-0.55, 0.6, 0.6));
    float ndl = saturate(dot(n, lightDir));
    float bands = max(floor(_Bands), 1.0);
    float shade = floor(ndl * bands + 0.5) / bands;

    float body = step(dist, radius);
    float rim = body * step(radius - pixel * 1.1, dist);

    // 右下の縁だけ、床からの照り返しで少し明るい
    float2 dir = dist > 0.0001 ? cell / dist : float2(0.0, 0.0);
    float lowerRight = step(0.6, dot(dir, normalize(float2(0.7, -0.7))));

    // 少し傾いた帯と、帯に沿ってゆっくり回る鋲
    float3 axis = normalize(float3(-_BandTilt * 0.6, 1.0, 0.35));
    float band = body * (1.0 - rim) * step(abs(dot(n, axis)), _BandWidth);
    float3 side = normalize(cross(float3(0.0, 0.0, 1.0), axis));
    float3 up = cross(axis, side);
    float angle = atan2(dot(n, up), dot(n, side)) / (2.0 * PI) + time * _RollSpeed * 0.1;
    float count = floor(_RivetCount);
    float rivet = band * step(0.5, count) * step(abs(frac(angle * count) - 0.5), 0.14)
        * step(abs(dot(n, axis)), _BandWidth * 0.55) * step(0.45, n.z);

    // 照りは左上の小さな点。ゆっくり大きさがゆらぐ
    float3 halfDir = normalize(lightDir + float3(0.0, 0.0, 1.0));
    float glintSize = _HighlightSize * (1.0 + 0.35 * sin(time * _GlintSpeed));
    float highlight = body * (1.0 - rim) * step(1.0 - glintSize, dot(n, halfDir));

    half3 rgb = lerp(_ShadowColor.rgb, _Color.rgb, shade);
    rgb = lerp(rgb, _BandColor.rgb * (0.55 + 0.7 * shade), band);
    rgb = lerp(rgb, _RivetColor.rgb * (0.45 + 0.75 * shade), rivet);
    rgb = lerp(rgb, _HighlightColor.rgb, highlight);
    rgb = lerp(rgb, lerp(_EdgeColor.rgb, _RimColor.rgb, lowerRight), rim);

    // 玉のすぐ外を 1 ドット分だけ明るい線で囲み、暗い床の上でも見えるようにする
    float outline = (1.0 - body) * step(dist, radius + pixel);
    float upperLeft = saturate(dot(dir, normalize(float2(-0.7, 0.7))) * 0.5 + 0.5);
    rgb = lerp(rgb, _OutlineColor.rgb, outline);
    rgb *= input.color.rgb;

    float alpha = saturate(body + outline * _OutlineAlpha * lerp(0.45, 1.0, upperLeft));
    alpha *= input.color.a;
    return half4(rgb, alpha);
}

#endif
