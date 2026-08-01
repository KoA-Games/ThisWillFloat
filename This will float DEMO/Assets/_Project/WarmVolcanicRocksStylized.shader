Shader "This Will Float/Warm Volcanic Rocks Stylized"
{
    Properties
    {
        _BaseColor("Warm Gray Brown", Color) = (0.318, 0.286, 0.259, 1)
        _ShadowColor("Muted Charcoal Brown", Color) = (0.208, 0.196, 0.184, 1)
        _HighlightColor("Dusty Warm Gray", Color) = (0.455, 0.416, 0.376, 1)
        _Smoothness("Smoothness", Range(0, 1)) = 0.02
        _Metallic("Metallic", Range(0, 1)) = 0
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
            Name "ForwardWarmRock"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                half3 normalWS : TEXCOORD0;
                float4 shadowCoord : TEXCOORD1;
                half fogFactor : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _ShadowColor;
                float4 _HighlightColor;
                float _Smoothness;
                float _Metallic;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = positions.positionCS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.shadowCoord = GetShadowCoord(positions);
                output.fogFactor = ComputeFogFactor(positions.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 normalWS = normalize(input.normalWS);
                Light mainLight = GetMainLight(input.shadowCoord);
                half ndl = saturate(dot(normalWS, mainLight.direction));
                half shadow = lerp(0.62h, 1.0h, mainLight.shadowAttenuation);
                half lightAmount = saturate((ndl * 0.78h + 0.22h) * shadow);

                // Broad three-tone lighting preserves the prefab's low-poly planes while
                // keeping every lighting state inside the approved warm volcanic palette.
                half3 color = lerp(_ShadowColor.rgb, _BaseColor.rgb,
                    smoothstep(0.16h, 0.58h, lightAmount));
                color = lerp(color, _HighlightColor.rgb,
                    smoothstep(0.64h, 0.98h, lightAmount) * 0.72h);
                color *= lerp(0.94h, 1.03h, saturate(normalWS.y));
                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
    }
}
