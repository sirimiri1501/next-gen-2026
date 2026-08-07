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

        [Header("Start Preview")]
        [SerializeField] private bool showStartPreview = true;
        [SerializeField] private string previewTitle = "CAMERA VIEWING";
        [SerializeField] private string previewText = "Use Back and Next to inspect preview cameras inside the building, then click Begin to start.";

        private Vector3 respawnPosition;
        private Quaternion respawnRotation;
        private Vector3 initialRespawnPosition;
        private Quaternion initialRespawnRotation;
        private bool hasRespawnPoint;
        private bool hasInitialRespawnPoint;
        private string statusMessage = string.Empty;
        private float statusMessageUntil;

        public bool HasKey { get; private set; }
        public bool HasStarted { get; private set; }
        public bool IsComplete { get; private set; }
        public bool IsGameplayActive => HasStarted && !IsComplete;
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

            if (showStartPreview)
            {
                EnterStartPreview();
            }
            else
            {
                BeginPlay();
            }
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
            initialRespawnPosition = respawnPosition;
            initialRespawnRotation = respawnRotation;
            hasRespawnPoint = true;
            hasInitialRespawnPoint = true;
        }

        public void SetCheckpoint(Transform checkpoint)
        {
            if (checkpoint == null || !IsGameplayActive)
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
            if (HasKey || !IsGameplayActive)
            {
                return;
            }

            HasKey = true;
            SetStatus("Energy Core collected. Reach the Exit!", 3f);
        }

        public bool TryCompleteLevel()
        {
            if (!IsGameplayActive)
            {
                return false;
            }

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

        public void BeginPlay()
        {
            if (IsComplete)
            {
                return;
            }

            ResetGameForBegin();
            HasStarted = true;
            SetPlayerControlEnabled(true);
            SetOverviewCameraEnabled(false);
            SetStatus("Observe the space, avoid the guards, and find the Energy Core.", 4f);
        }

        public void RespawnPlayer(string reason)
        {
            if (player == null || !hasRespawnPoint || !IsGameplayActive)
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

            LDEnemySpinGuard[] spinGuards = FindObjectsOfType<LDEnemySpinGuard>();
            foreach (LDEnemySpinGuard spinGuard in spinGuards)
            {
                spinGuard.ResetGuard();
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

        private void OnGUI()
        {
            if (!HasStarted && showStartPreview)
            {
                DrawStartPreview();
                return;
            }

            if (!showHud)
            {
                return;
            }

            GUI.Box(new Rect(16f, 16f, 420f, 104f), string.Empty);
            GUI.Label(new Rect(30f, 26f, 390f, 24f), "LEVEL DESIGN STARTER KIT");
            GUI.Label(new Rect(30f, 50f, 390f, 24f), objectiveText);
            GUI.Label(new Rect(30f, 75f, 390f, 24f), HasKey ? "ENERGY CORE: FOUND" : "ENERGY CORE: NOT FOUND");
            GUI.Label(new Rect(30f, 96f, 390f, 20f), "WASD Move | Mouse Look | Space Jump | Esc Cursor");

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

        private void EnterStartPreview()
        {
            HasStarted = false;
            SetPlayerControlEnabled(false);
            SetOverviewCameraEnabled(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void ResetGameForBegin()
        {
            HasKey = false;
            IsComplete = false;

            if (hasInitialRespawnPoint)
            {
                respawnPosition = initialRespawnPosition;
                respawnRotation = initialRespawnRotation;
                hasRespawnPoint = true;
            }

            ResetPlayerToRespawnPoint();
            ResetGuards();
            ResetCollectibles();
            ResetCheckpoints();
        }

        private void ResetPlayerToRespawnPoint()
        {
            if (player == null || !hasRespawnPoint)
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
        }

        private static void ResetGuards()
        {
            LDEnemyGuard[] guards = FindObjectsOfType<LDEnemyGuard>();
            foreach (LDEnemyGuard guard in guards)
            {
                guard.ResetGuard();
            }

            LDEnemySpinGuard[] spinGuards = FindObjectsOfType<LDEnemySpinGuard>();
            foreach (LDEnemySpinGuard spinGuard in spinGuards)
            {
                spinGuard.ResetGuard();
            }
        }

        private static void ResetCollectibles()
        {
            LDKeyCollectible[] collectibles = FindObjectsOfType<LDKeyCollectible>(true);
            foreach (LDKeyCollectible collectible in collectibles)
            {
                collectible.ResetCollectible();
            }
        }

        private static void ResetCheckpoints()
        {
            LDCheckpoint[] checkpoints = FindObjectsOfType<LDCheckpoint>(true);
            foreach (LDCheckpoint checkpoint in checkpoints)
            {
                checkpoint.ResetCheckpoint();
            }
        }

        private void SetPlayerControlEnabled(bool enabledState)
        {
            if (player == null)
            {
                return;
            }

            player.enabled = enabledState;
            if (enabledState)
            {
                player.ResetMotion();
            }
        }

        private void SetOverviewCameraEnabled(bool enabledState)
        {
            LDThirdPersonCamera followCamera = FindObjectOfType<LDThirdPersonCamera>();
            if (followCamera != null)
            {
                followCamera.SetOverviewMode(enabledState);
            }
        }

        private void DrawStartPreview()
        {
            float width = Mathf.Min(600f, Screen.width - 40f);
            float height = 230f;
            Rect box = new Rect((Screen.width - width) * 0.5f, Screen.height - height - 36f, width, height);
            GUI.Box(box, string.Empty);

            GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 20
            };
            GUIStyle textStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperCenter,
                wordWrap = true
            };

            GUI.Label(new Rect(box.x + 24f, box.y + 18f, box.width - 48f, 34f), previewTitle, titleStyle);
            GUI.Label(new Rect(box.x + 42f, box.y + 62f, box.width - 84f, 56f), previewText, textStyle);
            GUI.Label(new Rect(box.x + 42f, box.y + 118f, box.width - 84f, 24f), GetPreviewCameraStatus(), textStyle);

            bool canCyclePreviewCameras = HasPreviewCameras();
            GUI.enabled = canCyclePreviewCameras;
            if (GUI.Button(new Rect(box.x + box.width * 0.5f - 190f, box.y + 164f, 110f, 38f), "Back"))
            {
                ShowPreviousPreviewCamera();
            }

            if (GUI.Button(new Rect(box.x + box.width * 0.5f + 80f, box.y + 164f, 110f, 38f), "Next"))
            {
                ShowNextPreviewCamera();
            }

            GUI.enabled = true;

            if (GUI.Button(new Rect(box.x + box.width * 0.5f - 70f, box.y + 164f, 140f, 38f), "Begin"))
            {
                BeginPlay();
            }
        }

        private void ShowNextPreviewCamera()
        {
            LDThirdPersonCamera followCamera = FindPreviewCamera();
            if (followCamera != null)
            {
                followCamera.ShowNextOverviewPoint();
            }
        }

        private void ShowPreviousPreviewCamera()
        {
            LDThirdPersonCamera followCamera = FindPreviewCamera();
            if (followCamera != null)
            {
                followCamera.ShowPreviousOverviewPoint();
            }
        }

        private bool HasPreviewCameras()
        {
            LDThirdPersonCamera followCamera = FindPreviewCamera();
            return followCamera != null && followCamera.OverviewPointCount > 0;
        }

        private string GetPreviewCameraStatus()
        {
            LDThirdPersonCamera followCamera = FindPreviewCamera();
            if (followCamera == null || followCamera.OverviewPointCount == 0)
            {
                return "Viewing: automatic orbit";
            }

            return "Viewing: " + followCamera.CurrentOverviewPointName +
                " (" + followCamera.CurrentOverviewPointNumber + "/" + followCamera.OverviewPointCount + ")";
        }

        private static LDThirdPersonCamera FindPreviewCamera()
        {
            return FindObjectOfType<LDThirdPersonCamera>();
        }
    }
}
