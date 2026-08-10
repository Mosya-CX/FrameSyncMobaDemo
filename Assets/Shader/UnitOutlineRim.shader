Shader "MOBA/UnitOutlineRim"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (0, 0, 0, 1)
        _OutlineWidth ("Outline Width", Float) = 0.2
        _OutlineAlpha ("Outline Alpha", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+1"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "Outline"
            Cull Front
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float _OutlineWidth;
                float _OutlineAlpha;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 normalWS =
                    TransformObjectToWorldNormal(
                        input.normalOS);
                float3 positionWS =
                    TransformObjectToWorld(
                        input.positionOS.xyz);
                positionWS +=
                    normalWS * _OutlineWidth;
                output.positionHCS =
                    TransformWorldToHClip(
                        positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return half4(
                    _OutlineColor.rgb,
                    _OutlineColor.a *
                        _OutlineAlpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
