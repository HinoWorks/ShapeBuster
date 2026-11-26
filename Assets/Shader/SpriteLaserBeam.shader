// 縦レーザー。真ん中は白、上下の端へ向かって色が乗る。
// _PixelMix を上げると、ブロックと同じ大きさのドットに区切って、色を段にする（0 でもとのなめらかな見た目）。
Shader "ShapeBuster/SpriteLaserBeam"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _EndPower ("端への色の集まり", Range(0.4, 4)) = 1.6
        _GradientAngle ("濃淡の角度", Range(-90, 90)) = 0
        [Header(Dot)]
        _PixelMix ("ドット感（0 でなめらか）", Range(0, 1)) = 0.5
        _DotsPerCell ("1 マスのドット数", Range(2, 16)) = 9
        _ColorSteps ("色の段の数", Range(1, 8)) = 4
        _DotWorldSize ("ドットの大きさ（マスが無いとき）", Float) = 0.06
        [Header(Fade)]
        _FadeCool ("消えるとき芯が色に変わる強さ", Range(0, 2)) = 1.2
        _FadeCrumble ("消えるときの崩れ方", Range(0, 1.5)) = 1.1
        _FadeSpark ("崩れる直前の光", Range(0, 1)) = 0.6
        [HideInInspector] _BeamFade ("Beam Fade", Float) = 0
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
