using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LevelDesignStarterKit
{
    public sealed class LDThirdPersonCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField, Min(1f)] private float distance = 5.5f;
        [SerializeField, Min(0f)] private float pivotHeight = 1.45f;
        [SerializeField, Min(0.1f)] private float mouseSensitivity = 3f;
        [SerializeField] private float minimumPitch = -20f;
        [SerializeField] private float maximumPitch = 65f;
        [SerializeField, Min(0f)] private float positionSmoothTime = 0.05f;
        [SerializeField, Min(0.01f)] private float collisionRadius = 0.2f;
        [SerializeField] private LayerMask collisionMask = ~(1 << 2);
        [SerializeField] private bool lockCursorOnStart = true;

        [Header("Onboarding Objective Tour")]
        [Tooltip("Automatically play Player -> Energy Core -> Exit -> Energy Core -> Player when the level starts.")]
        [SerializeField] private bool playObjectiveTourOnStart = true;
        [Tooltip("Optional. If empty, the first active LDKeyCollectible is used.")]
        [SerializeField] private Transform energyCoreObjective;
        [Tooltip("Optional. If empty, the first active LDExitGoal is used.")]
        [SerializeField] private Transform exitObjective;
        [Tooltip("Optional exact camera position and rotation for the Energy Core shot.")]
        [SerializeField] private Transform energyCoreCameraAnchor;
        [Tooltip("Optional exact camera position and rotation for the Exit shot.")]
        [SerializeField] private Transform exitCameraAnchor;
        [Tooltip("Optional ordered camera pose waypoints between the Player and Energy Core.")]
        [SerializeField] private Transform[] playerToEnergyCoreWaypoints;
        [Tooltip("Optional ordered camera pose waypoints between the Energy Core and Exit.")]
        [SerializeField] private Transform[] energyCoreToExitWaypoints;

        [Header("Onboarding Tour - Automatic Framing")]
        [Tooltip("Used only when a Camera Anchor is not assigned.")]
        [SerializeField, Min(0f)] private float automaticViewDistance = 4.5f;
        [Tooltip("Used only when a Camera Anchor is not assigned. A high default keeps the route above level walls.")]
        [SerializeField, Min(0f)] private float automaticViewHeight = 6.5f;
        [SerializeField] private float automaticViewSideOffset;
        [SerializeField] private Vector3 energyCoreLookOffset = new Vector3(0f, 0.8f, 0f);
        [SerializeField] private Vector3 exitLookOffset = new Vector3(0f, 1.5f, 0f);

        [Header("Onboarding Tour - Timing")]
        [SerializeField, Min(0f)] private float tourStartDelay = 0.35f;
        [SerializeField, Min(0.01f)] private float playerToEnergyCoreTravelTime = 3f;
        [SerializeField, Min(0.01f)] private float energyCoreToExitTravelTime = 2.5f;
        [Tooltip("Total travel time from Exit back through the full reversed route to Player.")]
        [SerializeField, Min(0.01f)] private float returnTravelTime = 4f;
        [SerializeField, Min(0f)] private float energyCoreHoldTime = 1.25f;
        [SerializeField, Min(0f)] private float exitHoldTime = 1.75f;
        [SerializeField] private AnimationCurve tourMovementEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Onboarding Tour - Control")]
        [Tooltip("Pause guard movement and detection while keeping their cone visuals active.")]
        [SerializeField] private bool pauseGuardsDuringTour = true;
        [SerializeField] private bool freezePlayerDuringTour = true;
        [SerializeField] private bool allowTourSkip = true;
        [SerializeField] private KeyCode tourSkipKey = KeyCode.Escape;
        [SerializeField] private bool drawTourPathGizmos = true;

        private float yaw;
        private float pitch = 18f;
        private Vector3 positionVelocity;
        private Coroutine objectiveTourCoroutine;
        private bool objectiveTourPlaying;
        private Pose tourStartPose;
        private Pose energyCorePose;
        private Pose exitPose;
        private readonly List<Pose> objectiveTourRoute = new List<Pose>();
        private int energyCoreRouteIndex;
        private LDPlayerMotor frozenPlayerMotor;
        private bool frozenPlayerWasEnabled;
        private bool playerWasFrozenByTour;
        private LDGameSession pausedGameSession;
        private bool cinematicWasPlayingBeforeTour;

        public bool IsObjectiveTourPlaying => objectiveTourPlaying;

        private void Start()
        {
            ResolveTarget();
            if (target != null)
            {
                yaw = target.eulerAngles.y;
            }

            if (lockCursorOnStart)
            {
                LockCursor(true);
            }

            SnapBehindTarget();

            if (playObjectiveTourOnStart)
            {
                PlayObjectiveTour();
            }
        }

        private void Update()
        {
            if (objectiveTourPlaying)
            {
                if (allowTourSkip && Input.GetKeyDown(tourSkipKey))
                {
                    SkipObjectiveTour();
                }

                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                LockCursor(Cursor.lockState != CursorLockMode.Locked);
            }
            else if (Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked)
            {
                LockCursor(true);
            }
        }

        private void LateUpdate()
        {
            if (objectiveTourPlaying)
            {
                return;
            }

            ResolveTarget();
            if (target == null)
            {
                return;
            }

            if (Cursor.lockState == CursorLockMode.Locked)
            {
                yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
                pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity;
                pitch = Mathf.Clamp(pitch, minimumPitch, maximumPitch);
            }

            Vector3 pivot = target.position + Vector3.up * pivotHeight;
            Quaternion orbitRotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 desiredOffset = orbitRotation * (Vector3.back * distance);
            Vector3 desiredPosition = pivot + desiredOffset;

            Vector3 castDirection = desiredPosition - pivot;
            float castDistance = castDirection.magnitude;
            if (castDistance > 0.001f && Physics.SphereCast(
                    pivot,
                    collisionRadius,
                    castDirection.normalized,
                    out RaycastHit hit,
                    castDistance,
                    collisionMask,
                    QueryTriggerInteraction.Ignore))
            {
                desiredPosition = pivot + castDirection.normalized * Mathf.Max(0.15f, hit.distance - collisionRadius);
            }

            transform.position = Vector3.SmoothDamp(
                transform.position,
                desiredPosition,
                ref positionVelocity,
                positionSmoothTime);
            transform.rotation = Quaternion.LookRotation(pivot - transform.position, Vector3.up);
        }

        private void OnDisable()
        {
            if (!objectiveTourPlaying)
            {
                return;
            }

            if (objectiveTourCoroutine != null)
            {
                StopCoroutine(objectiveTourCoroutine);
                objectiveTourCoroutine = null;
            }

            objectiveTourPlaying = false;
            RestorePlayerAfterTour();
            RestoreGuardsAfterTour();
            positionVelocity = Vector3.zero;
            SnapBehindTarget();
        }

        public void Configure(Transform followTarget)
        {
            target = followTarget;
        }

        public void SnapBehindTarget()
        {
            ResolveTarget();
            if (target == null)
            {
                return;
            }

            yaw = target.eulerAngles.y;
            positionVelocity = Vector3.zero;
            Pose followPose = CreateFollowPose(target);
            transform.SetPositionAndRotation(followPose.position, followPose.rotation);
        }

        private Pose CreateFollowPose(Transform followTarget)
        {
            Vector3 pivot = followTarget.position + Vector3.up * pivotHeight;
            Quaternion orbitRotation = Quaternion.Euler(pitch, followTarget.eulerAngles.y, 0f);
            Vector3 cameraPosition = pivot + orbitRotation * (Vector3.back * distance);
            Quaternion cameraRotation = Quaternion.LookRotation(pivot - cameraPosition, Vector3.up);
            return new Pose(cameraPosition, cameraRotation);
        }

        public void PlayObjectiveTour()
        {
            if (objectiveTourPlaying || !isActiveAndEnabled)
            {
                return;
            }

            ResolveTarget();
            ResolveObjectiveReferences();
            if (target == null || energyCoreObjective == null || exitObjective == null)
            {
                Debug.LogWarning(
                    "Objective camera tour needs a Player target, an LDKeyCollectible, and an LDExitGoal.",
                    this);
                return;
            }

            SnapBehindTarget();
            tourStartPose = new Pose(transform.position, transform.rotation);
            energyCorePose = CreateObjectivePose(
                energyCoreObjective,
                energyCoreCameraAnchor,
                target.position,
                energyCoreLookOffset);
            exitPose = CreateObjectivePose(
                exitObjective,
                exitCameraAnchor,
                energyCoreObjective.position,
                exitLookOffset);
            BuildObjectiveTourRoute();

            PauseGuardsForTour();
            FreezePlayerForTour();
            objectiveTourPlaying = true;
            objectiveTourCoroutine = StartCoroutine(PlayObjectiveTourRoutine());
        }

        public void SkipObjectiveTour()
        {
            if (!objectiveTourPlaying)
            {
                return;
            }

            if (objectiveTourCoroutine != null)
            {
                StopCoroutine(objectiveTourCoroutine);
                objectiveTourCoroutine = null;
            }

            FinishObjectiveTour();
        }

        private IEnumerator PlayObjectiveTourRoutine()
        {
            yield return WaitForTourSeconds(tourStartDelay);
            yield return MoveCameraAlongRoute(0, energyCoreRouteIndex, playerToEnergyCoreTravelTime);
            yield return WaitForTourSeconds(energyCoreHoldTime);
            yield return MoveCameraAlongRoute(
                energyCoreRouteIndex,
                objectiveTourRoute.Count - 1,
                energyCoreToExitTravelTime);
            yield return WaitForTourSeconds(exitHoldTime);

            // Travel through the exact cached poses in reverse so the return route matches the outbound route.
            yield return MoveCameraAlongRoute(objectiveTourRoute.Count - 1, 0, returnTravelTime);

            objectiveTourCoroutine = null;
            FinishObjectiveTour();
        }

        private IEnumerator MoveCameraAlongRoute(int startIndex, int endIndex, float duration)
        {
            if (startIndex == endIndex)
            {
                Pose onlyPose = objectiveTourRoute[endIndex];
                transform.SetPositionAndRotation(onlyPose.position, onlyPose.rotation);
                yield break;
            }

            int step = endIndex > startIndex ? 1 : -1;
            int segmentCount = Mathf.Abs(endIndex - startIndex);
            float[] segmentLengths = new float[segmentCount];
            float totalLength = 0f;

            for (int segment = 0; segment < segmentCount; segment++)
            {
                int fromIndex = startIndex + segment * step;
                int toIndex = fromIndex + step;
                float segmentLength = Vector3.Distance(
                    objectiveTourRoute[fromIndex].position,
                    objectiveTourRoute[toIndex].position);
                segmentLengths[segment] = Mathf.Max(0.001f, segmentLength);
                totalLength += segmentLengths[segment];
            }

            float safeDuration = Mathf.Max(0.01f, duration);
            float elapsed = 0f;

            while (elapsed < safeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float normalizedTime = Mathf.Clamp01(elapsed / safeDuration);
                float easedTime = Mathf.Clamp01(tourMovementEase != null
                    ? tourMovementEase.Evaluate(normalizedTime)
                    : normalizedTime);
                float targetDistance = easedTime * totalLength;
                float distanceBeforeSegment = 0f;
                int activeSegment = 0;

                while (activeSegment < segmentCount - 1
                    && targetDistance > distanceBeforeSegment + segmentLengths[activeSegment])
                {
                    distanceBeforeSegment += segmentLengths[activeSegment];
                    activeSegment++;
                }

                int fromIndex = startIndex + activeSegment * step;
                int toIndex = fromIndex + step;
                float segmentTime = Mathf.Clamp01(
                    (targetDistance - distanceBeforeSegment) / segmentLengths[activeSegment]);
                Pose fromPose = objectiveTourRoute[fromIndex];
                Pose toPose = objectiveTourRoute[toIndex];

                transform.position = Vector3.Lerp(fromPose.position, toPose.position, segmentTime);
                transform.rotation = Quaternion.Slerp(fromPose.rotation, toPose.rotation, segmentTime);
                yield return null;
            }

            Pose destination = objectiveTourRoute[endIndex];
            transform.SetPositionAndRotation(destination.position, destination.rotation);
        }

        private static IEnumerator WaitForTourSeconds(float duration)
        {
            float elapsed = 0f;
            float safeDuration = Mathf.Max(0f, duration);
            while (elapsed < safeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private void FinishObjectiveTour()
        {
            objectiveTourPlaying = false;
            RestorePlayerAfterTour();
            RestoreGuardsAfterTour();
            positionVelocity = Vector3.zero;
            SnapBehindTarget();
        }

        private void FreezePlayerForTour()
        {
            playerWasFrozenByTour = false;
            frozenPlayerMotor = target != null ? target.GetComponent<LDPlayerMotor>() : null;
            if (frozenPlayerMotor == null)
            {
                frozenPlayerMotor = FindObjectOfType<LDPlayerMotor>();
            }

            if (!freezePlayerDuringTour || frozenPlayerMotor == null)
            {
                return;
            }

            frozenPlayerWasEnabled = frozenPlayerMotor.enabled;
            frozenPlayerMotor.ResetMotion();
            frozenPlayerMotor.enabled = false;
            playerWasFrozenByTour = true;
        }

        private void RestorePlayerAfterTour()
        {
            if (playerWasFrozenByTour && frozenPlayerMotor != null)
            {
                frozenPlayerMotor.enabled = frozenPlayerWasEnabled;
            }

            playerWasFrozenByTour = false;
            frozenPlayerMotor = null;
        }

        private void PauseGuardsForTour()
        {
            pausedGameSession = null;
            if (!pauseGuardsDuringTour || LDGameSession.Instance == null)
            {
                return;
            }

            pausedGameSession = LDGameSession.Instance;
            cinematicWasPlayingBeforeTour = pausedGameSession.IsCinematicPlaying;
            pausedGameSession.SetCinematicPlaying(true);
        }

        private void RestoreGuardsAfterTour()
        {
            if (pausedGameSession != null)
            {
                pausedGameSession.SetCinematicPlaying(cinematicWasPlayingBeforeTour);
            }

            pausedGameSession = null;
        }

        private Pose CreateObjectivePose(
            Transform objective,
            Transform cameraAnchor,
            Vector3 approachOrigin,
            Vector3 lookOffset)
        {
            if (cameraAnchor != null)
            {
                return new Pose(cameraAnchor.position, cameraAnchor.rotation);
            }

            Vector3 approachDirection = objective.position - approachOrigin;
            approachDirection.y = 0f;
            if (approachDirection.sqrMagnitude <= 0.0001f)
            {
                approachDirection = objective.forward;
                approachDirection.y = 0f;
            }

            if (approachDirection.sqrMagnitude <= 0.0001f)
            {
                approachDirection = Vector3.forward;
            }

            approachDirection.Normalize();
            Vector3 sideDirection = Vector3.Cross(Vector3.up, approachDirection).normalized;
            Vector3 cameraPosition = objective.position
                - approachDirection * automaticViewDistance
                + Vector3.up * automaticViewHeight
                + sideDirection * automaticViewSideOffset;
            Vector3 lookDirection = objective.TransformPoint(lookOffset) - cameraPosition;

            if (lookDirection.sqrMagnitude <= 0.0001f)
            {
                return new Pose(cameraPosition, transform.rotation);
            }

            Vector3 cameraUp = Mathf.Abs(Vector3.Dot(lookDirection.normalized, Vector3.up)) > 0.98f
                ? Vector3.forward
                : Vector3.up;
            return new Pose(cameraPosition, Quaternion.LookRotation(lookDirection, cameraUp));
        }

        private void BuildObjectiveTourRoute()
        {
            objectiveTourRoute.Clear();
            objectiveTourRoute.Add(tourStartPose);
            AppendWaypointPoses(playerToEnergyCoreWaypoints);
            objectiveTourRoute.Add(energyCorePose);
            energyCoreRouteIndex = objectiveTourRoute.Count - 1;
            AppendWaypointPoses(energyCoreToExitWaypoints);
            objectiveTourRoute.Add(exitPose);
        }

        private void AppendWaypointPoses(Transform[] waypoints)
        {
            if (waypoints == null)
            {
                return;
            }

            foreach (Transform waypoint in waypoints)
            {
                if (waypoint != null)
                {
                    objectiveTourRoute.Add(new Pose(waypoint.position, waypoint.rotation));
                }
            }
        }

        private void ResolveObjectiveReferences()
        {
            if (energyCoreObjective == null)
            {
                LDKeyCollectible[] energyCores = FindObjectsOfType<LDKeyCollectible>();
                if (energyCores.Length > 1)
                {
                    Debug.LogWarning(
                        "Multiple Energy Cores found. Assign Energy Core Objective explicitly on the camera.",
                        this);
                }

                if (energyCores.Length > 0)
                {
                    energyCoreObjective = energyCores[0].transform;
                }
            }

            if (exitObjective == null)
            {
                LDExitGoal[] exits = FindObjectsOfType<LDExitGoal>();
                if (exits.Length > 1)
                {
                    Debug.LogWarning(
                        "Multiple Exits found. Assign Exit Objective explicitly on the camera.",
                        this);
                }

                if (exits.Length > 0)
                {
                    exitObjective = exits[0].transform;
                }
            }
        }

        private void ResolveTarget()
        {
            if (target != null)
            {
                return;
            }

            LDPlayerMotor player = FindObjectOfType<LDPlayerMotor>();
            if (player != null)
            {
                target = player.transform;
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawTourPathGizmos)
            {
                return;
            }

            Transform previewPlayer = target;
            Transform previewEnergyCore = energyCoreObjective;
            Transform previewExit = exitObjective;

            if (previewPlayer == null)
            {
                LDPlayerMotor player = FindObjectOfType<LDPlayerMotor>();
                previewPlayer = player != null ? player.transform : null;
            }

            if (previewEnergyCore == null)
            {
                LDKeyCollectible energyCore = FindObjectOfType<LDKeyCollectible>();
                previewEnergyCore = energyCore != null ? energyCore.transform : null;
            }

            if (previewExit == null)
            {
                LDExitGoal exit = FindObjectOfType<LDExitGoal>();
                previewExit = exit != null ? exit.transform : null;
            }

            if (previewPlayer == null || previewEnergyCore == null || previewExit == null)
            {
                return;
            }

            Pose previewCorePose = CreateObjectivePose(
                previewEnergyCore,
                energyCoreCameraAnchor,
                previewPlayer.position,
                energyCoreLookOffset);
            Pose previewExitPose = CreateObjectivePose(
                previewExit,
                exitCameraAnchor,
                previewEnergyCore.position,
                exitLookOffset);

            Gizmos.color = new Color(0.15f, 0.85f, 1f, 1f);
            Pose previewStartPose = CreateFollowPose(previewPlayer);
            Vector3 previousPosition = previewStartPose.position;
            DrawWaypointPathGizmos(playerToEnergyCoreWaypoints, ref previousPosition);
            Gizmos.DrawLine(previousPosition, previewCorePose.position);
            previousPosition = previewCorePose.position;
            DrawWaypointPathGizmos(energyCoreToExitWaypoints, ref previousPosition);
            Gizmos.DrawLine(previousPosition, previewExitPose.position);
            Gizmos.DrawWireSphere(previewCorePose.position, 0.35f);
            Gizmos.DrawWireSphere(previewExitPose.position, 0.35f);
            Gizmos.DrawRay(previewCorePose.position, previewCorePose.rotation * Vector3.forward * 1.5f);
            Gizmos.DrawRay(previewExitPose.position, previewExitPose.rotation * Vector3.forward * 1.5f);
        }

        private void DrawWaypointPathGizmos(Transform[] waypoints, ref Vector3 previousPosition)
        {
            if (waypoints == null)
            {
                return;
            }

            foreach (Transform waypoint in waypoints)
            {
                if (waypoint == null)
                {
                    continue;
                }

                Gizmos.DrawLine(previousPosition, waypoint.position);
                Gizmos.DrawWireSphere(waypoint.position, 0.22f);
                Gizmos.DrawRay(waypoint.position, waypoint.forward);
                previousPosition = waypoint.position;
            }
        }

        private static void LockCursor(bool shouldLock)
        {
            Cursor.lockState = shouldLock ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !shouldLock;
        }
    }
}
