// タレットの弾。プレイヤーのボール（丸）と見分けられるよう、タレットと同じ色のドットのひし形にする。光は左上から。
Shader "ShapeBuster/SpriteTurretShot"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _HighlightColor ("明るい色", Color) = (1, 0.886, 0.588, 1)
        _Color ("地の色", Color) = (1, 0.667, 0.235, 1)
        _ShadowColor ("影の色", Color) = (0.886, 0.486, 0.157, 1)
        _EdgeColor ("縁の色", Color) = (0.667, 0.329, 0.118, 1)
        _OutlineColor ("外の線の色", Color) = (0.133, 0.125, 0.204, 1)
        _PixelSize ("ドットの大きさ", Range(0.04, 0.25)) = 0.07
        _Size ("ひし形の大きさ", Range(0.1, 0.55)) = 0.32
        _OutlineAlpha ("外の線の濃さ", Range(0, 1)) = 0.9
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
            #include "SpriteTurretShotPass.hlsl"
            #pragma target 3.0
            #pragma vertex UnlitVertex
            #pragma fragment UnlitFragment
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #include "SpriteTurretShotPass.hlsl"
            #pragma target 3.0
            #pragma vertex UnlitVertex
            #pragma fragment UnlitFragment
            ENDHLSL
        }
    }
}
