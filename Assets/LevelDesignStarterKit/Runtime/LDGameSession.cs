using UnityEngine;

namespace LevelDesignStarterKit
{
    /// <summary>
    /// Owns the tiny gameplay loop used by the level-design exercise:
    /// respawn, checkpoint, key collection, exit, and a minimal debug HUD.
    /// </summary>
    public sealed class LDGameSession : MonoBehaviour
    {
        public static LDGameSession Instance { get; private set; }

        [Header("Scene References")]
        [SerializeField] private LDPlayerMotor player;
        [SerializeField] private Transform initialSpawnPoint;

        [Header("Exercise")]
        [SerializeField] private string objectiveText = "Find the Energy Core, then reach the Exit.";
        [SerializeField] private bool showHud = true;

        private Vector3 respawnPosition;
        private Quaternion respawnRotation;
        private bool hasRespawnPoint;
        private string statusMessage = string.Empty;
        private float statusMessageUntil;

        public bool HasKey { get; private set; }
        public bool IsComplete { get; private set; }
        public bool IsCinematicPlaying { get; private set; }
        public LDPlayerMotor Player => player;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("Only one LDGameSession should exist in a scene.", this);
                enabled = false;
                return;
            }

            Instance = this;
        }

        private void Start()
        {
            if (player == null)
            {
                player = FindObjectOfType<LDPlayerMotor>();
            }

            if (player != null)
            {
                RegisterPlayer(player);
            }

            SetStatus("Observe the space, avoid the guards, and find the Energy Core.", 4f);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void Configure(LDPlayerMotor playerMotor, Transform spawnPoint)
        {
            player = playerMotor;
            initialSpawnPoint = spawnPoint;
        }

        public void RegisterPlayer(LDPlayerMotor playerMotor)
        {
            player = playerMotor;

            if (hasRespawnPoint)
            {
                return;
            }

            Transform source = initialSpawnPoint != null ? initialSpawnPoint : player.transform;
            respawnPosition = source.position;
            respawnRotation = source.rotation;
            hasRespawnPoint = true;
        }

        public void SetCheckpoint(Transform checkpoint)
        {
            if (checkpoint == null)
            {
                return;
            }

            respawnPosition = checkpoint.position;
            respawnRotation = checkpoint.rotation;
            hasRespawnPoint = true;
            SetStatus("Checkpoint activated", 2f);
        }

        public void CollectKey()
        {
            if (HasKey)
            {
                return;
            }

            HasKey = true;
            SetStatus("Energy Core collected. Reach the Exit!", 3f);
        }

        public bool TryCompleteLevel()
        {
            if (!HasKey)
            {
                SetStatus("The Exit is locked. Find the Energy Core first.", 3f);
                return false;
            }

            if (IsComplete)
            {
                return true;
            }

            IsComplete = true;
            SetStatus("LEVEL COMPLETE", 30f);

            if (player != null)
            {
                player.enabled = false;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return true;
        }

        public void RespawnPlayer(string reason)
        {
            if (player == null || !hasRespawnPoint || IsComplete)
            {
                return;
            }

            CharacterController controller = player.GetComponent<CharacterController>();
            if (controller != null)
            {
                controller.enabled = false;
            }

            player.transform.SetPositionAndRotation(respawnPosition, respawnRotation);
            player.ResetMotion();

            if (controller != null)
            {
                controller.enabled = true;
            }

            LDEnemyGuard[] guards = FindObjectsOfType<LDEnemyGuard>();
            foreach (LDEnemyGuard guard in guards)
            {
                guard.ResetGuard();
            }

            LDThirdPersonCamera followCamera = FindObjectOfType<LDThirdPersonCamera>();
            if (followCamera != null)
            {
                followCamera.SnapBehindTarget();
            }

            SetStatus(string.IsNullOrWhiteSpace(reason) ? "Returned to checkpoint" : reason, 2.5f);
        }

        public void SetStatus(string message, float duration)
        {
            statusMessage = message;
            statusMessageUntil = Time.unscaledTime + Mathf.Max(0.1f, duration);
        }

        public void SetCinematicPlaying(bool isPlaying)
        {
            IsCinematicPlaying = isPlaying;
        }

        private void OnGUI()
        {
            if (!showHud)
            {
                return;
            }

            GUI.Box(new Rect(16f, 16f, 420f, 104f), string.Empty);
            GUI.Label(new Rect(30f, 26f, 390f, 24f), "LEVEL DESIGN STARTER KIT");
            GUI.Label(new Rect(30f, 50f, 390f, 24f), objectiveText);
            GUI.Label(new Rect(30f, 75f, 390f, 24f), HasKey ? "ENERGY CORE: FOUND" : "ENERGY CORE: NOT FOUND");
            GUI.Label(new Rect(30f, 96f, 390f, 20f), "WASD Move | Shift Run | Space Jump | Esc Cursor");

            if (Time.unscaledTime < statusMessageUntil && !string.IsNullOrEmpty(statusMessage))
            {
                float width = Mathf.Min(620f, Screen.width - 40f);
                GUI.Box(new Rect((Screen.width - width) * 0.5f, 24f, width, 42f), statusMessage);
            }

            if (IsComplete)
            {
                float width = Mathf.Min(520f, Screen.width - 40f);
                float height = 130f;
                Rect box = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
                GUI.Box(box, string.Empty);
                GUI.Label(new Rect(box.x + 30f, box.y + 24f, box.width - 60f, 32f), "LEVEL COMPLETE");
                GUI.Label(new Rect(box.x + 30f, box.y + 62f, box.width - 60f, 44f), "Review the route, enemy placement, sightlines, pacing, and optional path.");
            }
        }
    }
}
