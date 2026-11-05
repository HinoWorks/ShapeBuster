// ボムボール。スプライトの四角の中に、熱い玉と広がる輪を描く。
Shader "ShapeBuster/SpriteBombBall"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("地の色", Color) = (1, 0.42, 0.08, 1)
        _CoreColor ("芯の色", Color) = (1, 0.95, 0.75, 1)
        _EdgeColor ("縁の色", Color) = (0.45, 0.08, 0.02, 1)
        _RingColor ("輪の色", Color) = (1, 0.85, 0.3, 1)
        _PixelSize ("ドットの大きさ", Range(0.04, 0.25)) = 0.1
        _Radius ("玉の大きさ", Range(0.3, 1)) = 0.78
        _CoreRadius ("芯の大きさ", Range(0.05, 0.6)) = 0.22
        _PulseSpeed ("輪の速さ", Range(0.2, 4)) = 1.4
        _RingThickness ("輪の太さ", Range(0.02, 0.25)) = 0.08
        _RingStrength ("輪の強さ", Range(0, 1.5)) = 1
        _EmberRate ("火の粉の速さ", Range(0, 20)) = 8
        _EmberStrength ("火の粉の強さ", Range(0, 1.5)) = 0.8
        _Glow ("外の光", Range(0, 0.4)) = 0.18
        _GlowAlpha ("外の光の濃さ", Range(0, 1)) = 0.35
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
            #include "SpriteBombBallPass.hlsl"
            #pragma target 3.0
            #pragma vertex UnlitVertex
            #pragma fragment UnlitFragment
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #include "SpriteBombBallPass.hlsl"
            #pragma target 3.0
            #pragma vertex UnlitVertex
            #pragma fragment UnlitFragment
            ENDHLSL
        }
    }
}
