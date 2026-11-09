Shader "KeyboardGame/WordExit/Glow"
{
    Properties { _Brightness("Brightness",Float)=1.25 _Disturbance("Disturbance",Float)=.055 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+80" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off ZTest LEqual Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; float2 detail:TEXCOORD1; float4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float seed:TEXCOORD1; float4 color:COLOR; };
            CBUFFER_START(UnityPerMaterial)
                float _Brightness,_Disturbance,_Phase;
            CBUFFER_END
            Varyings vert(Attributes v){Varyings o;o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.uv=v.uv;o.seed=v.detail.x;o.color=v.color;return o;}
            half4 frag(Varyings i):SV_Target
            {
                float angle=atan2(i.uv.y,i.uv.x),r=length(i.uv);
                float wobble=sin(angle*3+_Phase*5+i.seed)*.55+sin(angle*5-_Phase*3+i.seed*1.7)*.30+sin(angle*7+_Phase*2+i.seed*.7)*.15;
                float edge=.70+wobble*_Disturbance;
                float aa=max(fwidth(r),.002);
                // Alpha is exactly one in the guaranteed central disk; bloom alone cannot hide a letter.
                float body=1-smoothstep(edge-.11,edge+aa,r);
                float halo=exp(-r*r*5.5)*.28*(1-smoothstep(.82,1,r));
                float alpha=max(body,halo)*i.color.a;
                float inner=exp(-r*r*12);
                float cloud=.5+.5*sin(i.uv.x*7+_Phase*2+i.seed)*sin(i.uv.y*8-_Phase*1.6);
                float3 hue=i.color.rgb/max(1,max(i.color.r,max(i.color.g,i.color.b)));
                float3 rgb=lerp(hue*(.86+.10*cloud),lerp(hue,float3(1,1,1),.73),inner)*_Brightness;
                return half4(rgb,alpha);
            }
            ENDHLSL
        }
    }
}
