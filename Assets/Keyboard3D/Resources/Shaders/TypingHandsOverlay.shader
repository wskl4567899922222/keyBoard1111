// 指法手覆盖着色器（2026-11-09）：双手永远画在键盘上方。
// 背景：手部透明平面悬浮高度仅 0.06×键高，按键下压时手指沉到键帽顶面以下，
// LEqual 深度测试把手指碎片藏进键帽后面（截图实测：D 键手势手指被键帽截断）。
// URP/Particles/Unlit 不暴露 _ZTest 属性 → 专写一个 ZTest Always 的极简 Unlit。
// 与原 URP/Particles/Unlit 行为对齐：纹理 × 顶点色（半幅缝线羽化用）× _BaseColor（含运行时动画的 alpha）。
Shader "ByteTyping/HandsOverlay"
{
    Properties
    {
        _BaseMap("Texture", 2D) = "white" {}
        _BaseColor("Tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Cull Off               // 双面：俯仰角大时背面不可穿帮（原 NewHandMat 同款）
            ZWrite Off
            ZTest Always           // 核心：无条件通过深度测试 → 与键帽任何穿插都画在键盘之上
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                return tex * IN.color * _BaseColor;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
