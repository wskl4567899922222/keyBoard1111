Shader "KeyboardGame/SingleTrailGlow"
{
    Properties
    {
        [MainTexture] _MainTex("Core texture",2D)="white"{}
        _Intensity("Brightness",Range(0,8))=2
        _WhiteCore("White core",Range(0,1))=0.85
        _Head("Head sprite",Float)=0
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
        Pass
        {
            Name "SingleTrail"
            Tags {"LightMode"="SRPDefaultUnlit"}
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes {float4 positionOS:POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;};
            struct Varyings {float4 positionCS:SV_POSITION;half4 color:COLOR;float2 uv:TEXCOORD0;};
            TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half _Intensity;
                half _WhiteCore;
                half _Head;
            CBUFFER_END
            Varyings vert(Attributes v)
            {
                Varyings o;o.positionCS=TransformObjectToHClip(v.positionOS.xyz);
                o.color=v.color;o.uv=TRANSFORM_TEX(v.uv,_MainTex);return o;
            }
            half4 frag(Varyings i):SV_Target
            {
                float2 uv=float2(lerp(0.5,i.uv.x,_Head),i.uv.y);
                half alpha=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv).a;
                half core=smoothstep(0.65h,0.98h,alpha)*_WhiteCore;
                half3 rgb=lerp(i.color.rgb,half3(1,1,1),core)*_Intensity;
                return half4(rgb,alpha*i.color.a);
            }
            ENDHLSL
        }
    }
}
