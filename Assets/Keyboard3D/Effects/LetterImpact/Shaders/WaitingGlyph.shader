Shader "KeyboardGame/Letters/WaitingGlyph"
{
    Properties { _Color("Color",Color)=(0.55,0.55,0.55,1) _Visible("Visible",Float)=1 }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
        Pass
        {
            Cull Back ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A {float4 positionOS:POSITION;};
            struct V {float4 positionCS:SV_POSITION;};
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;float _Visible;
            CBUFFER_END
            V vert(A i){V o;o.positionCS=TransformObjectToHClip(i.positionOS.xyz);return o;}
            half4 frag(V i):SV_Target{clip(_Visible-0.5);return _Color;}
            ENDHLSL
        }
    }
}
