Shader "KeyboardGame/WordDust"
{
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent+15" "RenderType"="Transparent"}
        Pass
        {
            Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A{float4 pos:POSITION;float2 uv:TEXCOORD0;float2 data:TEXCOORD1;half4 color:COLOR;};
            struct V{float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float2 data:TEXCOORD1;half4 color:COLOR;};
            V vert(A a){V o;o.pos=TransformObjectToHClip(a.pos.xyz);o.uv=a.uv;o.data=a.data;o.color=a.color;return o;}
            float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);}
            half4 frag(V i):SV_Target
            {
                float2 p=i.uv*2-1;float seed=i.data.x,time=i.data.y;
                float n=noise(p*2.7+seed+float2(time*.55,-time*.8));
                float fine=noise(p*5.3-seed+time*.45);
                float radius=length(p)+(.5-n)*.24;
                float edge=1-smoothstep(.34,.94,radius);
                float wisps=smoothstep(.18,.70,n*.7+fine*.3+(.30-time*.13));
                float alpha=edge*wisps*i.color.a;
                float light=.76+.24*n;
                return half4(i.color.rgb*light,alpha);
            }
            ENDHLSL
        }
    }
}
