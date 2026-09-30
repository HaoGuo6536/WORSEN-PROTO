// ============================================================================
// FogDriver.cs
// ============================================================================
// PURPOSE:
//   Owns the bounded density texture and publishes the field to the fog shader.
//   Progress commands are coalesced until LateUpdate so a frame uploads at most once.
// ARCHITECTURAL ROLE:
//   Driver (§7a) · Presentation · Fog.
// KEY RESPONSIBILITIES:
//   - Preserve the pending look during room construction and apply only optical colours.
//   - Apply pure density changes, upload R8 data and own shader globals.
//   - Release texture/global ownership on disable and record actual upload CPU time.
// DEPENDENCIES:
//   - Core room/graph data, own presenter/state/config and Unity rendering APIs.
// USAGE NOTES:
//   Scene-owned, commanded only by FogManager. Owns globals prefixed _WorsenFog.
//   Exactly one enabled fog service is supported. Texture3D.SetPixelData/Apply
//   uploads the whole capped R8 buffer only when dirty; CPU evaluation is regional.
//   Upload timing excludes density evaluation and includes SetPixelData plus Apply.
// ============================================================================
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Worsen.Core;
using Debug = UnityEngine.Debug;

namespace Worsen.Presentation.Fog
{
    public sealed class FogDriver : MonoBehaviour
    {
        private FogDriverConfig _config;
        private FogDriverState _state = new FogDriverState();
        public int RoomCount => _state.Rooms.Count;
        public int UploadRevision => _state.UploadRevision;
        public double LastUploadMilliseconds => _state.LastUploadMilliseconds;
        public float Progress(int id) => _state.Rooms.TryGetValue(id, out var room) ? room.Progress : 0f;

        public void Initialize(FogDriverConfig config)
        {
            _config = config != null ? config : Resources.Load<FogDriverConfig>("ScriptableObjects/Presentation/Fog/FogDriverConfig");
            if (_config == null) Debug.LogWarning("Fog needs FogDriverConfig; run Worsen/Fog/1 - Install Renderer Feature.", this);
        }
        public void SetRooms(IReadOnlyList<GeneratedRoomSample> rooms, LevelGraph graph)
        {
            bool enabledBefore = _state.Enabled;
            string look = _state.Look;
            ResetFloor();
            if (_config == null) return;
            _state = FogDensityPresenter.Build(rooms, graph, _config);
            _state.Enabled = enabledBefore;
            _state.Look = look;
            if (_state.UnmatchedEdges > 0) Debug.LogWarning($"Fog omitted {_state.UnmatchedEdges} graph edges without matching shared-wall portal centers.", this);
        }
        public void SetLook(string look)
        { _state.Look = look; _state.UploadPending = true; }
        public void SetRoomProgress(int id, float progress)
        { if (_config != null) FogDensityPresenter.SetProgress(_state, id, progress, _config); }
        public void SetEnabled(bool value)
        {
            _state.Enabled = value;
            if (!value) ReleaseTexture();
            else _state.UploadPending = true;
        }
        public void ResetFloor()
        {
            ReleaseTexture();
            _state = new FogDriverState { Enabled = _state.Enabled };
        }
        public void Flush()
        {
            if (_config == null || !_state.Enabled || !isActiveAndEnabled || _state.Density.Length == 0) return;
            FogDensityPresenter.Rebuild(_state, _config);
            if (!_state.UploadPending) return;
            if (_state.Texture == null)
            {
                if (!SystemInfo.supports3DTextures || !SystemInfo.SupportsTextureFormat(TextureFormat.R8))
                { Debug.LogError("Fog requires 3D R8 texture support.", this); _state.Enabled = false; return; }
                _state.Texture = new Texture3D(_state.Size.x, _state.Size.y, _state.Size.z, TextureFormat.R8, false)
                { name = "Worsen Fog Density", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
            }
            long started = Stopwatch.GetTimestamp();
            _state.Texture.SetPixelData(_state.Density, 0);
            _state.Texture.Apply(false, false);
            _state.LastUploadMilliseconds = (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;
            _state.UploadRevision++;
            _state.UploadPending = false;
            Vector3 size = _state.Bounds.size;
            Shader.SetGlobalTexture("_WorsenFogDensity", _state.Texture);
            Shader.SetGlobalMatrix("_WorsenFogWorldToGrid", Matrix4x4.Scale(new Vector3(1f / size.x, 1f / size.y, 1f / size.z)) * Matrix4x4.Translate(-_state.Bounds.min));
            Shader.SetGlobalVector("_WorsenFogMin", _state.Bounds.min);
            Shader.SetGlobalVector("_WorsenFogMax", _state.Bounds.max);
            Shader.SetGlobalColor("_WorsenFogBody", FogLookPresenter.Body(_state.Look, _config));
            Shader.SetGlobalColor("_WorsenFogThin", FogLookPresenter.Thin(_state.Look, _config));
            Shader.SetGlobalVector("_WorsenFogOptics", new Vector4(_config.Extinction, _config.Intensity, _config.GlowIntensity, _config.ThinThreshold));
            Shader.SetGlobalVector("_WorsenFogMarch", new Vector4(_config.MaxSteps, _config.EarlyExit, 0f, 0f));
            Shader.SetGlobalFloat("_WorsenFogEnabled", 1f);
        }
        private void ReleaseTexture()
        {
            if (_state.Texture == null) return;
            if (Shader.GetGlobalTexture("_WorsenFogDensity") == _state.Texture)
            {
                Shader.SetGlobalFloat("_WorsenFogEnabled", 0f);
                Shader.SetGlobalTexture("_WorsenFogDensity", null);
                Shader.SetGlobalMatrix("_WorsenFogWorldToGrid", Matrix4x4.identity);
                Shader.SetGlobalVector("_WorsenFogMin", Vector4.zero);
                Shader.SetGlobalVector("_WorsenFogMax", Vector4.zero);
                Shader.SetGlobalVector("_WorsenFogOptics", Vector4.zero);
                Shader.SetGlobalVector("_WorsenFogMarch", Vector4.zero);
                Shader.SetGlobalColor("_WorsenFogBody", Color.clear);
                Shader.SetGlobalColor("_WorsenFogThin", Color.clear);
            }
            if (Application.isPlaying) Destroy(_state.Texture); else DestroyImmediate(_state.Texture);
            _state.Texture = null;
        }
        private void OnDisable() => ResetFloor();
        private void OnDestroy() => ResetFloor();
    }
}
