Shader "KeyboardGame/Letters/Surface"
{
    Properties
    {
        _FrontTopBrightness("Front top / trail brightness multiplier",Range(0,4))=1.2
        [MainColor] _TopColor("Top / trail color",Color)=(0.08,0.85,1,1)
        _BottomBrightness("Bottom brightness / main color",Range(0,1))=0.6
        [HideInInspector] _Baseline("Glyph gradient bottom",Float)=0
        [HideInInspector] _TopY("Glyph gradient top",Float)=0.1166667
        [HideInInspector] _GradientY("Mesh to letter local Y",Vector)=(0,1,0,0)
        _DarkBrightness("Side / back brightness",Range(0,1))=0.5
        [Enum(Front,0,Bevel,1,SideBack,2)] _Region("Material region",Float)=0
        [HDR] _BevelColor("Independent bevel color",Color)=(1,1,1,1)
        [Toggle] _BevelFollowMain("Bevel follows trail color",Float)=1
        _BevelBrightness("Bevel / trail brightness multiplier",Range(0,4))=1.5
        _BevelEmission("Independent bevel emission",Range(0,8))=0.5
        _Opacity("Visibility",Range(0,1))=1
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent"}
        Pass
        {
            Name "LetterSurface"
            Tags {"LightMode"="SRPDefaultUnlit"}
            Cull Back
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "LetterSurface.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _TopColor, _BevelColor, _GradientY;
                float _Baseline, _TopY, _DarkBrightness, _BottomBrightness, _FrontTopBrightness;
                float _Region, _BevelEmission, _Opacity, _BevelFollowMain, _BevelBrightness;
            CBUFFER_END
            struct Attributes {float4 positionOS:POSITION;float2 uv:TEXCOORD0;};
            struct Varyings {float4 positionCS:SV_POSITION;float3 positionOS:TEXCOORD0;float2 uv:TEXCOORD1;};
            Varyings vert(Attributes v)
            {
                Varyings o;o.positionCS=TransformObjectToHClip(v.positionOS.xyz);
                o.positionOS=v.positionOS.xyz;o.uv=v.uv;return o;
            }
            half4 frag(Varyings i):SV_Target
            {
                float alpha=saturate(_Opacity);
                if(_Region>0.5&&_Region<1.5)return half4(LetterBevelColor(_TopColor.rgb,_BevelColor.rgb,_BevelFollowMain,_BevelBrightness)*(1+_BevelEmission),alpha);
                float y=dot(float4(i.positionOS,1),_GradientY);
                float3 color=LetterGradient(y,_Baseline,_TopY,_TopColor.rgb,_FrontTopBrightness,_BottomBrightness,_DarkBrightness,_Region>1.5?1:0);
                return half4(LetterBodyOutput(color),alpha);
            }
            ENDHLSL
        }
    }
}
