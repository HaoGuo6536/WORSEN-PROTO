// ============================================================================
// FogRendererFeature.cs
// ============================================================================
// PURPOSE:
//   Integrates the fog field into URP's RenderGraph without replacing distance fog.
//   A depth-limited fullscreen raymarch absorbs the existing scene color in place.
// ARCHITECTURAL ROLE:
//   Driver (§7a, engine render-pipeline adapter) · Presentation · Fog.
// KEY RESPONSIBILITIES:
//   - Declare depth/color dependencies and own the renderer material and GPU marker.
// DEPENDENCIES:
//   - Unity URP/Core rendering APIs and the globals owned by FogDriver only.
// USAGE NOTES:
//   Renderer-owned ScriptableRendererFeature, not a scene MonoBehaviour. This is
//   the required URP adapter to the Driver stack. RenderGraph only; base Game
//   cameras only, before post-processing. Dispose releases the material. Fixed
//   function blending avoids a scene-color copy and preserves destination alpha.
// ============================================================================
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Worsen.Presentation.Fog
{
    public sealed class FogRendererFeature : ScriptableRendererFeature
    {
        [SerializeField] private Shader _shader;
        private Material _material;
        private FogPass _pass;
        public static readonly ProfilingSampler Sampler = new ProfilingSampler("Worsen Fog Raymarch");
        public static int LastRenderFrame { get; private set; } = -1;
        public static Vector2Int LastRenderSize { get; private set; }
        public void Initialize(Shader shader) { _shader = shader; Create(); }
        public override void Create()
        {
            CoreUtils.Destroy(_material);
            _material = _shader == null ? null : CoreUtils.CreateEngineMaterial(_shader);
            _pass = _material == null ? null : new FogPass(_material);
        }
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_pass == null || renderingData.cameraData.cameraType != CameraType.Game ||
                renderingData.cameraData.renderType != CameraRenderType.Base || Shader.GetGlobalFloat("_WorsenFogEnabled") < .5f) return;
            renderer.EnqueuePass(_pass);
        }
        protected override void Dispose(bool disposing)
        { CoreUtils.Destroy(_material); _material = null; _pass = null; Sampler.enableRecording = false; }

        private sealed class FogPass : ScriptableRenderPass
        {
            private readonly Material _material;
            private sealed class PassData { public Material Material; }
            public FogPass(Material material)
            {
                _material = material;
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
                ConfigureInput(ScriptableRenderPassInput.Depth);
            }
            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                var camera = frameData.Get<UniversalCameraData>();
                if (!resources.cameraDepthTexture.IsValid()) return;
                using (var builder = graph.AddRasterRenderPass<PassData>("Worsen Fog Raymarch", out var data, Sampler))
                {
                    data.Material = _material;
                    builder.UseTexture(resources.cameraDepthTexture, AccessFlags.Read);
                    builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);
                    builder.SetRenderFunc((PassData pass, RasterGraphContext context) =>
                        context.cmd.DrawProcedural(Matrix4x4.identity, pass.Material, 0, MeshTopology.Triangles, 3, 1));
                }
                LastRenderFrame = Time.frameCount;
                LastRenderSize = new Vector2Int(camera.cameraTargetDescriptor.width, camera.cameraTargetDescriptor.height);
            }
        }
    }
}
