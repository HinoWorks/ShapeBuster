#ifndef FIELD_FLOOR_PASS_INCLUDED
#define FIELD_FLOOR_PASS_INCLUDED

// フィールドの床。ブロックのマスと同じ大きさ・同じ位置のマスを、ほんの少しずつ違う明るさで敷く。
// プレイ中は BlockSession が、グローバルの _FieldCellSize（ブロックのマスの大きさ）と
// _FieldGridOrigin（ブロックの列の左上）を入れる。まだ入っていないとき（タイトルなど）はマテリアルの値を使う。
// 左右の帯（FieldSide）も同じ値でマスを切るので、床と帯のマスはつながって見える。
// _FineOn がオンなら、マスの中に、ブロックと同じくらいの細かいドットのむらを薄く重ねる（オフでマスだけの床に戻る）。

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
    float2 uv : TEXCOORD0;
    float2 positionWS : TEXCOORD1;
};

TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);

CBUFFER_START(UnityPerMaterial)
    half4 _Color;
    half4 _FillColor;
    float _CellSize;
    float4 _GridOrigin;
    float _ShadeMin;
    float _FineOn;
    float _FineDots;
    float _FineShadeMin;
CBUFFER_END

// BlockSession が入れるグローバルの値（左右の帯と同じ）。
float _FieldCellSize;
float4 _FieldGridOrigin;

Varyings UnlitVertex(Attributes input)
{
    Varyings output;
    float3 world = TransformObjectToWorld(input.positionOS);
    output.positionCS = TransformWorldToHClip(world);
    output.positionWS = world.xy;
    output.uv = input.uv;
    output.color = input.color;
    return output;
}

// 左右の帯（FieldSidePass.hlsl）と同じ式。
float Hash21(float2 p)
{
    p = frac(p * float2(123.34, 345.45));
    p += dot(p, p + 34.345);
    return frac(p.x * p.y);
}

half4 UnlitFragment(Varyings input) : SV_Target
{
    half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
    half4 tint = _FillColor * _Color * input.color * tex;

    bool useGlobal = _FieldCellSize > 0.0001;
    float size = useGlobal ? _FieldCellSize : max(_CellSize, 0.001);
    float2 origin = useGlobal ? _FieldGridOrigin.xy : _GridOrigin.xy;
    float2 grid = (input.positionWS - origin) / size;
    float shade = lerp(_ShadeMin, 1.0, Hash21(floor(grid)));
    // 細かいドット：マスを _FineDots 等分したドットごとに、ほんの少し明るさを変える（マスの線とそろう）。
    if (_FineOn > 0.5)
    {
        float2 fine = floor(grid * max(round(_FineDots), 1.0));
        shade *= lerp(_FineShadeMin, 1.0, Hash21(fine + 71.3));
    }
    return half4(tint.rgb * shade, tint.a);
}

#endif
