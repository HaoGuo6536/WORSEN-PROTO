// ============================================================================
// GlimpseRendererFeature.cs
// ============================================================================
// PURPOSE:
//   Draws a brief cold outline on hunter meshes during the Glimpse look-back snap.
//   Rendering after fog preserves the reveal while depth testing retains wall occlusion.
// ARCHITECTURAL ROLE:
//   Driver (§7a, engine render-pipeline adapter) · Presentation · PostFX.
// KEY RESPONSIBILITIES:
//   - Filter hunter renderers explicitly and consume camera-local Glimpse values.
//   - Declare color/depth/list dependencies and release the owned material.
// DEPENDENCIES:
//   Unity URP RenderGraph and the PostFX-owned GlimpseVolume only.
// USAGE NOTES:
//   Renderer-owned, Game base cameras only. Setup must assign the shader and a
//   hunter-only layer mask; an empty mask deliberately draws nothing. No scene scan.
//   Install after fog features; event 551 is after fog (550), before post effects (600).
//   This is an inverted-hull outline, not an x-ray or a gameplay visibility fact.
// ============================================================================
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RendererUtils;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
namespace Worsen.Presentation.PostFX
{
    public sealed class GlimpseRendererFeature : ScriptableRendererFeature
    {
        [SerializeField] private Shader _shader = null;
        [SerializeField] private LayerMask _hunterLayers = 0;
        private Material _material;
        private GlimpsePass _pass;
        public void Initialize(Shader shader, LayerMask hunterLayers)
        { _shader = shader; _hunterLayers = hunterLayers; Create(); }
        public override void Create()
        {
            CoreUtils.Destroy(_material);
            _material = _shader == null ? null : CoreUtils.CreateEngineMaterial(_shader);
            _pass = _material == null ? null : new GlimpsePass(_material, _hunterLayers);
        }
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_pass == null || _hunterLayers.value == 0 || renderingData.cameraData.cameraType != CameraType.Game
                || renderingData.cameraData.renderType != CameraRenderType.Base || !renderingData.cameraData.postProcessEnabled) return;
            var volume = VolumeManager.instance.stack.GetComponent<GlimpseVolume>();
            if (volume != null && volume.active && volume.Strength.value > 0f) renderer.EnqueuePass(_pass);
        }
        protected override void Dispose(bool disposing)
        { CoreUtils.Destroy(_material); _material = null; _pass = null; }
        private sealed class GlimpsePass : ScriptableRenderPass
        {
            private readonly Material material;
            private readonly int layers;
            private static readonly ShaderTagId[] Tags = { new ShaderTagId("UniversalForward"),
                new ShaderTagId("UniversalForwardOnly"), new ShaderTagId("SRPDefaultUnlit") };
            private sealed class PassData { public RendererListHandle List; public Color Tint; public float Width; }
            public GlimpsePass(Material material, int layers)
            { this.material = material; this.layers = layers; renderPassEvent = (RenderPassEvent)((int)RenderPassEvent.BeforeRenderingPostProcessing + 1); }
            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                var volume = VolumeManager.instance.stack.GetComponent<GlimpseVolume>();
                if (volume == null || !volume.active || volume.Strength.value <= 0f) return;
                var resources = frameData.Get<UniversalResourceData>();
                if (!resources.activeDepthTexture.IsValid()) return;
                var camera = frameData.Get<UniversalCameraData>();
                var rendering = frameData.Get<UniversalRenderingData>();
                var desc = new RendererListDesc(Tags, rendering.cullResults, camera.camera)
                { layerMask = layers, renderQueueRange = RenderQueueRange.opaque, sortingCriteria = SortingCriteria.CommonOpaque,
                    overrideMaterial = material, overrideMaterialPassIndex = 0 };
                using (var builder = graph.AddRasterRenderPass<PassData>("Worsen Glimpse", out var data))
                {
                    data.List = graph.CreateRendererList(desc);
                    data.Tint = volume.Tint.value; data.Tint.a *= volume.Strength.value;
                    data.Width = volume.Width.value;
                    builder.UseRendererList(data.List);
                    builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);
                    builder.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.Read);
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc((PassData pass, RasterGraphContext context) =>
                    {
                        context.cmd.SetGlobalColor("_GlimpseColor", pass.Tint);
                        context.cmd.SetGlobalFloat("_GlimpseWidth", pass.Width);
                        context.cmd.DrawRendererList(pass.List);
                        context.cmd.SetGlobalColor("_GlimpseColor", Color.clear);
                        context.cmd.SetGlobalFloat("_GlimpseWidth", 0f);
                    });
                }
            }
        }
    }
}
