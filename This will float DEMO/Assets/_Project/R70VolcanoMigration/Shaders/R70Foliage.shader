Shader "This Will Float/R70 Foliage"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (0.22, 0.34, 0.20, 1)
        _Roughness("Roughness", Range(0, 1)) = 0.78
        _NoiseScale("World Noise Scale", Range(0.01, 2)) = 0.55
        _NoiseStrength("Value Variation", Range(0, 0.2)) = 0.06
        _VertexColorWeight("Vertex Color Weight", Range(0, 1)) = 0
        _AmbientLift("Ambient Lift", Range(0, 1)) = 0.26
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        Cull Off

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 color : COLOR;
                float4 shadowCoord : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Roughness;
                half _NoiseScale;
                half _NoiseStrength;
                half _VertexColorWeight;
                half _AmbientLift;
            CBUFFER_END

            float Hash31(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color;
                output.shadowCoord = GetShadowCoord(positionInputs);
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input, bool isFrontFace : SV_IsFrontFace) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half3 normalWS = normalize(input.normalWS) * (isFrontFace ? 1.0h : -1.0h);
                half3 vertexTint = lerp(1.0h.xxx, saturate(input.color.rgb), _VertexColorWeight);
                half noise = (half)Hash31(floor(input.positionWS * _NoiseScale)) * 2.0h - 1.0h;
                half3 albedo = _BaseColor.rgb * vertexTint * (1.0h + noise * _NoiseStrength);

                Light mainLight = GetMainLight(input.shadowCoord);
                half ndl = saturate(dot(normalWS, mainLight.direction));
                half wrapped = saturate(ndl * (1.0h - _AmbientLift) + _AmbientLift);
                half shadow = lerp(0.66h, 1.0h, mainLight.shadowAttenuation);
                half3 lit = albedo * (mainLight.color * wrapped * shadow + 0.24h);
                lit = MixFog(lit, input.fogFactor);
                return half4(lit, 1.0h);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
