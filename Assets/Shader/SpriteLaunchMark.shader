// 発射地点のマーク。ブロックと同じ大きさのドットで、輪・まわる弧・中心の点・撃ったときの波紋を描く。
// 動き（輪の縮み、回転、波紋、薄まり）は BlockAimGuide が MaterialPropertyBlock で入れる。
Shader "ShapeBuster/SpriteLaunchMark"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("輪の色", Color) = (1, 1, 1, 1)
        _Accent ("弧と内側の色", Color) = (0.55, 0.9, 1, 1)
        _FillAlpha ("内側の明るさ", Range(0, 0.6)) = 0.18
        _DotsPerCell ("1 マスのドット数", Range(2, 16)) = 9
        _DotWorldSize ("ドットの大きさ（マスが無いとき）", Float) = 0.06
        [HideInInspector] _RingRadius ("Ring Radius", Float) = 0.21
        [HideInInspector] _RingScale ("Ring Scale", Float) = 1
        [HideInInspector] _Spin ("Spin", Float) = 0
        [HideInInspector] _Pulse ("Pulse", Float) = 0
        [HideInInspector] _Burst ("Burst", Float) = 0
        [HideInInspector] _Fade ("Fade", Float) = 1
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
            #include "SpriteLaunchMarkPass.hlsl"
            #pragma target 3.0
            #pragma vertex UnlitVertex
            #pragma fragment UnlitFragment
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #include "SpriteLaunchMarkPass.hlsl"
            #pragma target 3.0
            #pragma vertex UnlitVertex
            #pragma fragment UnlitFragment
            ENDHLSL
        }
    }
}
