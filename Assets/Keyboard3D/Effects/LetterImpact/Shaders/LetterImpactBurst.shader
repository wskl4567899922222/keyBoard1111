Shader "KeyboardGame/LetterImpact/Burst"
{
    Properties
    {
        _Color("Color",Color)=(0.15,0.8,1,1)
        _Kind("Core / Body / Star / Fragment / Afterglow",Float)=0
        _Release("Breakup",Range(0,1))=0
        _CoverRelease("Cover release",Range(0,1))=0
        _Attack("Attack",Range(0,1))=1
        _Seed("Seed",Float)=0
        _Variation("Lobe variation",Float)=0.14
        _Glow("Edge glow",Float)=0.12
        [HideInInspector] _CoverageDebug("Coverage diagnostic",Float)=0
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent+30" "RenderType"="Transparent"}
        Pass
        {
            Cull Off ZWrite Off ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A {float4 positionOS:POSITION;float2 uv:TEXCOORD0;};
            struct V {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;};
            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Kind,_Release,_CoverRelease,_Attack,_Seed,_Variation,_Glow,_CoverageDebug;
            CBUFFER_END
            V vert(A i){V o;o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.uv=i.uv;return o;}
            float smin(float a,float b,float k)
            {
                float h=saturate(0.5+0.5*(b-a)/k);return lerp(b,a,h)-k*h*(1-h);
            }
            float roundedBox(float2 p,float2 b,float radius)
            {
                float2 q=abs(p)-b+radius;return length(max(q,0))+min(max(q.x,q.y),0)-radius;
            }
            half4 frag(V i):SV_Target
            {
                float2 p=i.uv*2-1;
                float alpha=0;float3 rgb=_Color.rgb;
                if(_Kind<0.5)
                {
                    float r=length(p);
                    float disk=1-smoothstep(0.05,0.60,r);
                    float slash=pow(saturate(1-abs(p.x+p.y*0.20)*13),2)*pow(saturate(1-abs(p.y)),1.6);
                    float cross=pow(saturate(1-abs(p.y-p.x*0.15)*17),2)*pow(saturate(1-abs(p.x)),1.8);
                    alpha=saturate(disk+slash*0.55+cross*0.40);
                }
                else if(_Kind<1.5)
                {
                    float breaking=smoothstep(0,1,_Release);
                    float coreScale=1-smoothstep(0,0.78,_CoverRelease);
                    float core=roundedBox(p,float2(0.66,0.69)*max(coreScale,0.001),0.42*coreScale);
                    if(coreScale<0.005)core=5;
                    float d=core;
                    // Six persistent asymmetrical lobes. At hold they are joined; at release their necks separate.
                    [unroll] for(int n=0;n<6;n++)
                    {
                        float angle=n*1.04719755+_Seed*0.17;
                        float sn,cs;sincos(angle,sn,cs);
                        float variation=sin(n*2.71+_Seed)*_Variation;
                        float radius=(0.24+variation*0.22)*(1-smoothstep(0.12,1,_Release));
                        float radial=0.58+variation*0.25+breaking*0.13;
                        float2 center=float2(cs,sn)*radial;
                        float lobe=length(p-center)-radius;
                        d=smin(d,lobe,lerp(0.13,0.012,breaking));
                    }
                    float aa=max(fwidth(d),0.003);
                    float fill=1-smoothstep(-aa,aa,d);
                    float rim=exp2(-max(d,0)*48)*_Glow*(1-fill);
                    float tail=1-smoothstep(0.80,1,_Release);
                    alpha=(fill+rim)*tail;
                    // Solid color with a broad internal highlight; no white ball shading or dark contour.
                    float inner=saturate(-d*4);
                    float light=0.08+0.17*inner+0.06*saturate(p.y+0.5);
                    rgb=lerp(rgb,float3(1.25,1.25,1.25),light);
                }
                else if(_Kind<3.5)
                {
                    // The mesh itself is a five-point star or triangle, not a glowing rectangle.
                    alpha=1;
                    rgb*=0.92+0.08*saturate(p.y+0.5);
                }
                else
                {
                    float r=length(p*float2(1,1.08));
                    alpha=pow(saturate(1-r),3)*0.80;
                }
                if(_CoverageDebug>0.5)rgb=1;
                return half4(rgb,saturate(alpha*_Color.a));
            }
            ENDHLSL
        }
    }
}
