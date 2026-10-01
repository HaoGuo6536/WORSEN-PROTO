// Inverted hull rendered after fog, before image degradation. Depth-tested against
// opaque geometry: Glimpse reveals silhouettes through fog, never through walls.
Shader "Worsen/GlimpseOutline"
{

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Glimpse Outline"
            Cull Front ZWrite Off ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4 _GlimpseColor;
            float _GlimpseWidth;
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                world += TransformObjectToWorldNormal(input.normalOS) * _GlimpseWidth;
                output.positionCS = TransformWorldToHClip(world);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target { return _GlimpseColor; }
            ENDHLSL
        }
    }
}
