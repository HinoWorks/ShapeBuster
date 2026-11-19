// 装甲ブロックの殻の盾。ブロックの外へはみ出して描く。
// 絵はテクスチャを使わず、ドット単位で計算する。位置と大きさは BlockPiece が決める。
// _ShellMode：1 = 下（弧の形で、左右に 1 ドット、下に 2 ドットはみ出す）、2 = 左、3 = 右（外へ 1 ドット膨らむ）。
Shader "ShapeBuster/BlockShell"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _ShellColor ("殻の色", Color) = (0.98, 0.78, 0.12, 1)
        _ShellDark ("殻の縁取り", Color) = (0.3, 0.17, 0.03, 1)
        _ShellLight ("殻のハイライト", Color) = (1, 0.96, 0.7, 1)
        [HideInInspector] _Dots ("ドット数", Vector) = (10, 6, 0, 0)
        [HideInInspector] _ShellTh ("殻の高さ", Float) = 5
        [HideInInspector] _ShellMode ("殻の向き", Float) = 1
        [HideInInspector] _Flash ("Flash", Range(0, 1)) = 0
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "CanUseSpriteAtlas" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        struct Attributes
        {
            float3 positionOS : POSITION;
            float2 uv : TEXCOORD0;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
        };

        CBUFFER_START(UnityPerMaterial)
            half4 _ShellColor;
            half4 _ShellDark;
            half4 _ShellLight;
            float4 _Dots;
            float _ShellTh;
            float _ShellMode;
            float _Flash;
        CBUFFER_END

        // ドットの種類。
        #define SHELL_NONE 0
        #define SHELL_BODY 1
        #define SHELL_EDGE 2
        #define SHELL_RIDGE 3
        #define SHELL_LIGHT 4
        #define SHELL_INNER 5

        Varyings ShellVertex(Attributes input)
        {
            Varyings output;
            output.positionCS = TransformObjectToHClip(input.positionOS);
            output.uv = input.uv;
            return output;
        }

        /// 下の殻の高さ。中央が高い弧にする。
        float BottomHeight(float along, float span, float th)
        {
            float bump = max(1.0, round(th * 0.4));
            float t = ((along + 0.5) / span) * 2.0 - 1.0;
            return th - bump + round((1.0 - t * t) * bump);
        }

        /// 下の殻。k は下端から、along は左端から数えたドット。
        int BottomCell(float k, float along, float span, float th)
        {
            float h = BottomHeight(along, span, th);
            if (k > h + 0.5) return SHELL_NONE;

            float hl = along >= 1.0 ? BottomHeight(along - 1.0, span, th) : -1.0;
            float hr = along <= span - 2.0 ? BottomHeight(along + 1.0, span, th) : -1.0;
            bool edge = k >= h - 0.5 || along < 0.5 || along > span - 1.5 || k < 0.5
                || (hl >= 0.0 && k >= hl - 0.5) || (hr >= 0.0 && k >= hr - 0.5);
            if (edge) return SHELL_EDGE;
            // アイコンの貝殻と同じ、下寄りの合わせ目。
            if (k < 1.5) return SHELL_INNER;
            if (abs(along - floor(span * 0.3)) < 0.25 && abs(k - (h - 1.0)) < 0.25) return SHELL_LIGHT;

            // 下の中央から扇状に広がる筋。4 ドットに 1 本くらいの間隔。
            float stripes = max(3.0, round(span * 0.25));
            float ang = atan2(along + 0.5 - span * 0.5, k + 1.5) / 3.14159265;
            return frac(ang * stripes + 0.25) >= 0.5 ? SHELL_RIDGE : SHELL_BODY;
        }

        /// 左右の殻。k は外側の縁から、along は下端から数えたドット。inner は内側の縁の列。
        int SideCell(float k, float along, float span, float inner)
        {
            float t = ((along + 0.5) / span) * 2.0 - 1.0;
            float start = 1.0 - round(1.0 - t * t);
            if (k < start - 0.5) return SHELL_NONE;
            if (k < start + 0.5 || along < 0.5 || along > span - 1.5) return SHELL_EDGE;
            if (k > inner - 0.5) return SHELL_INNER;
            if (abs(along - floor(span * 0.7)) < 0.25 && abs(k - (start + 1.0)) < 0.25) return SHELL_LIGHT;
            // 横の筋は 4 ドットに 1 本。
            return frac(along * 0.25) >= 0.75 ? SHELL_RIDGE : SHELL_BODY;
        }

        half4 ShellFragment(Varyings input) : SV_Target
        {
            float2 dots = max(_Dots.xy, float2(1.0, 1.0));
            float2 cell = min(floor(input.uv * dots), dots - 1.0);

            int kind;
            if (_ShellMode < 1.5)
            {
                kind = BottomCell(cell.y, cell.x, dots.x, _ShellTh);
            }
            else
            {
                float k = _ShellMode < 2.5 ? cell.x : dots.x - 1.0 - cell.x;
                kind = SideCell(k, cell.y, dots.y, dots.x - 1.0);
            }
            if (kind == SHELL_NONE) return half4(0.0, 0.0, 0.0, 0.0);

            half3 rgb = _ShellColor.rgb;
            if (kind == SHELL_EDGE) rgb = _ShellDark.rgb;
            else if (kind == SHELL_RIDGE) rgb = lerp(_ShellColor.rgb, _ShellDark.rgb, 0.35);
            else if (kind == SHELL_INNER) rgb = lerp(_ShellColor.rgb, _ShellDark.rgb, 0.55);
            else if (kind == SHELL_LIGHT) rgb = _ShellLight.rgb;

            float edge = kind == SHELL_EDGE ? 1.0 : 0.0;
            rgb = lerp(rgb, half3(1.0, 1.0, 1.0), saturate(_Flash) * (1.0 - edge * 0.5));
            return half4(rgb, 1.0);
        }
        ENDHLSL

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShellVertex
            #pragma fragment ShellFragment
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShellVertex
            #pragma fragment ShellFragment
            ENDHLSL
        }
    }
}
