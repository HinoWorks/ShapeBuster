// 伸びるレーザー。スプライトのアルファが形になる。
// 芯の太さはワールド単位なので、幅が太くなっても白い芯は細いまま残る。
Shader "ShapeBuster/SpriteLaserBeam"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("色味", Color) = (1,1,1,1)
        _CoreColor ("芯の色", Color) = (1,1,1,1)
        _EdgeColor ("縁の色", Color) = (0.18,0.05,0.55,1)
        _PixelSize ("ドットの大きさ", Range(0.02, 0.25)) = 0.07
        _CoreWidth ("芯の太さ", Range(0, 0.4)) = 0.045
        _GlowWidth ("光の広がり", Range(0.05, 1.5)) = 0.62
        _FlowScale ("筋の間隔", Range(0.05, 2)) = 0.35
        _FlowSpeed ("筋の速さ", Range(0, 40)) = 12
        _StreakWidth ("筋の太さ", Range(0.02, 0.6)) = 0.2
        _StreakStrength ("筋の強さ", Range(0, 1.5)) = 0.75
        _SparkRate ("瞬きの速さ", Range(0, 30)) = 16
        _EndFade ("端のぼかし", Range(0, 0.2)) = 0.04
        _EdgeAlpha ("縁の濃さ", Range(0, 1)) = 0.45
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
            #include "SpriteLaserBeamPass.hlsl"
            #pragma target 3.0
            #pragma vertex UnlitVertex
            #pragma fragment UnlitFragment
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #include "SpriteLaserBeamPass.hlsl"
            #pragma target 3.0
            #pragma vertex UnlitVertex
            #pragma fragment UnlitFragment
            ENDHLSL
        }
    }
}
