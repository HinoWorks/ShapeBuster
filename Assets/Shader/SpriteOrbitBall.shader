// オービットボール。スプライトの四角の中に、熱い芯とそれを回る玉を描く。
Shader "ShapeBuster/SpriteOrbitBall"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("地の色", Color) = (1, 0.72, 0.18, 1)
        _CoreColor ("芯の色", Color) = (1, 0.96, 0.72, 1)
        _EdgeColor ("縁の色", Color) = (0.55, 0.2, 0.02, 1)
        _MoonColor ("回る玉の色", Color) = (1, 0.98, 0.86, 1)
        _TrackColor ("軌道の色", Color) = (1, 0.8, 0.32, 1)
        _PixelSize ("ドットの大きさ", Range(0.04, 0.25)) = 0.08
        _Radius ("玉の大きさ", Range(0.2, 0.7)) = 0.4
        _CoreRadius ("芯の大きさ", Range(0.05, 0.4)) = 0.16
        _OrbitRadius ("軌道の半径", Range(0.3, 0.95)) = 0.72
        _MoonSize ("回る玉の大きさ", Range(0.03, 0.25)) = 0.11
        _OrbitSpeed ("回る速さ", Range(0, 6)) = 1.6
        _TrackWidth ("軌道の太さ", Range(0.01, 0.2)) = 0.045
        _TrackAlpha ("軌道の濃さ", Range(0, 1)) = 0.55
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
            #include "SpriteOrbitBallPass.hlsl"
            #pragma target 3.0
            #pragma vertex UnlitVertex
            #pragma fragment UnlitFragment
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #include "SpriteOrbitBallPass.hlsl"
            #pragma target 3.0
            #pragma vertex UnlitVertex
            #pragma fragment UnlitFragment
            ENDHLSL
        }
    }
}
