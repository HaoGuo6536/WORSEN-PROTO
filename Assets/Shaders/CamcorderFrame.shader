// Text-free old-camcorder lens; one pass composed after existing post-processing.
// Seven bilinear scene-color taps. All visual tuning and tape phase are supplied
// by PostFXDriverConfig / CamcorderFramePresenter; no engine clock or text atlas.
Shader "Worsen/CamcorderFrame"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Old Camcorder"
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            // URP Core first: Blit.hlsl uses TEXTURE2D_X, which it does not define itself.
            // Without it the editor shows the error only on the asset; player builds fail.
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            float4 _CamcorderLens; // corner strength, radius, softness, blur pixels
            float4 _CamcorderTape; // jitter pixels, chroma pixels, phase, line count
            float _CamcorderEdgeStart;

            half4 Scene(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, saturate(uv));
            }
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float2 p = abs(uv * 2.0 - 1.0);
                float radius = max(0.01, _CamcorderLens.y);
                float2 q = p - (1.0 - radius);
                float cornerDistance = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - radius;
                float corner = smoothstep(-max(0.01, _CamcorderLens.z), 0.0, cornerDistance);
                float edge = smoothstep(_CamcorderEdgeStart, 1.0, max(p.x, p.y));
                // `line` is a reserved HLSL word (geometry primitive); `distance` is an intrinsic.
                float scanline = floor(uv.y * _CamcorderTape.w);
                float jitter = sin(scanline + _CamcorderTape.z) * _CamcorderTape.x;
                float2 pixel = _BlitTexture_TexelSize.xy;
                float2 sampleUV = uv + float2(jitter * pixel.x, 0.0);
                float2 blur = pixel * (_CamcorderLens.w * edge);
                half4 center = Scene(sampleUV);
                half3 color = (center.rgb * 4.0 + Scene(sampleUV + float2(blur.x, 0)).rgb
                    + Scene(sampleUV - float2(blur.x, 0)).rgb + Scene(sampleUV + float2(0, blur.y)).rgb
                    + Scene(sampleUV - float2(0, blur.y)).rgb) * 0.125;
                float2 chroma = float2(_CamcorderTape.y * pixel.x, 0.0);
                // Add faint horizontal channel bleed; do not erase URP's existing aberration.
                color.r += Scene(sampleUV + chroma).r - center.r;
                color.b += Scene(sampleUV - chroma).b - center.b;
                return half4(max(0.0, color) * (1.0 - corner * _CamcorderLens.x), center.a);
            }
            ENDHLSL
        }
    }
}
