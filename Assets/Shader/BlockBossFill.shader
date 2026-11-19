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
        _DotScale ("ドットの細かさ", Range(2, 16)) = 8
        _DotSize ("ドットの大きさ", Range(0.4, 1)) = 0.8
        [HideInInspector] _CellSpan ("Cell Span", Vector) = (1,1,0,0)
        _VeinColor ("亀裂の色", Color) = (1, 0.14, 0.38, 1)
        _Corruption ("侵食", Range(0, 1)) = 0.3
        _VeinStrength ("亀裂の強さ", Range(0, 1)) = 1
        _VeinSpeed ("亀裂の速さ", Range(0, 2)) = 0.65
        _PulseSpeed ("脈動の速さ", Range(0, 4)) = 1
        [HideInInspector] _Flash ("Flash", Range(0, 1)) = 0
        [HideInInspector] _Zombie ("ゾンビとして描く", Float) = 0
        [HideInInspector] _Seed ("個体の種", Float) = 0
        [Header(Zombie)] _EyeColor ("目の色", Color) = (1, 0.3, 0.55, 1)
        _MouthColor ("口の色", Color) = (0.08, 0.02, 0.03, 1)
        _TeethColor ("歯の色", Color) = (0.86, 0.8, 0.62, 1)
        _RotColor ("腐敗の掛け色", Color) = (0.55, 0.66, 0.4, 1)
        _RotAmount ("腐敗の量", Range(0, 1)) = 0.3
        _WoundColor ("傷の色", Color) = (0.42, 0.04, 0.06, 1)
        [Header(Shape)] [Enum(Square,0,Trapezoid,1,Diamond,2,Hourglass,3,Crown,4,Cross,5,InvTrapezoid,6,Hexagon,7)] _Shape ("形", Float) = 0
        [Header(Shell)] [ToggleUI] _BossShell ("甲羅の殻を描く", Float) = 0
        _ShellTint ("殻の色", Color) = (0.86, 0.62, 0.2, 1)
        [HideInInspector] _ShellOpen ("殻の開いた面", Float) = 0
        [HideInInspector] _ShellWarn ("殻が回る前の点滅", Range(0, 1)) = 0
        [HideInInspector] _ShellHit ("殻が弾いた光", Range(0, 1)) = 0
        [Header(Regen)] [ToggleUI] _Regen ("再生の種を描く", Float) = 0
        _PodColor ("種の色", Color) = (0.55, 0.88, 1, 1)
        _PodDim ("種の暗い色", Color) = (0.1, 0.4, 0.95, 1)
        _VeinAmount ("血管の量", Range(0, 0.5)) = 0
        [HideInInspector] _RegenGlow ("RegenGlow", Float) = 0
        [HideInInspector] _RegenCharge ("RegenCharge", Float) = 0
        [HideInInspector] _HealPulse ("HealPulse", Float) = 0
        [HideInInspector] _ArmorSide ("ArmorSide", Float) = 0
        [HideInInspector] _Bloat ("Bloat", Float) = 0
        [HideInInspector] _BoilColor ("BoilColor", Color) = (1, 0.7, 0.3, 1)
        [HideInInspector] _Split ("Split", Float) = 0
        [HideInInspector] _StitchColor ("StitchColor", Color) = (0.85, 0.8, 0.68, 1)
        [HideInInspector] _PatchTint ("PatchTint", Color) = (1, 1, 1, 1)
        [HideInInspector] _SplitSide ("SplitSide", Float) = 0
        [HideInInspector] _Ghost ("Ghost", Float) = 0
        [HideInInspector] _GhostFade ("GhostFade", Float) = 0
        [HideInInspector] _PodCrack ("種のひび", Range(0, 1)) = 0
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
