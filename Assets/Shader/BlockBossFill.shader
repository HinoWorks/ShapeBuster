Shader "ShapeBuster/BlockBossFill"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _FillColor ("現在レベルの色", Color) = (0.62, 0.16, 0.22, 1)
        _UnderColor ("下のレベルの色", Color) = (0, 0, 0, 0)
        _OutlineColor ("Outline Color", Color) = (0.93, 0.80, 0.84, 1)
        _OutlineWidth ("Outline Width", Range(0.005, 0.2)) = 0.05
        [HideInInspector] _OutlineWidthXY ("Outline Width XY", Vector) = (0,0,0,0)
        _DotScale ("ドットの細かさ", Range(2, 16)) = 7.5
        _DotSize ("ドットの大きさ", Range(0.4, 1)) = 0.8
        [HideInInspector] _CellSpan ("Cell Span", Vector) = (1,1,0,0)
        _VeinColor ("亀裂の色", Color) = (1, 0.14, 0.38, 1)
        _CoreColor ("裂け目の色", Color) = (0.015, 0, 0.02, 1)
        _Corruption ("侵食", Range(0, 1)) = 0.34
        _VeinStrength ("亀裂の強さ", Range(0, 1)) = 1
        _VeinSpeed ("亀裂の速さ", Range(0, 2)) = 0.65
        _PulseSpeed ("脈動の速さ", Range(0, 4)) = 1
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
            #include "BlockBossFillPass.hlsl"
            #pragma target 3.0
            #pragma vertex UnlitVertex
            #pragma fragment UnlitFragment
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #include "BlockBossFillPass.hlsl"
            #pragma target 3.0
            #pragma vertex UnlitVertex
            #pragma fragment UnlitFragment
            ENDHLSL
        }
    }
}
