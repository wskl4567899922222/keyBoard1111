Shader "Keyboard/Preview/KeyWaterWave"
{
 Properties{_Age("Age",Float)=0 _Layer("Layer",Float)=0 _Opacity("Opacity",Float)=1 _HueShift("Hue shift",Float)=0 _ZTest("[ZTest]",Float)=4}
 SubShader
 {
  Tags{"RenderPipeline"="UniversalPipeline" "Queue"="Transparent+2"}
  Pass
  {
   Blend SrcAlpha OneMinusSrcAlpha Cull Off ZWrite Off ZTest [_ZTest]
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "EnergyFlightHue.hlsl"
   float _Age,_Layer,_Opacity;
   struct A{float4 p:POSITION;float3 n:NORMAL;float2 uv:TEXCOORD0;float4 color:COLOR;};
   struct V{float4 p:SV_POSITION;float3 n:TEXCOORD1;float3 world:TEXCOORD2;float2 uv:TEXCOORD0;float4 color:COLOR;};
   V vert(A i){V o;o.p=TransformObjectToHClip(i.p.xyz);o.world=TransformObjectToWorld(i.p.xyz);o.n=TransformObjectToWorldNormal(i.n);o.uv=i.uv;o.color=i.color;return o;}
   half4 frag(V i):SV_Target
   {
    float r=saturate(i.uv.x),angle=i.uv.y;
    float3 n=normalize(i.n+float3(.00001,.00001,.00001));
    float fresnel=pow(saturate(1-abs(dot(n,GetWorldSpaceNormalizeViewDir(i.world)))),2);
    if(_Layer>2.5)
    {
     float spec=pow(saturate(dot(n,normalize(float3(-.4,1,-.3)))),18);
     float3 liquid=lerp(float3(.018,.15,.35),float3(.25,.66,1),fresnel)+float3(.65,.84,1)*spec*.8;
     float alpha=(.46+.34*fresnel+.20*spec)*_Opacity;
     if(_Layer<3.5)
     {
      float sides=smoothstep(0,.08,i.uv.x)*(1-smoothstep(.92,1,i.uv.x));
      float hole=length((i.uv-float2(.42,.73))*float2(8,7));
      float hole2=length((i.uv-float2(.72,.87))*float2(11,9));
      alpha*=sides*smoothstep(.62,.82,hole)*smoothstep(.55,.78,hole2);
      liquid+=float3(.40,.15,.65)*fresnel*.18;
     }
     else alpha*=i.color.a;
     return half4(clamp(ShiftFlightHue(liquid),0,1.5),saturate(alpha));
    }
    float wave=.5+.5*sin(r*18-_Age*30+sin(angle*4+_Age*8)*.85);
    float bands=pow(saturate(wave),9);
    float glint=pow(saturate(dot(n,normalize(float3(-.4,1,-.3)))),22);
    float breakup=.55+.45*sin(angle*3+r*7+_Age*9);
    float inner=_Layer<.5?0:_Layer<1.5?.34:.63;
    float outer=_Layer> .5&&_Layer<1.5?.78:1;
    float rim=(1-smoothstep(outer-.045,outer,r));
    if(_Layer>.5)rim*=smoothstep(inner,inner+.035,r);
    float3 color=lerp(float3(.015,.09,.26),float3(.035,.42,.90),.25+.55*fresnel);
    color+=float3(.35,.73,1)*(bands*.65*breakup+glint*.65);
    color+=float3(.40,.14,.65)*bands*.15;
    color=ShiftFlightHue(color);
    float opacity=(.23+.28*fresnel+.30*bands+.15*glint)*_Opacity*rim;
    if(_Layer>.5)opacity*=.50;
    return half4(clamp(color,0,1.5),saturate(opacity));
   }
   ENDHLSL
  }
 }
}
