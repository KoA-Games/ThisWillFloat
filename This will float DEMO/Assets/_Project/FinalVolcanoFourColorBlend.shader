Shader "This Will Float/Final Volcano Four Color Blend"
{
    Properties
    {
        _BaseColor("Warm Brown", Color) = (0.52, 0.41, 0.31, 1)
        _MiddleColor("Gray Brown", Color) = (0.46, 0.42, 0.36, 1)
        _UpperColor("Muted Gray", Color) = (0.39, 0.40, 0.38, 1)
        _SummitColor("Volcanic Charcoal", Color) = (0.32, 0.335, 0.35, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float ridgeShade : TEXCOORD1;
                float4 color : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _MiddleColor;
                float4 _UpperColor;
                float4 _SummitColor;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.ridgeShade = input.uv.x;
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 weights = max(input.color, 0.0h);
                weights /= max(0.0001h, weights.r + weights.g + weights.b + weights.a);
                half4 color = _BaseColor * weights.r
                    + _MiddleColor * weights.g
                    + _UpperColor * weights.b
                    + _SummitColor * weights.a;

                half3 normalWS = normalize(input.normalWS);
                half3 broadLightDirection = normalize(half3(-0.38h, 0.82h, -0.43h));
                half light = saturate(dot(normalWS, broadLightDirection) * 0.5h + 0.62h);
                color.rgb *= lerp(0.74h, 1.14h, light);
                color.rgb *= lerp(0.78h, 1.16h, saturate(input.ridgeShade));
                return color;
            }
            ENDHLSL
        }
    }
}
