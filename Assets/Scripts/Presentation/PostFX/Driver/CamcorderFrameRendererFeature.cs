// ============================================================================
// CamcorderFrameRendererFeature.cs
// ============================================================================
// PURPOSE:
//   Adds the text-free old-camcorder frame after the existing URP post effects.
//   A single color-to-color RenderGraph pass swaps the active target, avoiding
//   a copy-back pass and preserving existing grain and chromatic aberration.
// ARCHITECTURAL ROLE:
//   Driver (§7a, engine render-pipeline adapter) · Presentation · PostFX.
// KEY RESPONSIBILITIES:
//   - Own the shader material and one fullscreen pass.
//   - Read camera-local volume values and exclude preview/overlay cameras.
//   - Declare intermediate color dependency and dispose renderer resources.
// DEPENDENCIES:
//   Unity URP/Core rendering APIs and the owned CamcorderFrameVolume adapter.
// USAGE NOTES:
//   Renderer-owned, RenderGraph only. Setup must install this feature and assign
//   Worsen/CamcorderFrame explicitly so the shader survives player stripping.
//   Expected cost: one fullscreen draw, seven color samples/pixel, one transient
//   color target, no depth input or history. GPU milliseconds require measurement.
// ============================================================================
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;
namespace Worsen.Presentation.PostFX
{
    public sealed class CamcorderFrameRendererFeature : ScriptableRendererFeature
    {
        [SerializeField] private Shader _shader = null;
        private Material _material;
        private FramePass _pass;
        public void Initialize(Shader shader) { _shader = shader; Create(); }
        public override void Create()
        {
            CoreUtils.Destroy(_material);
            _material = _shader == null ? null : CoreUtils.CreateEngineMaterial(_shader);
            _pass = _material == null ? null : new FramePass(_material);
        }
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_pass == null || renderingData.cameraData.cameraType != CameraType.Game ||
                renderingData.cameraData.renderType != CameraRenderType.Base || !renderingData.cameraData.postProcessEnabled) return;
            var frame = VolumeManager.instance.stack.GetComponent<CamcorderFrameVolume>();
            if (frame == null || !frame.active || !frame.Enabled.value) return;
            renderer.EnqueuePass(_pass);
        }
        protected override void Dispose(bool disposing)
        { CoreUtils.Destroy(_material); _material = null; _pass = null; }

        private sealed class FramePass : ScriptableRenderPass
        {
            private readonly Material _material;
            public FramePass(Material material)
            {
                _material = material;
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
                requiresIntermediateTexture = true;
            }
            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer) return;
                var frame = VolumeManager.instance.stack.GetComponent<CamcorderFrameVolume>();
                if (frame == null || !frame.Enabled.value) return;
                var properties = new MaterialPropertyBlock();
                properties.SetVector("_CamcorderLens", frame.Lens.value);
                properties.SetVector("_CamcorderTape", frame.Tape.value);
                properties.SetFloat("_CamcorderEdgeStart", frame.EdgeStart.value);
                var source = resources.activeColorTexture;
                var description = graph.GetTextureDesc(source);
                description.name = "Camcorder Frame Color";
                description.clearBuffer = false;
                var destination = graph.CreateTexture(description);
                var parameters = new RenderGraphUtils.BlitMaterialParameters(source, destination, _material, 0)
                { propertyBlock = properties };
                graph.AddBlitPass(parameters, passName: "Worsen Old Camcorder");
                resources.cameraColor = destination;
            }
        }
    }
}
