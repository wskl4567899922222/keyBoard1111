Shader "Keyboard/Preview/EnergyFlightDetails"
{
 Properties { _HueShift("Hue shift",Float)=0 _ZTest("[ZTest]",Float)=4 }
 SubShader
 {
  Tags{"RenderPipeline"="UniversalPipeline" "Queue"="Transparent+1"}
  Pass
  {
   Blend One One Cull Off ZWrite Off ZTest [_ZTest]
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "EnergyFlightHue.hlsl"
   struct A{float4 p:POSITION;float2 uv:TEXCOORD0;float2 kind:TEXCOORD1;float4 color:COLOR;};
   struct V{float4 p:SV_POSITION;float2 uv:TEXCOORD0;float2 kind:TEXCOORD1;float4 color:COLOR;};
   V vert(A i){V o;o.p=TransformObjectToHClip(i.p.xyz);o.uv=i.uv;o.kind=i.kind;o.color=i.color;return o;}
   half4 frag(V i):SV_Target
   {
    float2 p=i.uv;float3 color;
    if(i.kind.x>1.5)
    {
     float halo=exp(-dot(p,p)*4.7);
     float rays=exp(-abs(p.x)*32)*pow(saturate(1-abs(p.y)),2)
      +exp(-abs(p.y)*32)*pow(saturate(1-abs(p.x)),2);
     float core=exp(-dot(p,p)*170);
     color=float3(.025,.065,.65)*lerp(1,i.color.rgb*1.6,.6)*halo*.65
      +i.color.rgb*rays*.65+lerp(float3(.78,.90,1),i.color.rgb,.15)*core;
    }
    else if(i.kind.x>.5)
    {
     float shape=i.kind.y;
     float2 q=p;q.y+=(shape-.5)*p.x*.6;
     float d=shape<.33?dot(q,q):shape<.66?pow(abs(q.x)+abs(q.y),2):dot(q,q)/max(.22,1+.65*q.x);
     float core=exp(-d*12);
     color=i.color.rgb*(exp(-d*4)*.25+core*.9)+float3(.65,.8,1)*core*.35;
    }
    else
    {
     float longitudinal=pow(saturate(1-abs(p.x)),.8);
     float thin=exp(-p.y*p.y*24);
     color=i.color.rgb*longitudinal*(thin*.75+exp(-p.y*p.y*4)*.16);
    }
    return half4(ShiftFlightHue(color)*i.color.a,1);
   }
   ENDHLSL
  }
 }
}
