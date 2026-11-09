Shader "KeyboardGame/Letters/WaitingSDF"
{
    Properties
    {
        _MainTex("Static font distance atlas",2D)="white"{}
        _Color("Color",Color)=(0.55,0.55,0.55,1)
        _EdgeSoftness("Edge transition in screen pixels",Range(0.5,1.5))=1
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
        Pass
        {
            Cull Off ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A {float4 positionOS:POSITION;float2 uv:TEXCOORD0;};
            struct V {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;};
            TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;half4 _Color;float _EdgeSoftness;
            CBUFFER_END
            V vert(A i){V o;o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.uv=TRANSFORM_TEX(i.uv,_MainTex);return o;}
            half4 frag(V i):SV_Target
            {
                float distance=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv).a;
                float width=max(fwidth(distance),0.0001)*_EdgeSoftness;
                float alpha=smoothstep(0.5-width*0.5,0.5+width*0.5,distance);
                return half4(_Color.rgb,_Color.a*alpha);
            }
            ENDHLSL
        }
    }
}
