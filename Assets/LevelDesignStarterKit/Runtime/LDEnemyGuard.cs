using UnityEngine;
using UnityEngine.Rendering;

namespace LevelDesignStarterKit
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class LDEnemyGuard : MonoBehaviour
    {
        private enum GuardState
        {
            Patrol,
            Chase,
            Search
        }

        [Header("References")]
        [SerializeField] private Transform player;
        [SerializeField] private LDWaypointPath patrolPath;
        [SerializeField] private Transform eye;

        [Header("Guard Movement Mode")]
        [InspectorName("Stand Still While Idle")]
        [Tooltip("ON: the guard stands and scans while idle. After losing the player, it searches normally and then resumes stationary scanning.")]
        [SerializeField] private bool standStillUntilPlayerDetected;
        [SerializeField, Min(0.1f)] private float patrolSpeed = 2.2f;
        [SerializeField, Min(0.1f)] private float chaseSpeed = 4.2f;
        [SerializeField, Min(1f)] private float rotationSpeed = 360f;
        [SerializeField, Min(0.05f)] private float waypointTolerance = 0.25f;
        [SerializeField, Min(0f)] private float waypointWaitTime = 0.4f;
        [SerializeField] private float gravity = -25f;

        [Header("Stationary Scan (Stand Still Enabled)")]
        [Tooltip("Total scan arc, centered on the guard's facing direction when stationary mode begins.")]
        [SerializeField, Range(0f, 180f)] private float stationaryScanAngle = 90f;
        [Tooltip("Rotation speed while scanning, in degrees per second.")]
        [SerializeField, Min(1f)] private float stationaryScanSpeed = 55f;
        [Tooltip("How long the guard waits at both ends of the scan arc.")]
        [SerializeField, Min(0f)] private float stationaryEndpointWaitTime = 1f;

        [Header("Detection")]
        [SerializeField, Min(0.5f)] private float detectionDistance = 8f;
        [SerializeField, Range(1f, 179f)] private float viewAngle = 75f;
        [SerializeField, Min(0.1f)] private float losePlayerAfter = 2.5f;
        [SerializeField, Min(0.1f)] private float catchDistance = 1.1f;
        [SerializeField] private LayerMask visionMask = ~0;

        [Header("Vision Cone Visual")]
        [Tooltip("Show the filled vision cone in the Game view.")]
        [SerializeField] private bool showVisionCone = true;
        [Tooltip("More rays make obstacle edges smoother but cost more physics queries.")]
        [SerializeField, Range(2, 100)] private int visionConeRayCount = 36;
        [Tooltip("Vertical offset above the guard's floor level to avoid z-fighting.")]
        [SerializeField, Min(0f)] private float visionConeHeight = 0.04f;
        [Tooltip("Shorten the cone rays when they hit a collider in Vision Mask.")]
        [SerializeField] private bool clipVisionConeToObstacles = true;
        [SerializeField] private Color visionConeColor = new Color(1f, 0.78f, 0.48f, 0.28f);
        [SerializeField] private Color alertVisionConeColor = new Color(1f, 0.16f, 0.08f, 0.4f);
        [Tooltip("Optional transparent material. Leave empty to use the generated unlit material.")]
        [SerializeField] private Material visionConeMaterial;

        [Header("Simple Obstacle Steering")]
        [SerializeField, Min(0f)] private float obstacleProbeDistance = 1.1f;
        [SerializeField] private LayerMask obstacleMask = ~(1 << 2);

        private CharacterController controller;
        private GuardState state;
        private int currentWaypointIndex;
        private bool movingForward = true;
        private float waypointWaitTimer;
        private float timeWithoutSight;
        private float verticalVelocity;
        private Vector3 lastSeenPosition;
        private Vector3 initialPosition;
        private Quaternion initialRotation;
        private Quaternion stationaryCenterRotation;
        private int stationaryScanDirection = 1;
        private float stationaryEndpointWaitTimer;
        private bool previousStandStillMode;

        private readonly RaycastHit[] visionConeHits = new RaycastHit[32];
        private GameObject visionConeObject;
        private Mesh visionConeMesh;
        private Material runtimeVisionConeMaterial;
        private Vector3[] visionConeVertices;
        private Vector3[] visionConeNormals;
        private int[] visionConeTriangles;
        private int builtVisionConeRayCount = -1;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            initialPosition = transform.position;
            initialRotation = transform.rotation;
            stationaryCenterRotation = GetYawRotation(initialRotation);
            previousStandStillMode = standStillUntilPlayerDetected;
            EnsureVisionConeVisual();
        }

        private void Start()
        {
            ResolvePlayer();
        }

        private void Update()
        {
            RefreshMovementMode();

            if (LDGameSession.Instance != null && LDGameSession.Instance.IsCinematicPlaying)
            {
                ApplyGravityOnly();
                return;
            }

            if (LDGameSession.Instance != null && LDGameSession.Instance.IsComplete)
            {
                ApplyGravityOnly();
                return;
            }

            ResolvePlayer();
            bool canSeePlayer = CanSeePlayer();
            UpdateStandardDetectionState(canSeePlayer);

            if (player != null && state == GuardState.Chase)
            {
                Vector3 horizontalDelta = player.position - transform.position;
                horizontalDelta.y = 0f;
                if (horizontalDelta.magnitude <= catchDistance)
                {
                    CatchPlayer();
                    return;
                }
            }

            if (standStillUntilPlayerDetected && state == GuardState.Patrol)
            {
                UpdateStationaryGuard();
                return;
            }

            switch (state)
            {
                case GuardState.Chase:
                    MoveTowards(player != null ? player.position : lastSeenPosition, chaseSpeed, 0f);
                    break;

                case GuardState.Search:
                    MoveTowards(lastSeenPosition, chaseSpeed * 0.8f, 0.1f);
                    if (timeWithoutSight >= losePlayerAfter)
                    {
                        ReturnToPatrol();
                    }
                    break;

                default:
                    UpdatePatrol();
                    break;
            }
        }

        private void LateUpdate()
        {
            UpdateVisionConeVisual();
        }

        private void OnEnable()
        {
            EnsureVisionConeVisual();
            if (visionConeObject != null)
            {
                visionConeObject.SetActive(showVisionCone);
            }
        }

        private void OnDisable()
        {
            if (visionConeObject != null)
            {
                visionConeObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (visionConeObject != null)
            {
                Destroy(visionConeObject);
            }

            if (visionConeMesh != null)
            {
                Destroy(visionConeMesh);
            }

            if (runtimeVisionConeMaterial != null)
            {
                Destroy(runtimeVisionConeMaterial);
            }
        }

        public void Configure(LDWaypointPath path, Transform playerTransform)
        {
            patrolPath = path;
            player = playerTransform;
        }

        public void ResetGuard()
        {
            if (controller == null)
            {
                controller = GetComponent<CharacterController>();
            }

            controller.enabled = false;
            transform.SetPositionAndRotation(initialPosition, initialRotation);
            controller.enabled = true;

            state = GuardState.Patrol;
            currentWaypointIndex = 0;
            movingForward = true;
            waypointWaitTimer = 0f;
            timeWithoutSight = 0f;
            verticalVelocity = 0f;
            stationaryCenterRotation = GetYawRotation(initialRotation);
            stationaryScanDirection = 1;
            stationaryEndpointWaitTimer = 0f;
            previousStandStillMode = standStillUntilPlayerDetected;
        }

        private void RefreshMovementMode()
        {
            if (standStillUntilPlayerDetected == previousStandStillMode)
            {
                return;
            }

            previousStandStillMode = standStillUntilPlayerDetected;
            stationaryEndpointWaitTimer = 0f;

            if (standStillUntilPlayerDetected)
            {
                stationaryCenterRotation = GetYawRotation(transform.rotation);
                stationaryScanDirection = 1;

                if (state != GuardState.Chase && state != GuardState.Search)
                {
                    state = GuardState.Patrol;
                    timeWithoutSight = 0f;
                }
            }
        }

        private void UpdateStandardDetectionState(bool canSeePlayer)
        {
            if (canSeePlayer)
            {
                state = GuardState.Chase;
                lastSeenPosition = player.position;
                timeWithoutSight = 0f;
                return;
            }

            if (state != GuardState.Chase && state != GuardState.Search)
            {
                return;
            }

            timeWithoutSight += Time.deltaTime;
            if (state == GuardState.Chase)
            {
                state = GuardState.Search;
            }
        }

        private void UpdateStationaryGuard()
        {
            UpdateStationaryScan();
            ApplyGravityOnly();
        }

        private void UpdateStationaryScan()
        {
            float halfAngle = stationaryScanAngle * 0.5f;
            if (halfAngle <= 0.01f)
            {
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    stationaryCenterRotation,
                    stationaryScanSpeed * Time.deltaTime);
                return;
            }

            if (stationaryEndpointWaitTimer > 0f)
            {
                stationaryEndpointWaitTimer -= Time.deltaTime;
                return;
            }

            Quaternion targetRotation = stationaryCenterRotation
                * Quaternion.Euler(0f, halfAngle * stationaryScanDirection, 0f);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                stationaryScanSpeed * Time.deltaTime);

            if (Quaternion.Angle(transform.rotation, targetRotation) <= 0.05f)
            {
                transform.rotation = targetRotation;
                stationaryScanDirection *= -1;
                stationaryEndpointWaitTimer = stationaryEndpointWaitTime;
            }
        }

        private static Quaternion GetYawRotation(Quaternion rotation)
        {
            return Quaternion.Euler(0f, rotation.eulerAngles.y, 0f);
        }

        private void UpdatePatrol()
        {
            if (patrolPath == null || patrolPath.Count == 0)
            {
                ApplyGravityOnly();
                return;
            }

            Transform waypoint = patrolPath.GetPoint(currentWaypointIndex);
            Vector3 horizontalDelta = waypoint.position - transform.position;
            horizontalDelta.y = 0f;

            if (horizontalDelta.magnitude <= waypointTolerance)
            {
                waypointWaitTimer += Time.deltaTime;
                ApplyGravityOnly();

                if (waypointWaitTimer >= waypointWaitTime)
                {
                    waypointWaitTimer = 0f;
                    AdvanceWaypoint();
                }

                return;
            }

            MoveTowards(waypoint.position, patrolSpeed, waypointTolerance);
        }

        private void AdvanceWaypoint()
        {
            if (patrolPath.Count <= 1)
            {
                currentWaypointIndex = 0;
                return;
            }

            if (patrolPath.Loop)
            {
                currentWaypointIndex = (currentWaypointIndex + 1) % patrolPath.Count;
                return;
            }

            if (movingForward)
            {
                currentWaypointIndex++;
                if (currentWaypointIndex >= patrolPath.Count - 1)
                {
                    currentWaypointIndex = patrolPath.Count - 1;
                    movingForward = false;
                }
            }
            else
            {
                currentWaypointIndex--;
                if (currentWaypointIndex <= 0)
                {
                    currentWaypointIndex = 0;
                    movingForward = true;
                }
            }
        }

        private void ReturnToPatrol()
        {
            state = GuardState.Patrol;
            timeWithoutSight = 0f;
            waypointWaitTimer = 0f;

            if (standStillUntilPlayerDetected)
            {
                stationaryCenterRotation = GetYawRotation(transform.rotation);
                stationaryScanDirection = 1;
                stationaryEndpointWaitTimer = 0f;
            }

            if (patrolPath != null && patrolPath.Count > 0)
            {
                currentWaypointIndex = patrolPath.GetClosestPointIndex(transform.position);
            }
        }

        private void MoveTowards(Vector3 targetPosition, float speed, float stoppingDistance)
        {
            Vector3 delta = targetPosition - transform.position;
            delta.y = 0f;

            if (delta.magnitude <= stoppingDistance)
            {
                ApplyGravityOnly();
                return;
            }

            Vector3 desiredDirection = delta.normalized;
            Vector3 steeredDirection = GetSteeredDirection(desiredDirection);
            Quaternion targetRotation = Quaternion.LookRotation(steeredDirection, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);

            UpdateVerticalVelocity();
            Vector3 velocity = steeredDirection * speed;
            velocity.y = verticalVelocity;
            controller.Move(velocity * Time.deltaTime);
        }

        private Vector3 GetSteeredDirection(Vector3 desiredDirection)
        {
            if (obstacleProbeDistance <= 0f)
            {
                return desiredDirection;
            }

            Vector3 origin = transform.position + Vector3.up * 0.75f + desiredDirection * (controller.radius + 0.06f);
            if (!TryGetBlockingHit(origin, desiredDirection, obstacleProbeDistance))
            {
                return desiredDirection;
            }

            Vector3 left = Quaternion.Euler(0f, -55f, 0f) * desiredDirection;
            Vector3 right = Quaternion.Euler(0f, 55f, 0f) * desiredDirection;
            bool leftBlocked = TryGetBlockingHit(origin, left, obstacleProbeDistance);
            bool rightBlocked = TryGetBlockingHit(origin, right, obstacleProbeDistance);

            if (!leftBlocked)
            {
                return left;
            }

            if (!rightBlocked)
            {
                return right;
            }

            return Quaternion.Euler(0f, 90f, 0f) * desiredDirection;
        }

        private bool TryGetBlockingHit(Vector3 origin, Vector3 direction, float distance)
        {
            if (!Physics.Raycast(origin, direction, out RaycastHit hit, distance, obstacleMask, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            if (hit.transform == transform || hit.transform.IsChildOf(transform))
            {
                return false;
            }

            if (player != null && (hit.transform == player || hit.transform.IsChildOf(player)))
            {
                return false;
            }

            return true;
        }

        private bool CanSeePlayer()
        {
            if (player == null)
            {
                return false;
            }

            Vector3 eyePosition = eye != null ? eye.position : transform.position + Vector3.up * 1.55f;
            Vector3 targetPosition = player.position + Vector3.up * 1f;
            Vector3 toPlayer = targetPosition - eyePosition;
            float distance = toPlayer.magnitude;

            if (distance > detectionDistance || distance <= 0.001f)
            {
                return false;
            }

            Vector3 direction = toPlayer / distance;
            if (Vector3.Angle(transform.forward, direction) > viewAngle * 0.5f)
            {
                return false;
            }

            if (!Physics.Raycast(eyePosition, direction, out RaycastHit hit, distance + 0.1f, visionMask, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            return hit.transform == player || hit.transform.IsChildOf(player);
        }

        private void CatchPlayer()
        {
            if (LDGameSession.Instance != null)
            {
                LDGameSession.Instance.RespawnPlayer("Caught by a guard. Returned to checkpoint.");
            }
        }

        private void ResolvePlayer()
        {
            if (player != null)
            {
                return;
            }

            if (LDGameSession.Instance != null && LDGameSession.Instance.Player != null)
            {
                player = LDGameSession.Instance.Player.transform;
                return;
            }

            LDPlayerMotor foundPlayer = FindObjectOfType<LDPlayerMotor>();
            if (foundPlayer != null)
            {
                player = foundPlayer.transform;
            }
        }

        private void ApplyGravityOnly()
        {
            UpdateVerticalVelocity();
            controller.Move(Vector3.up * (verticalVelocity * Time.deltaTime));
        }

        private void UpdateVerticalVelocity()
        {
            if (controller.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
            }
            else
            {
                verticalVelocity += gravity * Time.deltaTime;
            }
        }

        private void EnsureVisionConeVisual()
        {
            if (!Application.isPlaying || visionConeObject != null)
            {
                return;
            }

            visionConeObject = new GameObject("VisionConeVisual");
            visionConeObject.layer = gameObject.layer;
            visionConeObject.transform.SetParent(transform, false);

            MeshFilter meshFilter = visionConeObject.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = visionConeObject.AddComponent<MeshRenderer>();

            visionConeMesh = new Mesh { name = name + "_VisionConeMesh" };
            visionConeMesh.MarkDynamic();
            meshFilter.sharedMesh = visionConeMesh;

            runtimeVisionConeMaterial = CreateVisionConeMaterial();
            meshRenderer.sharedMaterial = runtimeVisionConeMaterial;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            meshRenderer.enabled = runtimeVisionConeMaterial != null;

            visionConeObject.SetActive(showVisionCone);
        }

        private Material CreateVisionConeMaterial()
        {
            if (visionConeMaterial != null)
            {
                Material materialCopy = new Material(visionConeMaterial)
                {
                    name = visionConeMaterial.name + " (Guard Runtime Copy)"
                };
                SetVisionConeMaterialColor(materialCopy, visionConeColor);
                return materialCopy;
            }

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Transparent");
            }

            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (shader == null)
            {
                Debug.LogWarning("No transparent shader was found for the guard vision cone.", this);
                return null;
            }

            Material material = new Material(shader)
            {
                name = name + "_VisionConeMaterial",
                renderQueue = (int)RenderQueue.Transparent
            };

            if (shader.name == "Standard")
            {
                material.SetFloat("_Mode", 3f);
                material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
                material.DisableKeyword("_ALPHATEST_ON");
                material.EnableKeyword("_ALPHABLEND_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            }

            SetVisionConeMaterialColor(material, visionConeColor);
            return material;
        }

        private void UpdateVisionConeVisual()
        {
            EnsureVisionConeVisual();
            if (visionConeObject == null)
            {
                return;
            }

            if (!showVisionCone || runtimeVisionConeMaterial == null)
            {
                visionConeObject.SetActive(false);
                return;
            }

            if (!visionConeObject.activeSelf)
            {
                visionConeObject.SetActive(true);
            }

            visionConeObject.transform.localPosition = new Vector3(0f, visionConeHeight, 0f);
            visionConeObject.transform.localRotation = Quaternion.identity;
            visionConeObject.transform.localScale = Vector3.one;

            EnsureVisionConeMeshBuffers();

            Vector3 eyePosition = eye != null ? eye.position : transform.position + Vector3.up * 1.55f;
            Vector3 visualOriginWorld = new Vector3(eyePosition.x, transform.position.y, eyePosition.z);
            Vector3 localOrigin = transform.InverseTransformPoint(visualOriginWorld);
            localOrigin.y = 0f;
            visionConeVertices[0] = localOrigin;

            for (int i = 0; i <= builtVisionConeRayCount; i++)
            {
                float t = i / (float)builtVisionConeRayCount;
                float angle = Mathf.Lerp(-viewAngle * 0.5f, viewAngle * 0.5f, t);
                Vector3 worldDirection = Quaternion.Euler(0f, angle, 0f) * transform.forward;
                worldDirection.y = 0f;
                worldDirection.Normalize();

                float rayDistance = GetVisionConeRayDistance(eyePosition, worldDirection);
                Vector3 endpointWorld = visualOriginWorld + worldDirection * rayDistance;
                Vector3 endpointLocal = transform.InverseTransformPoint(endpointWorld);
                endpointLocal.y = 0f;
                visionConeVertices[i + 1] = endpointLocal;
            }

            visionConeMesh.vertices = visionConeVertices;
            visionConeMesh.RecalculateBounds();

            Color currentColor = state == GuardState.Chase ? alertVisionConeColor : visionConeColor;
            SetVisionConeMaterialColor(runtimeVisionConeMaterial, currentColor);
        }

        private void EnsureVisionConeMeshBuffers()
        {
            int rayCount = Mathf.Clamp(visionConeRayCount, 2, 100);
            if (builtVisionConeRayCount == rayCount && visionConeVertices != null)
            {
                return;
            }

            builtVisionConeRayCount = rayCount;
            visionConeVertices = new Vector3[rayCount + 2];
            visionConeNormals = new Vector3[visionConeVertices.Length];
            visionConeTriangles = new int[rayCount * 3];

            for (int i = 0; i < visionConeNormals.Length; i++)
            {
                visionConeNormals[i] = Vector3.up;
            }

            for (int i = 0; i < rayCount; i++)
            {
                int triangleIndex = i * 3;
                visionConeTriangles[triangleIndex] = 0;
                visionConeTriangles[triangleIndex + 1] = i + 1;
                visionConeTriangles[triangleIndex + 2] = i + 2;
            }

            visionConeMesh.Clear();
            visionConeMesh.vertices = visionConeVertices;
            visionConeMesh.normals = visionConeNormals;
            visionConeMesh.triangles = visionConeTriangles;
        }

        private float GetVisionConeRayDistance(Vector3 origin, Vector3 direction)
        {
            if (!clipVisionConeToObstacles)
            {
                return detectionDistance;
            }

            int hitCount = Physics.RaycastNonAlloc(
                origin,
                direction,
                visionConeHits,
                detectionDistance,
                visionMask,
                QueryTriggerInteraction.Ignore);

            float closestDistance = detectionDistance;
            for (int i = 0; i < hitCount; i++)
            {
                Transform hitTransform = visionConeHits[i].transform;
                if (hitTransform == null
                    || hitTransform == transform
                    || hitTransform.IsChildOf(transform)
                    || (player != null && (hitTransform == player || hitTransform.IsChildOf(player))))
                {
                    continue;
                }

                closestDistance = Mathf.Min(closestDistance, visionConeHits[i].distance);
            }

            return closestDistance;
        }

        private static void SetVisionConeMaterialColor(Material material, Color color)
        {
            if (material == null)
            {
                return;
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
        }

        private void OnDrawGizmos()
        {
            Vector3 origin = eye != null ? eye.position : transform.position + Vector3.up * 1.55f;
            Color color = state == GuardState.Chase ? Color.red : new Color(1f, 0.35f, 0.2f, 1f);
            Gizmos.color = color;

            const int segmentCount = 20;
            Vector3 previous = Quaternion.Euler(0f, -viewAngle * 0.5f, 0f) * transform.forward * detectionDistance;
            Gizmos.DrawLine(origin, origin + previous);

            for (int i = 1; i <= segmentCount; i++)
            {
                float angle = Mathf.Lerp(-viewAngle * 0.5f, viewAngle * 0.5f, i / (float)segmentCount);
                Vector3 next = Quaternion.Euler(0f, angle, 0f) * transform.forward * detectionDistance;
                Gizmos.DrawLine(origin + previous, origin + next);
                previous = next;
            }

            Gizmos.DrawLine(origin, origin + previous);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.5f, catchDistance);
        }
    }
}
