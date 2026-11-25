Shader "ShapeBuster/FieldSide"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("帯の色に掛ける色（帯の色はプレハブの SpriteRenderer の色）", Color) = (1, 1, 1, 1)
        _CellSize ("セルの大きさ（ワールド）", Float) = 1
        _GridOrigin ("セルの基準の位置（床の左下）", Vector) = (-8, -8, 0, 0)
        _ShadeMin ("暗いセルの明るさ（床は 0.82）", Range(0, 1)) = 0.78
        _GlowColorBefore ("内側のふちの光：撒く前（a は強さ）", Color) = (0.72, 0.66, 0.68, 0.08)
        _GlowColor ("内側のふちの光：撒いたあと（a は強さ）", Color) = (0.75, 0.82, 0.78, 0.12)
        _GlowWidth ("内側のふちの光の幅（ワールド）", Float) = 0.9
        _AshColor ("撒く前：灰の粒の色（a は強さ）", Color) = (0.62, 0.58, 0.6, 0.22)
        _AshDensity ("撒く前：灰の粒の多さ（0〜1）", Range(0, 1)) = 0.25
        _AshSpeed ("撒く前：灰の粒が降る速さ（ワールド/秒）", Float) = 0.06
        _DustColor ("撒いたあと：光の粒の色（a は強さ）", Color) = (0.8, 0.95, 0.85, 0.35)
        _DustDensity ("撒いたあと：光の粒の多さ（0〜1）", Range(0, 1)) = 0.3
        _DustSpeed ("撒いたあと：光の粒が上る速さ（ワールド/秒）", Float) = 0.12
        _SceneBrightness ("景色の明るさ（絵の色に掛ける）", Range(0, 2)) = 1
        _SceneOpacity ("景色の濃さ（0 で見えない）", Range(0, 1)) = 1
        [HideInInspector] _SceneTex ("景色の絵（BlockSession が入れる）", 2D) = "black" {}
        [HideInInspector] _SceneRect ("景色を貼る範囲（左下 x, y、幅、高さ。BlockSession が入れる）", Vector) = (0, 0, 1, 1)
        [HideInInspector] _SceneTile ("景色を縦にくり返すか（0/1。BlockSession が入れる）", Float) = 0
        [HideInInspector] _SceneOn ("景色を出すか（0/1。BlockSession が入れる）", Float) = 0
        [HideInInspector] _Cured ("ワクチンを撒いたか（0/1。BlockSession が入れる）", Float) = 0
        [HideInInspector] _EdgeX ("フィールドの縁の x", Float) = 0
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
            #include "FieldSidePass.hlsl"
            #pragma target 3.0
            #pragma vertex UnlitVertex
            #pragma fragment UnlitFragment
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #include "FieldSidePass.hlsl"
            #pragma target 3.0
            #pragma vertex UnlitVertex
            #pragma fragment UnlitFragment
            ENDHLSL
        }
    }
}
