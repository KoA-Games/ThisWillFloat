Shader "This Will Float/VRST Crater Only Blend"
{
    Properties
    {
        _FloorColor("Crater Floor", Color) = (0.255, 0.19, 0.155, 1)
        _WallColor("Inner Wall", Color) = (0.335, 0.295, 0.255, 1)
        _RimColor("Existing Rim", Color) = (0.285, 0.295, 0.285, 1)
        _Smoothness("Smoothness", Range(0, 1)) = 0.025
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry+8"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ForwardCraterOnly"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite On
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float4 color : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _FloorColor;
                float4 _WallColor;
                float4 _RimColor;
                float _Smoothness;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 weights = max(input.color.rgb, half3(0, 0, 0));
                weights /= max(weights.r + weights.g + weights.b, 0.0001h);
                half3 surface = _FloorColor.rgb * weights.r
                    + _WallColor.rgb * weights.g
                    + _RimColor.rgb * weights.b;
                half3 normalWS = normalize(input.normalWS);
                half3 lightDirection = normalize(half3(-0.38h, 0.82h, -0.43h));
                half diffuse = saturate(dot(normalWS, lightDirection));
                half stylizedLight = lerp(0.72h, 1.12h, smoothstep(0.10h, 0.88h, diffuse));
                return half4(surface * stylizedLight, 1.0h);
            }
            ENDHLSL
        }
    }
}
