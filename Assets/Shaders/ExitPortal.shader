// ============================================================================
// ExitPortal.shader
// PURPOSE: A front-only, aperture-bounded view of a quiet dusk field. The sky is
// evaluated from the viewing ray, not stuck to the door like a painted picture.
// ROLE: URP visual asset, owned by Floor's exported EscapeSurface material.
// RESPONSIBILITIES: Clip back views; shade sky/sun/horizon; occlude the local room.
// DEPENDENCIES: URP Core.hlsl. No textures, render targets, cameras or stencil bits.
// USAGE: Map the exported EscapeSurface slot to this shader in editor setup.
// The quad is local XY at Z=0.11, facing -Z. Its bounds are the aperture mask;
// depth testing preserves the frame/leaf and foreground occlusion. No shadow pass.
// ============================================================================
Shader "Worsen/ExitPortal"
{
    Properties
    {
        [HDR] _Zenith ("Dusk sky", Color) = (0.18, 0.38, 0.62, 1)
        [HDR] _Horizon ("Warm horizon", Color) = (1.0, 0.64, 0.32, 1)
        [HDR] _Field ("Quiet field", Color) = (0.18, 0.24, 0.095, 1)
        [HDR] _Sun ("Sun", Color) = (3.0, 2.2, 1.1, 1)
        _SunDirection ("Sun direction in door space", Vector) = (0.22, 0.12, 1, 0)
        _SunRadius ("Sun angular radius", Range(0.005, 0.1)) = 0.035
        _Exposure ("Escape exposure", Range(0.1, 4)) = 1.25
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+10" }
        Pass
        {
            Name "Escape"
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull Back ZWrite On ZTest LEqual
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Zenith, _Horizon, _Field, _Sun;
                float4 _SunDirection;
                float _SunRadius, _Exposure;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionOS = input.positionOS.xyz;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 eye = TransformWorldToObject(GetCameraPositionWS());
                // Explicit half-space guard also handles cameras on the plane.
                clip(input.positionOS.z - eye.z - 0.0001);
                float3 ray = normalize(input.positionOS - eye);
                float sky = smoothstep(-0.015, 0.025, ray.y);
                float ridge = 0.012 * sin(ray.x * 19.0) + 0.006 * sin(ray.x * 43.0);
                float fieldEdge = smoothstep(ridge - 0.004, ridge + 0.004, ray.y);
                half3 color = lerp(_Horizon.rgb, _Zenith.rgb, saturate(ray.y * 2.0));
                color = lerp(_Field.rgb * (0.65 + 0.35 * sky), color, fieldEdge);
                float sunDistance = length(ray - normalize(_SunDirection.xyz));
                float sun = 1.0 - smoothstep(_SunRadius * 0.88, _SunRadius, sunDistance);
                color += _Sun.rgb * sun * fieldEdge;
                return half4(color * _Exposure, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
