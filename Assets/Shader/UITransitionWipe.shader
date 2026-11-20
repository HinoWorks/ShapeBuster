// 画面切り替えの幕。画面をマス目に分け、マスごとに少しずつずれて四角く閉じる・開く。
// _Progress が 0 で全部開き、1 で全部閉じる。マスが閉じる順番は _Pattern で変わる。
//   0：斜めに流れる   1：真ん中から広がる   2：ランダムに埋まる
//   3：上から降る     4：ブラインド（段ごとに左右交互）
// _Mirror が 1 なら左右を反転する。閉じきっている間は、真ん中で小さなゾンビが跳ねる（_Loading）。
// 跳ねる時間は _LoadingTime（閉じきってからの秒数）で数えるので、毎回地面から跳ね始める。
Shader "ShapeBuster/UITransitionWipe"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Base", Color) = (0.09, 0.1, 0.11, 1)
        _Accent ("Accent", Color) = (0.64, 0.72, 0.6, 1)
        _Progress ("Progress", Range(0, 1)) = 0
        _Pattern ("Pattern", Float) = 0
        _Mirror ("Mirror", Float) = 0
        _Seed ("Seed", Float) = 0
        _CellsX ("Cells X", Float) = 20
        _Aspect ("Aspect", Float) = 1.7778
        _Spread ("Spread", Range(0, 0.95)) = 0.7
        _Loading ("Loading", Range(0, 1)) = 0
        _LoadingTime ("Loading Time", Float) = 0
        _HopSpeed ("Hop Speed", Float) = 8
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Overlay"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            fixed4 _Color;
            fixed4 _Accent;
            float _Progress;
            float _Pattern;
            float _Mirror;
            float _Seed;
            float _CellsX;
            float _Aspect;
            float _Spread;
            float _Loading;
            float _LoadingTime;
            float _HopSpeed;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21) + _Seed);
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            // マスが閉じ始める順番（0 が最初、1 が最後）。
            float Delay(float2 cell, float2 grid)
            {
                float2 p = (cell + 0.5) / grid;
                if (_Mirror > 0.5)
                    p.x = 1.0 - p.x;
                int pattern = (int)round(_Pattern);

                if (pattern == 1)
                {
                    float2 q = (p - 0.5) * float2(_Aspect, 1.0);
                    return saturate(length(q) / length(float2(_Aspect, 1.0) * 0.5));
                }
                if (pattern == 2)
                    return Hash(cell);
                if (pattern == 3)
                    return saturate((1.0 - p.y) * 0.8 + Hash(float2(cell.x, 7.0)) * 0.2);
                if (pattern == 4)
                {
                    float row = fmod(cell.y, 2.0);
                    float x = row < 0.5 ? p.x : 1.0 - p.x;
                    return saturate(x * 0.85 + (1.0 - p.y) * 0.15);
                }
                // 0：左上から右下へ斜めに流れる。
                return saturate((p.x * _Aspect + (1.0 - p.y)) / (_Aspect + 1.0));
            }

            // 閉じている間に真ん中で跳ねる、小さなゾンビ。8×8 ドット。
            fixed4 LoadingIcon(float2 uv, fixed4 color)
            {
                if (_Loading <= 0.001)
                    return color;
                float2 q = (uv - 0.5) * float2(_Aspect, 1.0);
                float size = 0.1;
                float hop = abs(sin(_LoadingTime * _HopSpeed)) * 0.06;
                float2 local = (q - float2(0.0, hop - 0.01)) / size;
                // 影
                float2 s = (q - float2(0.0, -size * 0.62)) / float2(size * (0.9 - hop * 4.0), size * 0.12);
                if (dot(s, s) < 1.0)
                    color.rgb = lerp(color.rgb, color.rgb * 0.55, _Loading);
                if (abs(local.x) > 0.5 || abs(local.y) > 0.5)
                    return color;

                float2 dot8 = floor((local + 0.5) * 8.0);
                fixed3 body = _Accent.rgb;
                // 縁
                if (dot8.x < 1.0 || dot8.x > 6.0 || dot8.y < 1.0 || dot8.y > 6.0)
                    body *= 0.6;
                // 目
                if (dot8.y == 4.0 && (dot8.x == 2.0 || dot8.x == 5.0))
                    body = fixed3(0.95, 0.9, 0.45);
                // 口
                if (dot8.y == 2.0 && dot8.x >= 2.0 && dot8.x <= 5.0)
                    body = fixed3(0.18, 0.16, 0.14);
                color.rgb = lerp(color.rgb, body, _Loading);
                return color;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 grid = float2(_CellsX, max(1.0, round(_CellsX / max(0.1, _Aspect))));
                float2 scaled = i.uv * grid;
                float2 cell = floor(scaled);
                float2 inner = frac(scaled) - 0.5;

                float spread = saturate(_Spread);
                float local = saturate((_Progress - Delay(cell, grid) * spread) / max(0.0001, 1.0 - spread));
                // ドット絵らしく、四角の大きさは 5 段階で変わる。
                float stepSize = ceil(local * 5.0) / 5.0;
                float edge = max(abs(inner.x), abs(inner.y)) * 2.0;
                if (local <= 0.0 || edge > stepSize + 0.001)
                    discard;

                fixed4 color = _Color;
                color.rgb *= 0.92 + Hash(cell + 3.1) * 0.08;
                // 閉じかけのマスは差し色に寄せて、波の先がわかるようにする。
                float moving = step(0.001, local) * step(local, 0.999);
                color.rgb = lerp(color.rgb, _Accent.rgb, moving * 0.45);
                color = LoadingIcon(i.uv, color);
                color.a *= i.color.a;
                return color;
            }
            ENDCG
        }
    }
}
