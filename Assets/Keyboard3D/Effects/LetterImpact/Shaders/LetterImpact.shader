Shader "KeyboardGame/LetterImpact/Procedural"
{
    Properties
    {
        _Color("Color",Color)=(1,0.5,0.2,1)
        _Kind("Core / Cover / Spark",Float)=0
        _Progress("Progress",Range(0,1))=0
        _Irregularity("Edge variation",Range(0,1))=0.1
        _Seed("Seed",Float)=0
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent+30" "RenderType"="Transparent"}
        Pass
        {
            Cull Off ZWrite Off ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A {float4 positionOS:POSITION;float2 uv:TEXCOORD0;};
            struct V {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;};
            CBUFFER_START(UnityPerMaterial)
                float4 _Color;float _Kind,_Progress,_Irregularity,_Seed;
            CBUFFER_END
            V vert(A i){V o;o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.uv=i.uv;return o;}
            half4 frag(V i):SV_Target
            {
                float2 p=i.uv*2-1;
                float r=length(p);float a=atan2(p.y,p.x);
                float mask;
                if(_Kind<0.5)
                {
                    float disk=1-smoothstep(0.1,0.85,r);
                    float rays=pow(saturate(1-abs(p.x)*9),3)*saturate(1-abs(p.y));
                    rays+=pow(saturate(1-abs(p.y)*12),3)*saturate(1-abs(p.x));
                    mask=saturate(disk+rays*0.3);
                }
                else if(_Kind<1.5)
                {
                    float wobble=(sin(a*3+_Seed)+sin(a*7-_Seed*1.7)*0.25)*_Irregularity*0.4;
                    float edge=r+wobble*smoothstep(0.2,0.8,r);
                    mask=1-smoothstep(0.52,0.96,edge);
                }
                else
                {
                    mask=pow(saturate(1-abs(p.x)),1.7)*(1-smoothstep(0.2,1,abs(p.y)));
                }
                float3 rgb=_Color.rgb;
                if(_Kind>0.5&&_Kind<1.5)rgb=lerp(rgb,lerp(rgb,float3(1.4,1.4,1.4),0.65),pow(saturate(1-r),2));
                return half4(rgb,mask*_Color.a);
            }
            ENDHLSL
        }
    }
}
