// ============================================================================
// SceneFlowManager.cs
// ============================================================================
//
// PURPOSE:
//   Provides the single scene-loading boundary for the prototype. It resolves a
//   Core scene key, validates that the scene is available in the build, and reports
//   engine loading facts so the run can wait for the subsequent SceneRoot hand-off.
//
// ARCHITECTURAL ROLE:
//   Manager (§1, §8b) · Session · SceneFlow (Session system).
//   A minimal stateless facade; pure mapping is delegated to its Controller.
//
// KEY RESPONSIBILITIES:
//   - Keep one persistent canonical scene-loading service.
//   - Validate load requests and own the only SceneManager.LoadScene call.
//   - Forward recognized scene-load notifications as Core-typed instance events.
//
// DEPENDENCIES:
//   - SceneFlowController and Definitions in this system; Core SceneKey.
//   - Unity SceneManager loading and notifications, expressly owned here (§8b).
//
// USAGE NOTES:
//   Persistent on its own root GameObject, explicitly initialized by the SceneRoot.
//   OnEnable/OnDisable pair the engine subscription. This facade owns no loading
//   sequence, scene references, or runtime flow state; the Run Session owns waiting.
//   SceneLoaded means the engine finished loading, not that scene assembly is ready.
//   FloorLoop is intentionally unavailable until its later milestone builds it.
//
// ============================================================================

using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Worsen.Core;

namespace Worsen.Session.SceneFlow
{
    public sealed class SceneFlowManager : MonoBehaviour
    {
        private readonly SceneFlowController controller = new SceneFlowController();

        public static SceneFlowManager Instance { get; private set; }

        public event Action<SceneKey> SceneLoadStarted;
        public event Action<SceneKey> SceneLoaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
        }

        public SceneFlowManager Initialize()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return Instance;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            return this;
        }

        public void RequestLoad(SceneKey scene)
        {
            if (Instance != this)
                throw new InvalidOperationException("Initialize and use the canonical SceneFlowManager before loading a scene.");

            string path = controller.GetScenePath(scene);
            if (!Application.CanStreamedLevelBeLoaded(path))
                throw new InvalidOperationException("Scene is not available in the build: " + path +
                    ". Build the scene with its Worsen/Scenes setup command and enable it in Build Settings.");

            SceneLoadStarted?.Invoke(scene);
            SceneManager.LoadScene(path, LoadSceneMode.Single);
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (Instance == this && controller.TryGetSceneKey(scene.path, out SceneKey key))
                SceneLoaded?.Invoke(key);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            SceneLoadStarted = null;
            SceneLoaded = null;
        }
    }
}
