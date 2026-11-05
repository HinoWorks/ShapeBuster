// レーザーボール。スプライトの四角の中に、縦の芯と上へ流れる光を描く。
Shader "ShapeBuster/SpriteLaserBall"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("地の色", Color) = (0.25, 0.9, 1, 1)
        _CoreColor ("芯の色", Color) = (0.95, 1, 1, 1)
        _EdgeColor ("縁の色", Color) = (0.05, 0.2, 0.85, 1)
        _PixelSize ("ドットの大きさ", Range(0.04, 0.25)) = 0.1
        _Radius ("玉の大きさ", Range(0.3, 1)) = 0.72
        _CoreWidth ("芯の太さ", Range(0.02, 0.4)) = 0.12
        _GlowWidth ("光の広がり", Range(0.05, 1)) = 0.55
        _FlowScale ("筋の間隔", Range(0.5, 8)) = 3
        _FlowSpeed ("筋の速さ", Range(0, 12)) = 4
        _StreakWidth ("筋の太さ", Range(0.02, 0.6)) = 0.22
        _StreakStrength ("筋の強さ", Range(0, 1.5)) = 0.85
        _SparkRate ("瞬きの速さ", Range(0, 24)) = 12
        _BeamAlpha ("上下の光の濃さ", Range(0, 1)) = 0.55
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
            #include "SpriteLaserBallPass.hlsl"
            #pragma target 3.0
            #pragma vertex UnlitVertex
            #pragma fragment UnlitFragment
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #include "SpriteLaserBallPass.hlsl"
            #pragma target 3.0
            #pragma vertex UnlitVertex
            #pragma fragment UnlitFragment
            ENDHLSL
        }
    }
}
