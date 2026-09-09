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

        [Header("Movement")]
        [SerializeField, Min(0.1f)] private float patrolSpeed = 2.2f;
        [SerializeField, Min(0.1f)] private float chaseSpeed = 4.2f;
        [SerializeField, Min(1f)] private float rotationSpeed = 360f;
        [SerializeField, Min(0.05f)] private float waypointTolerance = 0.25f;
        [SerializeField, Min(0f)] private float waypointWaitTime = 0.4f;
        [SerializeField] private float gravity = -25f;

        [Header("Detection")]
        [SerializeField, Min(0.5f)] private float detectionDistance = 8f;
        [SerializeField, Range(1f, 179f)] private float viewAngle = 75f;
        [SerializeField, Min(0.1f)] private float losePlayerAfter = 2.5f;
        [SerializeField, Min(0.1f)] private float catchDistance = 1.1f;
        [SerializeField] private LayerMask visionMask = ~0;

        [Header("Sight Cone Visual")]
        [SerializeField] private bool showSightCone = true;
        [SerializeField, Range(6, 64)] private int sightConeSegments = 32;
        [SerializeField, Min(0f)] private float sightConeGroundOffset = 0.04f;
        [SerializeField] private Color patrolSightColor = new Color(1f, 0.35f, 0.05f, 0.22f);
        [SerializeField] private Color chaseSightColor = new Color(1f, 0.05f, 0.02f, 0.32f);

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
        private GameObject sightConeObject;
        private Mesh sightConeMesh;
        private Material sightConeMaterial;
        private Vector3[] sightConeVertices;
        private readonly RaycastHit[] sightConeHits = new RaycastHit[16];

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            initialPosition = transform.position;
            initialRotation = transform.rotation;
            CreateSightConeVisual();
        }

        private void Start()
        {
            ResolvePlayer();
        }

        private void Update()
        {
            if (LDGameSession.Instance != null && LDGameSession.Instance.IsComplete)
            {
                ApplyGravityOnly();
                return;
            }

            ResolvePlayer();
            bool canSeePlayer = CanSeePlayer();

            if (canSeePlayer)
            {
                state = GuardState.Chase;
                lastSeenPosition = player.position;
                timeWithoutSight = 0f;
            }
            else if (state == GuardState.Chase || state == GuardState.Search)
            {
                timeWithoutSight += Time.deltaTime;
                if (state == GuardState.Chase)
                {
                    state = GuardState.Search;
                }
            }

            UpdateSightConeVisual();

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

        private void CreateSightConeVisual()
        {
            sightConeObject = new GameObject("Sight Cone Visual");
            sightConeObject.transform.SetParent(transform, false);
            sightConeObject.transform.localPosition = Vector3.up * sightConeGroundOffset;

            MeshFilter meshFilter = sightConeObject.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = sightConeObject.AddComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.sortingOrder = -10;

            sightConeMesh = new Mesh { name = "Guard Sight Cone" };
            sightConeMesh.MarkDynamic();
            int segmentCount = Mathf.Max(6, sightConeSegments);
            sightConeVertices = new Vector3[segmentCount + 2];
            int[] triangles = new int[segmentCount * 3];

            sightConeVertices[0] = Vector3.zero;
            for (int i = 0; i <= segmentCount; i++)
            {
                float angle = Mathf.Lerp(-viewAngle * 0.5f, viewAngle * 0.5f, i / (float)segmentCount);
                sightConeVertices[i + 1] =
                    Quaternion.Euler(0f, angle, 0f) * Vector3.forward * detectionDistance;

                if (i < segmentCount)
                {
                    int triangleIndex = i * 3;
                    triangles[triangleIndex] = 0;
                    triangles[triangleIndex + 1] = i + 1;
                    triangles[triangleIndex + 2] = i + 2;
                }
            }

            sightConeMesh.vertices = sightConeVertices;
            sightConeMesh.triangles = triangles;
            sightConeMesh.RecalculateBounds();
            meshFilter.sharedMesh = sightConeMesh;

            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                sightConeMaterial = new Material(shader)
                {
                    name = "Guard Sight Cone (Runtime)",
                    hideFlags = HideFlags.HideAndDontSave
                };
                meshRenderer.sharedMaterial = sightConeMaterial;
            }

            UpdateSightConeVisual();
        }

        private void UpdateSightConeVisual()
        {
            if (sightConeObject == null)
            {
                return;
            }

            sightConeObject.SetActive(showSightCone);
            sightConeObject.transform.localPosition = Vector3.up * sightConeGroundOffset;
            if (!showSightCone)
            {
                return;
            }

            if (sightConeMaterial != null)
            {
                sightConeMaterial.color = state == GuardState.Chase ? chaseSightColor : patrolSightColor;
            }

            UpdateSightConeGeometry();
        }

        private void UpdateSightConeGeometry()
        {
            if (sightConeMesh == null || sightConeVertices == null)
            {
                return;
            }

            Vector3 rayOrigin = eye != null ? eye.position : transform.position + Vector3.up * 1.55f;
            int segmentCount = sightConeVertices.Length - 2;

            for (int i = 0; i <= segmentCount; i++)
            {
                float angle = Mathf.Lerp(-viewAngle * 0.5f, viewAngle * 0.5f, i / (float)segmentCount);
                Vector3 localDirection = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                Vector3 worldDirection = transform.TransformDirection(localDirection);
                float visibleDistance = GetVisibleConeDistance(rayOrigin, worldDirection);
                sightConeVertices[i + 1] = localDirection * visibleDistance;
            }

            sightConeMesh.vertices = sightConeVertices;
            sightConeMesh.RecalculateBounds();
        }

        private float GetVisibleConeDistance(Vector3 origin, Vector3 direction)
        {
            int hitCount = Physics.RaycastNonAlloc(
                origin,
                direction,
                sightConeHits,
                detectionDistance,
                visionMask,
                QueryTriggerInteraction.Ignore);

            float nearestDistance = detectionDistance;
            for (int i = 0; i < hitCount; i++)
            {
                Transform hitTransform = sightConeHits[i].transform;
                if (hitTransform == null || hitTransform == transform || hitTransform.IsChildOf(transform))
                {
                    continue;
                }

                if (player != null && (hitTransform == player || hitTransform.IsChildOf(player)))
                {
                    continue;
                }

                nearestDistance = Mathf.Min(nearestDistance, sightConeHits[i].distance);
            }

            return nearestDistance;
        }

        private void OnDestroy()
        {
            if (sightConeMesh != null)
            {
                Destroy(sightConeMesh);
            }

            if (sightConeMaterial != null)
            {
                Destroy(sightConeMaterial);
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
