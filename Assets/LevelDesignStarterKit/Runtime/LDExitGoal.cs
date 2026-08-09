using UnityEngine;
using UnityEngine.SceneManagement;

namespace LevelDesignStarterKit
{
    [RequireComponent(typeof(Collider))]
    public sealed class LDExitGoal : MonoBehaviour
    {
        [Header("Scene Transition")]
        [SerializeField, Tooltip("Scene name or asset path to load. Leave empty to load the next scene in Build Settings.")]
        private string destinationScene = string.Empty;

        private bool isLoadingScene;

        private void Reset()
        {
            Collider trigger = GetComponent<Collider>();
            trigger.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (isLoadingScene || other.GetComponentInParent<LDPlayerMotor>() == null)
            {
                return;
            }

            LDGameSession session = LDGameSession.Instance;
            if (session == null)
            {
                Debug.LogWarning("LD_Exit requires an LDGameSession to change scenes.", this);
                return;
            }

            // Preserve the locked-exit feedback before resolving the destination.
            if (!session.HasKey)
            {
                session.TryCompleteLevel();
                return;
            }

            if (!TryResolveDestination(out string sceneName, out int buildIndex, out bool hasDestination))
            {
                return;
            }

            if (!session.TryCompleteLevel() || !hasDestination)
            {
                return;
            }

            isLoadingScene = true;
            if (buildIndex >= 0)
            {
                SceneManager.LoadSceneAsync(buildIndex, LoadSceneMode.Single);
            }
            else
            {
                SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            }
        }

        private bool TryResolveDestination(out string sceneName, out int buildIndex, out bool hasDestination)
        {
            sceneName = string.IsNullOrWhiteSpace(destinationScene) ? string.Empty : destinationScene.Trim();
            buildIndex = -1;
            hasDestination = false;

            if (!string.IsNullOrEmpty(sceneName))
            {
                if (!Application.CanStreamedLevelBeLoaded(sceneName))
                {
                    Debug.LogWarning(
                        $"LD_Exit cannot load scene '{sceneName}'. Add it to Build Settings or correct the destination.",
                        this);
                    return false;
                }

                hasDestination = true;
                return true;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.buildIndex < 0)
            {
                Debug.LogWarning(
                    "LD_Exit has no destination and the active scene is not in Build Settings.",
                    this);
                return false;
            }

            int nextBuildIndex = activeScene.buildIndex + 1;
            if (nextBuildIndex >= SceneManager.sceneCountInBuildSettings)
            {
                // The final scene keeps the existing LEVEL COMPLETE state.
                return true;
            }

            buildIndex = nextBuildIndex;
            hasDestination = true;
            return true;
        }
    }
}
