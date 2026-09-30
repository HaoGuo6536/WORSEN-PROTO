// ============================================================================
// CollapseFog.shader
// ============================================================================
// PURPOSE:
//   Absorbs scene light through the collapse density field, with a cold thin edge.
//   Depth bounds the march so opaque walls occlude the field behind them.
// ARCHITECTURAL ROLE:
//   Driver shader · Presentation · Fog.
// KEY RESPONSIBILITIES:
//   - Integrate extinction with bounded jittered steps and transmittance early exit.
// DEPENDENCIES:
//   - URP depth and Core shader library; globals published by FogDriver.
// USAGE NOTES:
//   SPEC-001 section 12 has no shader placement rule; owned under Assets/Shaders/Fog.
//   RGB output is integrated radiance; alpha is remaining transmission, not opacity.
// ============================================================================
Shader "Worsen/Fog/CollapseFog"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Collapse Fog"
            ZWrite Off ZTest Always Cull Off
            Blend One SrcAlpha, Zero One
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            TEXTURE3D(_WorsenFogDensity);
            SAMPLER(sampler_WorsenFogDensity);
            float4x4 _WorsenFogWorldToGrid;
            float3 _WorsenFogMin, _WorsenFogMax;
            float4 _WorsenFogBody, _WorsenFogThin, _WorsenFogOptics, _WorsenFogMarch;
            float _WorsenFogEnabled;
            struct Varyings { float4 positionCS : SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(uint id : SV_VertexID)
            {
                Varyings output;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = GetFullScreenTriangleVertexPosition(id);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                if (_WorsenFogEnabled < .5) return half4(0, 0, 0, 1);
                float2 uv = input.positionCS.xy / _ScaledScreenParams.xy;
                float depth = SampleSceneDepth(uv);
                #if !UNITY_REVERSED_Z
                    depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, depth);
                #endif
                float3 endWS = ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);
                #if UNITY_REVERSED_Z
                    const float nearDepth = 1;
                #else
                    const float nearDepth = UNITY_NEAR_CLIP_VALUE;
                #endif
                float3 origin = ComputeWorldSpacePosition(uv, nearDepth, UNITY_MATRIX_I_VP);
                float3 delta = endWS - origin;
                float lengthWS = length(delta);
                float3 ray = delta / max(lengthWS, 1e-6);
                // Parallel slab axes must not divide by zero or produce NaNs.
                float3 safeRay = float3(ray.x >= 0 ? max(ray.x, 1e-6) : min(ray.x, -1e-6),
                    ray.y >= 0 ? max(ray.y, 1e-6) : min(ray.y, -1e-6), ray.z >= 0 ? max(ray.z, 1e-6) : min(ray.z, -1e-6));
                float3 first = (_WorsenFogMin - origin) / safeRay;
                float3 last = (_WorsenFogMax - origin) / safeRay;
                float3 lo = min(first, last), hi = max(first, last);
                float start = max(0, max(lo.x, max(lo.y, lo.z)));
                float finish = min(lengthWS, min(hi.x, min(hi.y, hi.z)));
                if (finish <= start) return half4(0, 0, 0, 1);
                int count = clamp((int)_WorsenFogMarch.x, 1, 48);
                float stepSize = (finish - start) / count;
                // Spatial jitter is stable: no uncontrolled per-frame shimmer.
                float jitter = frac(52.9829189 * frac(dot(input.positionCS.xy, float2(.06711056, .00583715))));
                float transmission = 1;
                float3 light = 0;
                [loop] for (int i = 0; i < 48; i++)
                {
                    if (i >= count || transmission <= _WorsenFogMarch.y) break;
                    float3 world = origin + ray * (start + (i + jitter) * stepSize);
                    float3 grid = mul(_WorsenFogWorldToGrid, float4(world, 1)).xyz;
                    float density = SAMPLE_TEXTURE3D_LOD(_WorsenFogDensity, sampler_WorsenFogDensity, grid, 0).r;
                    float opacity = 1 - exp(-density * _WorsenFogOptics.x * _WorsenFogOptics.y * stepSize);
                    float thin = saturate(density / _WorsenFogOptics.w);
                    thin = 4 * thin * (1 - thin); // Exactly zero in clear and thick voxels.
                    float3 emission = _WorsenFogThin.rgb * (_WorsenFogOptics.z * thin);
                    light += transmission * opacity * (_WorsenFogBody.rgb + emission);
                    transmission *= 1 - opacity;
                }
                return half4(light, transmission);
            }
            ENDHLSL
        }
    }
}
