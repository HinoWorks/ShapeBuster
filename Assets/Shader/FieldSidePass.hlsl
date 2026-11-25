#ifndef FIELD_SIDE_PASS_INCLUDED
#define FIELD_SIDE_PASS_INCLUDED

// フィールドの左右の帯。
// - 床（mat_Field_Back）と同じ大きさ・同じ位置のセルを、暗い色で敷く。セルはワールド座標で切るので、帯を伸ばしても床とずれない。
// - フィールドに接する内側のふちだけ、ほんのり明るくする。
// - 奥で、小さな粒が漂う（2 層。遠い層は小さく遅い）。
//   ワクチンを撒く前（_Cured = 0）は、くすんだ灰のような粒がゆっくり降る。
//   撒いたあと（_Cured = 1）は、淡い緑の光の粒がゆっくり上へ昇る。
// - マス目の上に、ステージごとの景色のドット絵（_SceneTex）を重ねる。ふちの光と粒は、景色の手前に重ねる。
//   置く場所と絵は BlockSession が SO_FieldScenery から決めて、MaterialPropertyBlock で渡す。

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
    float4 color : COLOR;
    float2 positionWS : TEXCOORD0;
};

CBUFFER_START(UnityPerMaterial)
    half4 _Color;
    float _CellSize;
    float4 _GridOrigin;
    float _ShadeMin;
    half4 _GlowColor;
    half4 _GlowColorBefore;
    float _GlowWidth;
    float _EdgeX;
    half4 _DustColor;
    float _DustDensity;
    float _DustSpeed;
    half4 _AshColor;
    float _AshDensity;
    float _AshSpeed;
    float _Cured;
    float4 _SceneRect;
    float _SceneOn;
    float _SceneTile;
    half _SceneBrightness;
    half _SceneOpacity;
CBUFFER_END

TEXTURE2D(_SceneTex);
SAMPLER(sampler_SceneTex);

Varyings UnlitVertex(Attributes input)
{
    Varyings output;
    float3 world = TransformObjectToWorld(input.positionOS);
    output.positionCS = TransformWorldToHClip(world);
    output.positionWS = world.xy;
    output.color = input.color;
    return output;
}

// 床（BlockCommon.hlsl）と同じ式。同じセルは同じ明るさになる。
float Hash21(float2 p)
{
    p = frac(p * float2(123.34, 345.45));
    p += dot(p, p + 34.345);
    return frac(p.x * p.y);
}

/// 粒の 1 層。spacing ごとのマスに 1 つまで粒を置き、時間とともに流す（speed が正なら上へ、負なら下へ）。
/// マスごとに、出るかどうか・位置・大きさ・明滅の速さを変える。twinkleAmp は明滅の強さ（0〜0.45）。
float DustLayer(float2 world, float spacing, float speed, float radius, float seed, float density, float twinkleAmp)
{
    float t = _Time.y;
    float2 p = world / spacing;
    p.y -= t * speed / spacing;
    float2 id = floor(p);
    float2 f = frac(p);

    float present = step(1.0 - density, Hash21(id + seed));
    float2 at = float2(Hash21(id + seed + 3.1), Hash21(id + seed + 7.7)) * 0.7 + 0.15;
    // 左右へゆっくり揺れる。
    at.x += sin(t * 0.35 + Hash21(id + seed + 1.3) * 6.28) * 0.12;
    float size = radius / spacing * lerp(0.6, 1.2, Hash21(id + seed + 5.9));
    float d = length(f - at);
    float spot = 1.0 - smoothstep(size * 0.3, size, d);
    // ゆっくり明るくなったり暗くなったりする。
    float twinkle = (1.0 - twinkleAmp) + twinkleAmp * sin(t * lerp(0.4, 1.1, Hash21(id + seed + 9.4)) + Hash21(id + seed + 2.2) * 6.28);
    return spot * present * twinkle;
}

half4 UnlitFragment(Varyings input) : SV_Target
{
    half4 tint = _Color * input.color;

    float2 grid = (input.positionWS - _GridOrigin.xy) / max(_CellSize, 0.001);
    float shade = lerp(_ShadeMin, 1.0, Hash21(floor(grid)));
    half3 color = tint.rgb * shade;

    // 景色：_SceneRect（左下の x, y と、幅, 高さ。ワールド）の中だけに貼る。空の部分は透明。
    // 幅が負なら、x は右の端で、絵を左右反転して貼る（右の帯に左の絵を使うとき）。
    if (_SceneOn > 0.5)
    {
        float2 sceneSize = _SceneRect.zw;
        sceneSize = (abs(sceneSize) < 0.001) ? float2(0.001, 0.001) : sceneSize;
        float2 sceneUV = (input.positionWS - _SceneRect.xy) / sceneSize;
        // 縦にくり返す絵（上から見下ろした町）は、帯の上から下まで敷く。
        if (_SceneTile > 0.5)
            sceneUV.y = frac(sceneUV.y);
        if (all(sceneUV >= 0.0) && all(sceneUV <= 1.0))
        {
            half4 scene = SAMPLE_TEXTURE2D(_SceneTex, sampler_SceneTex, sceneUV);
            color = lerp(color, scene.rgb * _SceneBrightness, scene.a * _SceneOpacity);
        }
    }

    // 内側のふち：フィールドの縁（_EdgeX）から離れるほど弱くなる光。
    float dist = abs(input.positionWS.x - _EdgeX);
    float glow = 1.0 - saturate(dist / max(_GlowWidth, 0.001));
    glow *= glow;
    half4 glowColor = lerp(_GlowColorBefore, _GlowColor, _Cured);
    color += glowColor.rgb * (glowColor.a * glow);

    // 粒。近い層と遠い層を重ねる。撒く前は灰が降り、撒いたあとは光が昇る。
    if (_Cured < 0.5)
    {
        float ash = DustLayer(input.positionWS, 1.3, -_AshSpeed, 0.06, 31.0, _AshDensity, 0.15)
                  + DustLayer(input.positionWS, 0.8, -_AshSpeed * 0.55, 0.035, 47.0, _AshDensity, 0.15) * 0.6;
        color += _AshColor.rgb * (_AshColor.a * saturate(ash));
    }
    else
    {
        float dust = DustLayer(input.positionWS, 1.3, _DustSpeed, 0.07, 0.0, _DustDensity, 0.45)
                   + DustLayer(input.positionWS, 0.8, _DustSpeed * 0.55, 0.04, 17.0, _DustDensity, 0.45) * 0.6;
        color += _DustColor.rgb * (_DustColor.a * saturate(dust));
    }

    return half4(color, tint.a);
}

#endif
