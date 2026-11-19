Shader "ShapeBuster/BlockLevelFill"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _FillColor ("現在レベルの色", Color) = (0.45, 0.62, 0.38, 1)
        _UnderColor ("下のレベルの色", Color) = (0, 0, 0, 0)
        _OutlineColor ("Outline Color", Color) = (0.92, 0.93, 0.9, 1)
        _OutlineWidth ("Outline Width", Range(0.005, 0.2)) = 0.03
        [HideInInspector] _OutlineWidthXY ("Outline Width XY", Vector) = (0,0,0,0)
        _DotScale ("ドットの細かさ", Range(2, 16)) = 6
        _DotSize ("ドットの大きさ", Range(0.4, 1)) = 0.75
        [HideInInspector] _CellSpan ("Cell Span", Vector) = (1,1,0,0)
        [HideInInspector] _Flash ("Flash", Range(0, 1)) = 0
        [HideInInspector] _Zombie ("ゾンビとして描く", Float) = 0
        [HideInInspector] _Seed ("個体の種", Float) = 0
        [Header(Zombie)] _EyeColor ("目の色", Color) = (0.93, 1, 0.45, 1)
        _MouthColor ("口の色", Color) = (0.08, 0.02, 0.03, 1)
        _TeethColor ("歯の色", Color) = (0.86, 0.8, 0.62, 1)
        _RotColor ("腐敗の掛け色", Color) = (0.55, 0.66, 0.4, 1)
        _RotAmount ("腐敗の量", Range(0, 1)) = 0.25
        _WoundColor ("傷の色", Color) = (0.42, 0.04, 0.06, 1)
        [Header(Regen)] [ToggleUI] _Regen ("額に再生の種を描く", Float) = 0
        _PodColor ("種の光った色", Color) = (0.55, 0.88, 1, 1)
        _PodDim ("種の暗い色", Color) = (0.1, 0.4, 0.95, 1)
        _VeinAmount ("血管の太さ", Range(0, 0.2)) = 0.07
        [HideInInspector] _RegenGlow ("種の明るさ", Range(0, 1)) = 0
        [HideInInspector] _RegenCharge ("回復の溜まり", Range(0, 1)) = 0
        [HideInInspector] _HealPulse ("回復の光", Range(0, 1)) = 0
        [HideInInspector] _ArmorSide ("殻の向き", Float) = 0
        [Header(Bloat)] [ToggleUI] _Bloat ("膨張の模様を描く", Float) = 0
        _BoilColor ("膿の粒の色", Color) = (1, 0.7, 0.3, 1)
        [Header(Split)] [ToggleUI] _Split ("分裂の縫い目を描く", Float) = 0
        _StitchColor ("縫い糸の色", Color) = (0.85, 0.8, 0.68, 1)
        _PatchTint ("継ぎはぎの肌に掛ける色", Color) = (0.95, 0.62, 0.95, 1)
        [HideInInspector] _SplitSide ("分裂の子の向き", Float) = 0
        [Header(Ghost)] [ToggleUI] _Ghost ("幽霊の模様を描く", Float) = 0
        [HideInInspector] _GhostFade ("すり抜けの透け", Range(0, 1)) = 0
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
            #include "BlockLevelFillPass.hlsl"
            #pragma target 3.0
            #pragma vertex UnlitVertex
            #pragma fragment UnlitFragment
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #include "BlockLevelFillPass.hlsl"
            #pragma target 3.0
            #pragma vertex UnlitVertex
            #pragma fragment UnlitFragment
            ENDHLSL
        }
    }
}
