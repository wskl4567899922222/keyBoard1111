Shader "KeyboardGame/WordEnergy"
{
    Properties{_Tint("Energy color",Color)=(.1,.75,1,1) _GradientTex("Stable word colors",2D)="white"{} _Intensity("Intensity",Float)=2.23 _CoreRatio("Core / halo",Float)=.08 _PointGain("Finishing flash",Float)=1 _Gather("Center gathering",Float)=0 _Disturbance("Organic edge",Float)=.10 _FlowTime("Flow time",Float)=0 _LeftTint("Left energy",Color)=(1,.3,.1,1) _RightTint("Right energy",Color)=(.1,.7,1,1)}
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent+15" "RenderType"="Transparent"}
        Pass
        {
            Cull Off ZWrite Off Blend One OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A{float4 pos:POSITION;float2 uv:TEXCOORD0;float2 mode:TEXCOORD1;half4 color:COLOR;};
            struct V{float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float mode:TEXCOORD1;half alpha:COLOR;};
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint,_LeftTint,_RightTint;float _Intensity,_CoreRatio,_PointGain,_Gather,_Disturbance,_FlowTime;
            CBUFFER_END
            TEXTURE2D(_GradientTex);SAMPLER(sampler_GradientTex);
            V vert(A a){V o;o.pos=TransformObjectToHClip(a.pos.xyz);o.uv=a.uv;o.mode=a.mode.x;o.alpha=a.color.a;return o;}
            half4 frag(V i):SV_Target
            {
                float2 p=i.uv*2-1;float edge=1-smoothstep(.52,1,abs(p.x));
                float taper=lerp(.28,1,sqrt(saturate(1-p.x*p.x)));
                float swell=_Gather*exp2(-p.x*p.x*18);
                float thickness=max(_CoreRatio*taper*(1+3*swell),fwidth(p.y)*.62);
                float lineCore=exp2(-p.y*p.y/(thickness*thickness)*1.6)*edge;
                float lineGlow=(exp2(-p.y*p.y*lerp(28,10,swell))*.23+exp2(-p.y*p.y*5)*.11)*edge*(1-smoothstep(.80,1,abs(p.y)));
                float angle=atan2(p.y,p.x);
                float wobble=1+_Disturbance*(.62*sin(angle*3+_FlowTime*1.7)+.38*sin(angle*5-_FlowTime*1.1));
                float radius=length(p)/wobble;float r2=radius*radius;
                float roundEdge=(1-smoothstep(.65,1,radius))*(1-smoothstep(.85,1,max(abs(p.x),abs(p.y))));
                float dotCore=exp2(-r2*30)*roundEdge;float dotGlow=(exp2(-r2*7)*.25+exp2(-r2*3)*.08)*roundEdge;
                float core=lerp(lineCore,dotCore,i.mode),glow=lerp(lineGlow,dotGlow,i.mode);
                float profile=core+glow;float alpha=saturate(profile*i.alpha);
                half3 wordColor=SAMPLE_TEXTURE2D(_GradientTex,sampler_GradientTex,float2(i.uv.x,.5)).rgb;
                float join=smoothstep(-.2,.2,p.y+.18*sin(p.x*4-_FlowTime*.7));
                half3 joined=lerp(lerp(_LeftTint.rgb,_RightTint.rgb,join),_Tint.rgb,smoothstep(.70,1,_Gather));
                half3 tint=lerp(lerp(wordColor,joined,i.mode),half3(1,1,1),saturate(core)*.32);
                // Premultiplied local glow; the scene's post-processing is unchanged.
                return half4(tint*profile*i.alpha*_Intensity*lerp(1,_PointGain,i.mode),alpha);
            }
            ENDHLSL
        }
    }
}
