#ifndef SPRITE_TURRET_SHOT_PASS_INCLUDED
#define SPRITE_TURRET_SHOT_PASS_INCLUDED

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
    half4 _HighlightColor;
    half4 _Color;
    half4 _ShadowColor;
    half4 _EdgeColor;
    half4 _OutlineColor;
    float _PixelSize;
    float _Size;
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
    // Knob スプライトは八角形のメッシュで、p はおよそ ±0.6 までしか描かれない。外の線まで収まるよう _Size は 0.4 以下にする
    float2 p = input.uv * 2.0 - 1.0;
    float pixel = max(_PixelSize, 0.02);
    // ドットの中心で色を決める（ドットの格子にそろえる）
    float2 cell = floor(p / pixel + 0.5) * pixel;
    float size = max(_Size, 0.001);

    // ひし形は、縦と横の距離の和で決まる
    float d = abs(cell.x) + abs(cell.y);
    float body = step(d, size);

    // 4 つの面で明るさを分ける：左上がいちばん明るく、右下がいちばん暗い
    float upper = step(0.0, cell.y);
    float left = step(cell.x, 0.0);
    half3 rgb = _Color.rgb;
    rgb = lerp(rgb, _HighlightColor.rgb, upper * left);
    rgb = lerp(rgb, _ShadowColor.rgb, (1.0 - upper) * (1.0 - left));

    // 右下の 2 辺の、いちばん外の 1 ドットを縁の色にして厚みを出す
    float lowerRight = step(0.0, cell.x - cell.y);
    float rim = body * step(size - pixel * 1.1, d) * lowerRight;
    rgb = lerp(rgb, _EdgeColor.rgb, rim);

    // まわりを 1 ドットの暗い線で囲む
    float outline = (1.0 - body) * step(d, size + pixel);
    rgb = lerp(rgb, _OutlineColor.rgb, outline);
    rgb *= input.color.rgb;

    float alpha = saturate(body + outline * _OutlineAlpha);
    alpha *= input.color.a;
    return half4(rgb, alpha);
}

#endif
