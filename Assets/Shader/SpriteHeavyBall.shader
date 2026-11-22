// 重い玉。スプライトの四角の中に、鉄のかたまりのような玉と、ゆっくり回る鋲の帯を描く。
Shader "ShapeBuster/SpriteHeavyBall"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("地の色", Color) = (0.26, 0.28, 0.33, 1)
        _ShadowColor ("影の色", Color) = (0.07, 0.07, 0.09, 1)
        _EdgeColor ("縁の色", Color) = (0.02, 0.02, 0.03, 1)
        _HighlightColor ("照りの色", Color) = (0.78, 0.84, 0.95, 1)
        _RimColor ("照り返しの色", Color) = (0.3, 0.34, 0.44, 1)
        _BandColor ("帯の色", Color) = (0.1, 0.1, 0.12, 1)
        _RivetColor ("鋲の色", Color) = (0.6, 0.65, 0.75, 1)
        _OutlineColor ("外の線の色", Color) = (0.62, 0.68, 0.8, 1)
        _PixelSize ("ドットの大きさ", Range(0.04, 0.25)) = 0.07
        _Radius ("玉の大きさ", Range(0.3, 1)) = 0.5
        _Bands ("陰の段数", Range(1, 6)) = 3
        _HighlightSize ("照りの大きさ", Range(0, 0.2)) = 0.015
        _BandWidth ("帯の太さ", Range(0, 0.4)) = 0.14
        _BandTilt ("帯の傾き", Range(-1, 1)) = 0.35
        _RivetCount ("鋲の数", Range(0, 12)) = 6
        _RollSpeed ("回る速さ", Range(0, 2)) = 0.25
        _GlintSpeed ("照りのゆらぎの速さ", Range(0, 4)) = 0.8
        _OutlineAlpha ("外の線の濃さ", Range(0, 1)) = 0.55
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
            #include "SpriteHeavyBallPass.hlsl"
            #pragma target 3.0
            #pragma vertex UnlitVertex
            #pragma fragment UnlitFragment
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #include "SpriteHeavyBallPass.hlsl"
            #pragma target 3.0
            #pragma vertex UnlitVertex
            #pragma fragment UnlitFragment
            ENDHLSL
        }
    }
}
