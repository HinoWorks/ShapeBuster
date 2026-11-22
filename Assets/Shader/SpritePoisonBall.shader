// 毒ボール。スプライトの四角の中に、緑の毒の玉と、ぷくぷく浮いて弾ける泡を描く。
Shader "ShapeBuster/SpritePoisonBall"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("地の色", Color) = (0.3, 0.72, 0.24, 1)
        _CoreColor ("芯の色", Color) = (0.62, 0.95, 0.42, 1)
        _EdgeColor ("縁の色", Color) = (0.06, 0.24, 0.08, 1)
        _BubbleColor ("泡の色", Color) = (0.86, 1, 0.74, 1)
        _PixelSize ("ドットの大きさ", Range(0.04, 0.25)) = 0.1
        _Radius ("玉の大きさ", Range(0.3, 1)) = 0.78
        _CoreRadius ("芯の大きさ", Range(0.05, 0.6)) = 0.2
        _BubbleSize ("泡の大きさ（マス）", Range(0.5, 2)) = 1
        _BubbleSpeed ("泡の速さ", Range(0.1, 2)) = 0.55
        _BubbleStrength ("泡の濃さ", Range(0, 1)) = 0.9
        _WobbleSpeed ("揺れの速さ", Range(0, 6)) = 2.2
        _Glow ("外の光", Range(0, 0.4)) = 0.14
        _GlowAlpha ("外の光の濃さ", Range(0, 1)) = 0.3
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
            #include "SpritePoisonBallPass.hlsl"
            #pragma target 3.0
            #pragma vertex UnlitVertex
            #pragma fragment UnlitFragment
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #include "SpritePoisonBallPass.hlsl"
            #pragma target 3.0
            #pragma vertex UnlitVertex
            #pragma fragment UnlitFragment
            ENDHLSL
        }
    }
}
