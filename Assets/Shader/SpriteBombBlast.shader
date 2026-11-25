// ボムボールの爆発。攻撃範囲（円）の中にだけ描き、外は透明。
// 白い閃光 → 広がる衝撃の輪 → ドット状の炎 → 煙、の順に _Progress（0→1）で進む。
// _Progress と _Seed は BlockBombBlast がインスタンスごとに入れる。
Shader "ShapeBuster/SpriteBombBlast"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Progress ("進み（0→1。プレイ中はコードが入れる）", Range(0, 1)) = 0.3
        _Seed ("ばらつきの種（プレイ中はコードが入れる）", Float) = 0
        _DotsPerCell ("ドットの細かさ（1 マスあたり）", Range(2, 16)) = 6
        _CoreColor ("閃光の色", Color) = (1, 1, 0.88, 1)
        _HotColor ("熱い炎の色", Color) = (1, 0.82, 0.3, 1)
        _FireColor ("炎の色", Color) = (1, 0.45, 0.12, 1)
        _EmberColor ("燃えさしの色", Color) = (0.62, 0.14, 0.08, 1)
        _SmokeColor ("煙の色", Color) = (0.2, 0.16, 0.16, 0.55)
        _RimColor ("範囲のふちの色", Color) = (1, 0.55, 0.18, 0.9)
        _FlashTime ("閃光の長さ（進みの割合）", Range(0.01, 0.5)) = 0.14
        _RingTime ("輪がふちに着くまで（進みの割合）", Range(0.05, 1)) = 0.45
        _RingWidth ("輪の太さ（ドット）", Range(0.5, 6)) = 2
        _FireAmount ("炎の量", Range(0, 2)) = 1
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
            #include "SpriteBombBlastPass.hlsl"
            #pragma target 3.0
            #pragma vertex UnlitVertex
            #pragma fragment UnlitFragment
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #include "SpriteBombBlastPass.hlsl"
            #pragma target 3.0
            #pragma vertex UnlitVertex
            #pragma fragment UnlitFragment
            ENDHLSL
        }
    }
}
