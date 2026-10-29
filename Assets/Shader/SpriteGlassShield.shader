Shader "ShapeBuster/SpriteGlassShield"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _OutlineColor ("Outline Color", Color) = (0.82,0.97,1,1)
        _OutlineWidth ("Outline Width", Range(0.005, 0.2)) = 0.03
        _GlassColor ("ガラス色", Color) = (0.72,0.93,1,1)
        _GlassAlpha ("ガラスの透明度", Range(0.05, 1)) = 0.42
        _GlassShade ("下側の暗さ", Range(0.2, 1)) = 0.62
        _GlassLine ("筋の強さ", Range(0, 0.4)) = 0.07
        _ShineColor ("光の色", Color) = (1,1,1,1)
        _ShineInterval ("光る間隔", Range(0.4, 8)) = 2.6
        _ShineDuration ("光っている時間", Range(0.08, 2)) = 0.7
        _ShineWidth ("光の幅", Range(0.02, 0.6)) = 0.16
        _ShineStrength ("光の強さ", Range(0, 2)) = 0.9
        _ShineDirection ("光の向き", Vector) = (1, 0.45, 0, 0)
        _SheenStrength ("常時の反射", Range(0, 1)) = 0.22
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

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #include "SpriteGlassShieldPass.hlsl"
            #pragma target 3.0
            #pragma vertex UnlitVertex
            #pragma fragment UnlitFragment
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #include "SpriteGlassShieldPass.hlsl"
            #pragma target 3.0
            #pragma vertex UnlitVertex
            #pragma fragment UnlitFragment
            ENDHLSL
        }
    }
}
