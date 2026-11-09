Shader "Keyboard/Preview/LayeredEnergyTail"
{
 Properties { _Clock("Time",Float)=0 _Gain("Gain",Float)=1 _Layer("Layer",Float)=0 _HueShift("Hue shift",Float)=0 _ZTest("[ZTest]",Float)=4 }
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
   float _Clock,_Gain,_Layer;
   struct A{float4 p:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
   struct V{float4 p:SV_POSITION;float2 uv:TEXCOORD0;float4 color:TEXCOORD1;};
   V vert(A i){V o;o.p=TransformObjectToHClip(i.p.xyz);o.uv=i.uv;o.color=i.color;return o;}
   half4 frag(V i):SV_Target
   {
    float s=saturate(i.uv.x),crossBand=saturate(i.uv.y)*2-1,v=abs(crossBand);
    float terminalSoftening=smoothstep(.78,1,s);
    float edgeFade=1-smoothstep(lerp(.88,.45,terminalSoftening),1,v);
    // Use distance behind the head so light travels through the tail as it grows.
    float phase=i.color.r*145-_Clock*14+_Layer*1.7;
    float flow=.76+.24*sin(phase+v*3);
    flow*=.91+.09*sin(i.color.r*75-_Clock*4+_Layer*2.1);
    // Interpolated terminal UVs can exceed one by a few ulps with MSAA.
    // Fractional powers of a negative base poison HDR/Bloom with non-finite values.
    float front=pow(saturate(1-s),1.4);
    // Long, irregular colour patches travel down every band independently of twist.
    float tintPhase=i.color.r*(105+_Layer*11)-_Clock*(6.5+_Layer*.7)+_Layer*1.8;
    tintPhase+=.45*sin(i.color.r*43-_Clock*2.2+_Layer);
    float tintFlow=smoothstep(.17,.86,.5+.5*sin(tintPhase));
    float3 body,light,rimColor;
    if(_Layer<.5)
    {
     float blend=tintFlow*.66;
     body=lerp(float3(.015,.34,1),float3(.40,.065,.98),blend);
     light=lerp(float3(.10,.76,1.05),float3(.49,.27,1.05),blend);
     rimColor=lerp(float3(.20,.50,1.05),float3(.65,.17,1),blend);
    }
    else if(_Layer<1.5)
    {
     body=lerp(float3(.035,.29,1),float3(.53,.045,.91),tintFlow*.90);
     light=lerp(float3(.08,.56,1.05),float3(.62,.23,1),tintFlow*.85);
     rimColor=lerp(float3(.18,.48,1),float3(.83,.20,.90),tintFlow*.80);
    }
    else if(_Layer<2.5)
    {
     body=lerp(float3(.025,.30,.98),float3(.015,.77,.68),tintFlow*.72);
     light=lerp(float3(.12,.56,1.05),float3(.05,.88,.83),tintFlow*.74);
     rimColor=lerp(float3(.25,.39,1.05),float3(.10,.95,.77),tintFlow*.76);
    }
    else if(_Layer<4.5)
    {
     float accent=(_Layer<3.5)?tintFlow*.82:tintFlow*.66;
     float3 accentColor=(_Layer<3.5)?float3(.46,.10,.96):float3(.035,.74,.84);
     body=lerp(float3(.025,.30,1),accentColor,accent);
     light=lerp(float3(.16,.60,1.05),accentColor*1.15,accent);
     rimColor=light;
    }
    else
    {
     float3 glow=float3(.035,.045,.45)*exp(-pow(v/.8,2))*.24;
     return half4(ShiftFlightHue(glow)*edgeFade*flow*i.color.a*_Gain,1);
    }
    float broad=exp(-pow(v/.70,2));
    float highlightCenter=.09*sin(i.color.r*100-_Clock*6);
    float core=exp(-pow((crossBand-highlightCenter)/(_Layer<.5?.32:.23),2));
    float3 color=body*exp(-pow(v/.84,2))*.38;
    color+=light*broad*(.38+.20*front);
    color+=(_Layer<.5?float3(.58,.88,1):light)*core*front*(_Layer<.5?.18:.20);
    float edgePosition=.68+.055*sin(phase*.65);
    float edgeWidth=(_Layer>.5&&_Layer<1.5)?.085:.060;
    float edge=exp(-pow((crossBand-edgePosition)/edgeWidth,2));
    edge*=.35+.65*pow(.5+.5*sin(phase*.8+1),2);
    color+=rimColor*edge*(_Layer<.5?.13:.32)*(1-terminalSoftening);
    float tipDefinition=1;
    float3 radiance=color*edgeFade*flow*saturate(i.color.a)*_Gain*tipDefinition;
    radiance=all(radiance==radiance)?clamp(radiance,0,4):0;
    return half4(ShiftFlightHue(radiance),1);
   }
   ENDHLSL
  }
 }
}
