Shader "ShapeBuster/FieldFloor"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("全体に掛ける色", Color) = (1, 1, 1, 1)
        _FillColor ("床の色", Color) = (0.434, 0.434, 0.434, 1)
        _CellSize ("マスの大きさ（ワールド。プレイ中は BlockSession がブロックのマスの大きさを入れる）", Float) = 1
        _GridOrigin ("マスの基準の位置（プレイ中は BlockSession がブロックの列の左上を入れる）", Vector) = (-8, -8, 0, 0)
        _ShadeMin ("暗いマスの明るさ（1 でむらなし）", Range(0, 1)) = 0.92
        [ToggleUI] _FineOn ("細かいドットを重ねる（オフで、マスだけの床）", Float) = 1
        _FineDots ("細かいドット：1 マスあたりの数（ブロックは 9）", Range(2, 24)) = 9
        _FineShadeMin ("細かいドット：暗いドットの明るさ（1 でむらなし）", Range(0, 1)) = 0.95
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
            #include "FieldFloorPass.hlsl"
            #pragma target 3.0
            #pragma vertex UnlitVertex
            #pragma fragment UnlitFragment
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #include "FieldFloorPass.hlsl"
            #pragma target 3.0
            #pragma vertex UnlitVertex
            #pragma fragment UnlitFragment
            ENDHLSL
        }
    }
}
