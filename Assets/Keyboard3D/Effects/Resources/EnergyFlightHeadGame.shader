Shader "Keyboard/Preview/EnergyFlightHeadGame"
{
 Properties { _Clock("Time",Float)=0 _Part("Core / shield / halo",Float)=0 _Gain("Gain",Float)=1 _HueShift("Hue shift",Float)=0 _ZTest("[ZTest]",Float)=4 }
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent"}
  Pass
  {
   Blend One One Cull Off ZWrite Off ZTest [_ZTest]
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "EnergyFlightHue.hlsl"
   float _Clock,_Part,_Gain;
   struct A {float4 p:POSITION;float3 n:NORMAL;float2 uv:TEXCOORD0;};
   struct V {float4 p:SV_POSITION;float3 local:TEXCOORD0;float3 world:TEXCOORD1;float3 n:TEXCOORD2;float2 uv:TEXCOORD3;};
   V vert(A i)
   {
    V o;float3 p=i.p.xyz;
    if(_Part>.5&&_Part<1.5)
    {
     float flutter=sin(i.uv.y*18.8495559+i.uv.x*8-_Clock*3)*sin(i.uv.x*3.14159265)*.012;
     p+=i.n*flutter;
    }
    o.local=p;o.world=TransformObjectToWorld(p);o.p=TransformWorldToHClip(o.world);
    o.n=TransformObjectToWorldNormal(i.n);o.uv=i.uv;return o;
   }
   half4 frag(V i):SV_Target
   {
    if(_Part>1.5)
    {
     float2 p=i.uv*2-1;float r2=dot(p,p);
     float halo=exp(-r2*6)*(1-smoothstep(.55,1,r2));
     return half4(ShiftFlightHue(float3(.045,.12,.65))*halo*_Gain,1);
    }
    float3 normal=normalize(i.n),view=GetWorldSpaceNormalizeViewDir(i.world);
    float facing=abs(dot(normal,view));float rim=pow(1-facing,2.1);
    float3 color;
    if(_Part<.5)
    {
     float3 p=i.local*2;float angle=atan2(p.z,p.y);
     float flow=.5+.5*sin(p.x*9+angle*3-_Clock*4+.6*sin(angle*2+_Clock));
     float white=pow(facing,12)*(.28+.72*smoothstep(-.9,.6,p.x));
     color=float3(.035,.12,.85)*(.30+.20*rim);
     color+=float3(.02,.65,1)*flow*.45;
     color+=float3(.72,.95,1.15)*white*.85;
     color+=float3(.30,.035,.70)*rim*.30;
     color*=lerp(.12,1,smoothstep(0,.38,facing));
    }
    else
    {
     float u=i.uv.x,phi=i.uv.y*6.28318530718;
     float stripe=pow(.5+.5*sin(phi*3+u*11-_Clock*3+.4*sin(phi*2)),7);
     float rearFade=1-smoothstep(.55,.97,u);
     float front=1-smoothstep(.05,.65,u);
     float3 tint=lerp(float3(.035,.55,1.1),float3(.35,.06,.90),smoothstep(.45,1,u));
     color=tint*(rim*.72+stripe*.065+front*.025);
     color+=float3(.35,.85,1.1)*pow(rim,4)*front*.30;
     color*=(.86+.06*sin(_Clock*2.5+phi))*rearFade;
    }
    return half4(ShiftFlightHue(color)*_Gain,1);
   }
   ENDHLSL
  }
 }
}
