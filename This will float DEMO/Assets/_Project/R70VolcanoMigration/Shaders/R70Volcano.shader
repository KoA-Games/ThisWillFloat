Shader "This Will Float/R70 Volcano"
{
    Properties
    {
        _BaseMap("Baked R70 Albedo", 2D) = "white" {}
        [Normal] _NormalMap("Baked R70 Tangent Normal", 2D) = "bump" {}
        _RoughnessMap("Baked R70 Roughness", 2D) = "white" {}
        _MacroMap("R70 Macro Relief", 2D) = "gray" {}
        _AshMap("R70 Eroded Ash Detail", 2D) = "gray" {}
        _BaseColor("Tint", Color) = (1, 1, 1, 1)
        _NormalStrength("Normal Strength", Range(0, 2)) = 1
        _Roughness("Roughness", Range(0, 1)) = 0.9
        _MacroColorStrength("Macro Color Relief", Range(0, 0.3)) = 0.12
        _AshStrength("Eroded Ash Strength", Range(0, 0.6)) = 0.25
        _AshScale("Eroded Ash Generated Scale", Range(0.5, 5)) = 2.35
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        Cull Back
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
            TEXTURE2D(_RoughnessMap); SAMPLER(sampler_RoughnessMap);
            TEXTURE2D(_MacroMap); SAMPLER(sampler_MacroMap);
            TEXTURE2D(_AshMap); SAMPLER(sampler_AshMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _NormalStrength;
                half _Roughness;
                half _MacroColorStrength;
                half _AshStrength;
                half _AshScale;
            CBUFFER_END

            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float4 tangentOS:TANGENT; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionHCS:SV_POSITION; float3 positionWS:TEXCOORD0; half3 normalWS:TEXCOORD1; half4 tangentWS:TEXCOORD2; float2 uv:TEXCOORD3; float4 shadowCoord:TEXCOORD4; half fogFactor:TEXCOORD5; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                VertexNormalInputs normals = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.normalWS = normals.normalWS;
                output.tangentWS = half4(normals.tangentWS, input.tangentOS.w * GetOddNegativeScale());
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.shadowCoord = GetShadowCoord(positions);
                output.fogFactor = ComputeFogFactor(positions.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb;
                half macro = SAMPLE_TEXTURE2D(_MacroMap, sampler_MacroMap, input.uv).r;
                baseColor *= 1.0h + (macro - 0.5h) * _MacroColorStrength;
                // Blender's Pass04C uses Generated coordinates scaled 2.35 for
                // the eroded-ash layer. Reintroduce its fine directional color
                // variation after the 2K albedo bake, but suppress it over the
                // deliberately green foothill regions.
                float2 ashUV = float2((input.positionWS.x + 80.0) / 160.0,
                                      input.positionWS.y / 75.0) * _AshScale;
                half3 ashColor = SAMPLE_TEXTURE2D(_AshMap, sampler_AshMap, ashUV).rgb;
                half greenMask = saturate((baseColor.g - max(baseColor.r, baseColor.b)) * 6.0h);
                half upperRockMask = smoothstep(8.0h, 34.0h, input.positionWS.y);
                half ashMask = (0.18h + upperRockMask * 0.82h) * (1.0h - greenMask * 0.88h) * _AshStrength;
                half ashValue = dot(ashColor, half3(0.299h, 0.587h, 0.114h));
                baseColor *= lerp(1.0h, lerp(0.78h, 1.22h, ashValue), ashMask);
                baseColor = lerp(baseColor, ashColor, ashMask * 0.16h);
                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv), _NormalStrength);
                half3 bitangentWS = input.tangentWS.w * cross(input.normalWS, input.tangentWS.xyz);
                half3 normalWS = normalize(TransformTangentToWorld(normalTS, half3x3(input.tangentWS.xyz, bitangentWS, input.normalWS)));
                half roughness = saturate(SAMPLE_TEXTURE2D(_RoughnessMap, sampler_RoughnessMap, input.uv).r * _Roughness);
                Light mainLight = GetMainLight(input.shadowCoord);
                half ndl = saturate(dot(normalWS, mainLight.direction));
                // Receive clear foliage shadows without crushing the volcano's
                // self-shadowed gullies into a single near-black band.
                half shadow = lerp(0.36h, 1.0h, mainLight.shadowAttenuation);
                half3 ambient = SampleSH(normalWS) * 0.48h + 0.19h;
                half3 diffuse = mainLight.color * ndl * shadow;
                half3 viewDir = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                half3 halfDir = SafeNormalize(mainLight.direction + viewDir);
                half specPower = lerp(96.0h, 3.0h, roughness);
                half specular = pow(saturate(dot(normalWS, halfDir)), specPower) * (1.0h - roughness) * 0.12h;
                half3 lit = baseColor * (ambient + diffuse) + mainLight.color * specular * shadow;
                lit = MixFog(lit, input.fogFactor);
                return half4(lit, 1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
