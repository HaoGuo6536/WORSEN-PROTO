// ============================================================================
// FogDoorway.shader
// ============================================================================
// PURPOSE:
//   Draws soft grey doorway haze without billboarding, extrusion or depth writes.
//   Its bounded alpha preserves shapes behind the sheet from either side.
// ARCHITECTURAL ROLE:
//   Driver rendering asset (§7a) · Presentation · Fog.
// KEY RESPONSIBILITIES:
//   - Blend a soft-edged, lightly mottled doorway sheet with the existing scene.
// DEPENDENCIES:
//   - Unity URP shader core and FogDoorwayDriver's private material colour.
// USAGE NOTES:
//   Serialized on FogDriverConfig; no runtime shader lookup. No vertex displacement.
// ============================================================================
Shader "Worsen/Collapse Doorway Haze"
{
    Properties { _HazeColor("Haze colour", Color) = (0.62, 0.65, 0.68, 0.22) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _HazeColor;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 edge = min(input.uv, 1.0 - input.uv);
                float softness = smoothstep(0.0, 0.12, min(edge.x, edge.y));
                float wisps = 0.82 + 0.18 * sin(input.uv.x * 23.0 + sin(input.uv.y * 19.0));
                return half4(_HazeColor.rgb, saturate(_HazeColor.a) * softness * wisps);
            }
            ENDHLSL
        }
    }
}
