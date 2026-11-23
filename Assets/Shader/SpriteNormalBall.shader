// ノーマルボール。スプライトの四角の中に、ドットで描いた白い玉を描く。光は左上から。
Shader "ShapeBuster/SpriteNormalBall"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("地の色", Color) = (1, 1, 1, 1)
        _ShadowColor ("影の色", Color) = (0.62, 0.68, 0.82, 1)
        _EdgeColor ("縁の色", Color) = (0.36, 0.4, 0.55, 1)
        _HighlightColor ("照りの色", Color) = (1, 1, 1, 1)
        _OutlineColor ("外の線の色", Color) = (0.13, 0.13, 0.2, 1)
        _PixelSize ("ドットの大きさ", Range(0.04, 0.25)) = 0.07
        _Radius ("玉の大きさ", Range(0.3, 1)) = 0.5
        _Bands ("陰の段数", Range(1, 6)) = 3
        _HighlightSize ("照りの大きさ", Range(0, 0.2)) = 0.03
        _OutlineAlpha ("外の線の濃さ", Range(0, 1)) = 0.8
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
            #include "SpriteNormalBallPass.hlsl"
            #pragma target 3.0
            #pragma vertex UnlitVertex
            #pragma fragment UnlitFragment
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #include "SpriteNormalBallPass.hlsl"
            #pragma target 3.0
            #pragma vertex UnlitVertex
            #pragma fragment UnlitFragment
            ENDHLSL
        }
    }
}
