Shader "MindBridgeXR/Rounded Transparent"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (0, 0, 0, 0.38)
        _CornerRadius("Corner Radius", Range(0, 0.5)) = 0.12
        _EdgeSoftness("Edge Softness", Range(0, 0.05)) = 0.002
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _CornerRadius;
                half _EdgeSoftness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 maskUV : TEXCOORD1;
                half fogFactor : TEXCOORD2;
                float aspect : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.maskUV = input.uv;
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);

                float3 objectWidth = mul((float3x3)GetObjectToWorldMatrix(), float3(1, 0, 0));
                float3 objectHeight = mul((float3x3)GetObjectToWorldMatrix(), float3(0, 1, 0));
                output.aspect = length(objectWidth) / max(length(objectHeight), 1e-5);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;

                // The standard Unity Quad lies in local XY, so its world-scale ratio
                // keeps the corner radius circular under non-uniform scaling.
                float aspect = max(input.aspect, 1e-3);

                float radius = min(_CornerRadius, 0.5);
                float2 halfSize = float2(0.5 * aspect, 0.5);
                // Avoid HLSL's reserved "point" token on Direct3D shader compilers.
                float2 centeredUV = (input.maskUV - 0.5) * float2(aspect, 1.0);
                float2 corner = abs(centeredUV) - (halfSize - radius);
                float distanceToEdge =
                    length(max(corner, 0.0)) + min(max(corner.x, corner.y), 0.0) - radius;

                float softness = max((float)_EdgeSoftness, fwidth(distanceToEdge));
                color.a *= 1.0 - smoothstep(-softness, softness, distanceToEdge);
                color.rgb = MixFog(color.rgb, input.fogFactor);
                return color;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
