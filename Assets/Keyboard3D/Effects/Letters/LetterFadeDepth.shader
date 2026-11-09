Shader "KeyboardGame/Letters/FadeDepth"
{
    Properties { _Opacity("Visibility",Range(0,1))=1 }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-1"}
        Pass
        {
            Name "SolidLetterDepth"
            Tags {"LightMode"="SRPDefaultUnlit"}
            Cull Back ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float _Opacity;
            CBUFFER_END
            float4 vert(float4 positionOS:POSITION):SV_POSITION{return TransformObjectToHClip(positionOS.xyz);}
            half4 frag():SV_Target{clip(_Opacity-0.000001);return 0;}
            ENDHLSL
        }
    }
}
