// 割れたポーションの水たまり。揺れる液面と、ぷくぷく弾ける泡。
// スプライトのアルファが水たまりの形になる。
Shader "ShapeBuster/SpritePotionPurify"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _BubbleTex ("泡の画像", 2D) = "white" {}
        _Color ("色味", Color) = (1,1,1,1)
        _DeepColor ("深い液の色", Color) = (0.04, 0.34, 0.32, 1)
        _ShallowColor ("浅い液の色", Color) = (0.42, 0.94, 0.74, 1)
        _FoamColor ("泡の色", Color) = (0.9, 1, 0.93, 1)
        _GlowColor ("光の色", Color) = (1, 0.9, 0.55, 1)
        _LiquidAlpha ("液の濃さ", Range(0.15, 1)) = 0.72
        _RippleSpeed ("液面の速さ", Range(0.2, 8)) = 4.2
        _RippleScale ("液面の細かさ", Range(8, 48)) = 24
        _RippleStrength ("液面の揺れ", Range(0, 2)) = 0.9
        _BubbleScale ("泡の数", Range(2, 12)) = 4.6
        _BubbleSpeed ("泡の速さ", Range(0.1, 2)) = 0.42
        _BubbleDensity ("泡の密度", Range(0, 1)) = 0.5
        _DotCount ("ドットの細かさ", Range(8, 128)) = 32
        _EdgeWidth ("縁の太さ", Range(0.005, 0.08)) = 0.03
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

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #include "SpritePotionPurifyPass.hlsl"
            #pragma target 3.0
            #pragma vertex UnlitVertex
            #pragma fragment UnlitFragment
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #include "SpritePotionPurifyPass.hlsl"
            #pragma target 3.0
            #pragma vertex UnlitVertex
            #pragma fragment UnlitFragment
            ENDHLSL
        }
    }
}
