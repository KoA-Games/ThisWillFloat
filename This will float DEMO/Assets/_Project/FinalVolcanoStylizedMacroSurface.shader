Shader "This Will Float/Final Volcano Stylized Macro Surface"
{
    Properties
    {
        _SoilColor("Lower Volcanic Soil", Color) = (0.502, 0.376, 0.267, 1)
        _MidColor("Mid Slope Ash Soil", Color) = (0.427, 0.384, 0.325, 1)
        _UpperRockColor("Upper Exposed Rock", Color) = (0.322, 0.333, 0.325, 1)
        _SummitColor("Summit Volcanic Charcoal", Color) = (0.224, 0.235, 0.239, 1)
        _FlowColor("Muted Volcanic Flow", Color) = (0.255, 0.239, 0.216, 1)
        _ShadowTint("Soft Volcanic Shadow Multiplier", Color) = (0.68, 0.67, 0.64, 1)
        _MacroStrength("Macro Variation", Range(0, 0.2)) = 0.08
        _FlowStrength("Broad Flow Strength", Range(0, 0.35)) = 0.24
        _Smoothness("Smoothness", Range(0, 1)) = 0.025
        _Metallic("Metallic", Range(0, 1)) = 0
        _VolcanoCenter("Volcano Center XZ", Vector) = (35, 75, 350, 0)
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
            Name "ForwardLitStylized"
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
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half ridgeShade : TEXCOORD2;
                half4 color : COLOR;
                float4 shadowCoord : TEXCOORD3;
                half fogFactor : TEXCOORD4;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _SoilColor;
                float4 _MidColor;
                float4 _UpperRockColor;
                float4 _SummitColor;
                float4 _FlowColor;
                float4 _ShadowTint;
                float4 _VolcanoCenter;
                float _MacroStrength;
                float _FlowStrength;
                float _Smoothness;
                float _Metallic;
            CBUFFER_END

            float AngularDistance(float angle, float center)
            {
                float delta = angle - center;
                return abs(atan2(sin(delta), cos(delta)));
            }

            float BroadFlow(float angle, float center, float width)
            {
                float distance = AngularDistance(angle, center) / width;
                return exp(-distance * distance);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.ridgeShade = input.uv.x;
                output.color = input.color;
                output.shadowCoord = GetShadowCoord(positions);
                output.fogFactor = ComputeFogFactor(positions.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 weights = max(input.color, 0.0h);
                weights /= max(0.0001h, weights.r + weights.g + weights.b + weights.a);

                // Preserve the four authored height families, then break their boundaries with
                // broad world-space fields. No sampled texture or object-space UV is involved.
                half soilWeight = saturate(weights.r);
                half midWeight = saturate(weights.g);
                half rockWeight = saturate(weights.b);
                half summitWeight = saturate(weights.a);

                float2 offset = input.positionWS.xz - _VolcanoCenter.xy;
                float radial01 = saturate(length(offset) / max(1.0, _VolcanoCenter.z));
                float2 macroPointA = offset / 168.0;
                float2 macroPointB = offset / 235.0;
                float macroA = sin(macroPointA.x * 1.07 + sin(macroPointA.y * 0.73 + 0.8));
                float macroB = cos(macroPointB.y * 1.21 - macroPointB.x * 0.58 + 1.4);
                float macroC = sin((macroPointA.x + macroPointB.y) * 0.84 - 0.65);
                float macro = macroA * 0.47 + macroB * 0.34 + macroC * 0.19;

                // Slowly move the lower/mid and mid/upper ownership in opposite directions.
                // This turns circular layer borders into a few large, legible color fields.
                half lowerPatchShift = (half)(macro * 0.105) * (soilWeight + midWeight);
                soilWeight = saturate(soilWeight - lowerPatchShift * 0.72h);
                midWeight = saturate(midWeight + lowerPatchShift);
                half upperPatchShift = (half)((macroB - macroA * 0.32) * 0.085) * (midWeight + rockWeight);
                midWeight = saturate(midWeight - upperPatchShift * 0.68h);
                rockWeight = saturate(rockWeight + upperPatchShift);

                float angle = atan2(offset.y, offset.x);
                float flows = max(BroadFlow(angle, -2.42, 0.15), BroadFlow(angle, -0.74, 0.22));
                flows = max(flows, BroadFlow(angle, 0.69, 0.17) * 0.82);
                flows = max(flows, BroadFlow(angle, 1.91, 0.25) * 0.68);
                flows = max(flows, BroadFlow(angle, 2.72, 0.19) * 0.74);
                float flowWindow = smoothstep(0.08, 0.16, radial01) *
                    (1.0 - smoothstep(0.74, 0.93, radial01));
                float brokenFlow = flows * flowWindow *
                    (0.67 + 0.33 * saturate(macro * 0.5 + 0.5));

                // A small amount of upper rock follows selected flows down the mountain. This
                // is color ownership only; vertex positions and normals remain untouched.
                rockWeight = saturate(rockWeight + (half)(brokenFlow * 0.18) * (soilWeight + midWeight));
                summitWeight = saturate(summitWeight + (half)(brokenFlow * 0.055) *
                    (half)(1.0 - smoothstep(0.34, 0.49, radial01)));

                half materialSum = max(0.0001h, soilWeight + midWeight + rockWeight + summitWeight);
                soilWeight /= materialSum;
                midWeight /= materialSum;
                rockWeight /= materialSum;
                summitWeight /= materialSum;

                half3 surface = _SoilColor.rgb * soilWeight
                    + _MidColor.rgb * midWeight
                    + _UpperRockColor.rgb * rockWeight
                    + _SummitColor.rgb * summitWeight;

                // Five unequal and interrupted streaks provide volcanic identity from gameplay
                // distance without creating repeated grooves or noisy small-scale detail.
                surface = lerp(surface, _FlowColor.rgb,
                    saturate((half)(brokenFlow * _FlowStrength * 1.65)));

                float wideFlows = max(BroadFlow(angle, -2.42, 0.26), BroadFlow(angle, -0.74, 0.35));
                wideFlows = max(wideFlows, BroadFlow(angle, 0.69, 0.29) * 0.82);
                wideFlows = max(wideFlows, BroadFlow(angle, 1.91, 0.39) * 0.68);
                wideFlows = max(wideFlows, BroadFlow(angle, 2.72, 0.31) * 0.74);
                float flowShoulder = saturate(wideFlows - flows * 0.72) * flowWindow;
                surface *= 1.0h + (half)(flowShoulder * 0.038);

                // World-space macro value and the pre-authored broad ridge field remain subtle.
                surface *= 1.0h + (half)(macro * _MacroStrength * 1.25);
                surface *= lerp(0.86h, 1.12h, saturate(input.ridgeShade));

                // Blend the authored smooth normal with the actual triangle plane. This makes
                // the existing mesh read as intentional low-poly rock without a noisy normal
                // texture, while retaining enough smoothness for the broad hero ridges.
                half3 faceNormal = normalize(cross(ddy(input.positionWS), ddx(input.positionWS)));
                faceNormal *= faceNormal.y < 0.0h ? -1.0h : 1.0h;
                half3 normalWS = normalize(lerp(normalize(input.normalWS), faceNormal, 0.56h));
                Light mainLight = GetMainLight(input.shadowCoord);
                half ndl = saturate(dot(normalWS, mainLight.direction));
                half wrappedLight = ndl * 0.72h + 0.28h;
                half shadow = lerp(0.72h, 1.0h, mainLight.shadowAttenuation);
                half slope = saturate(1.0h - normalWS.y);
                half slopeShape = lerp(1.025h, 0.98h, slope);

                // Gently quantized broad lighting gives readable low-poly planes without a
                // repeated normal map or high-frequency texture.
                half bandedLight = floor(wrappedLight * 4.0h + 0.5h) / 4.0h;
                half stylizedLight = lerp(wrappedLight, bandedLight, 0.28h) * shadow;
                half3 lightMultiplier = lerp(_ShadowTint.rgb, mainLight.color, stylizedLight);
                half3 lit = surface * lightMultiplier;
                lit *= slopeShape;
                lit = MixFog(lit, input.fogFactor);
                return half4(lit, 1.0h);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
    }
}
