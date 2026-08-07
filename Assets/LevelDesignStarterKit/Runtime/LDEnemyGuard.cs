using UnityEngine;

namespace LevelDesignStarterKit
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(LDGuardDetectionLight))]
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
        [SerializeField] private LDGuardDetectionLight detectionLight;

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

        [Header("Simple Obstacle Steering")]
        [SerializeField, Min(0f)] private float obstacleProbeDistance = 1.1f;
        [SerializeField] private LayerMask obstacleMask = ~(1 << 2);

        private CharacterController controller;
        private GuardState state;
        private int currentWaypointIndex;
        private int currentBezierSegmentIndex;
        private float currentBezierProgress;
        private bool movingForward = true;
        private float waypointWaitTimer;
        private float timeWithoutSight;
        private float verticalVelocity;
        private Vector3 lastSeenPosition;
        private Vector3 initialPosition;
        private Quaternion initialRotation;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            initialPosition = transform.position;
            initialRotation = transform.rotation;
        }

        private void Start()
        {
            ResolvePlayer();
            EnsureDetectionLight();
        }

        private void Update()
        {
            if (LDGameSession.Instance != null && LDGameSession.Instance.IsComplete)
            {
                ApplyGravityOnly();
                return;
            }

            ResolvePlayer();
            if (CanCatchNearbyPlayer())
            {
                CatchPlayer();
                return;
            }

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
            UpdateDetectionLight();
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
            currentBezierSegmentIndex = 0;
            currentBezierProgress = 0f;
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

            if (patrolPath.UseBezierMovement)
            {
                UpdateBezierPatrol();
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

        private void UpdateBezierPatrol()
        {
            int segmentCount = patrolPath.CurveSegmentCount;
            if (segmentCount == 0)
            {
                ApplyGravityOnly();
                return;
            }

            currentBezierSegmentIndex = Mathf.Clamp(currentBezierSegmentIndex, 0, segmentCount - 1);
            if (waypointWaitTimer > 0f)
            {
                ContinueBezierWait();
                return;
            }

            float segmentLength = patrolPath.GetBezierSegmentLength(currentBezierSegmentIndex);
            float progressDirection = movingForward || patrolPath.Loop ? 1f : -1f;
            float nextProgress = currentBezierProgress + progressDirection * patrolSpeed * Time.deltaTime / segmentLength;
            bool reachedSegmentEnd = nextProgress >= 1f || nextProgress <= 0f;
            currentBezierProgress = Mathf.Clamp01(nextProgress);

            Vector3 targetPosition = patrolPath.GetBezierPoint(currentBezierSegmentIndex, currentBezierProgress);
            MoveTowards(targetPosition, patrolSpeed, 0f);

            if (reachedSegmentEnd && IsNearHorizontalPosition(targetPosition, waypointTolerance))
            {
                if (waypointWaitTime > 0f)
                {
                    waypointWaitTimer = Mathf.Max(Time.deltaTime, 0.0001f);
                }
                else
                {
                    AdvanceBezierSegment();
                }
            }
        }

        private void ContinueBezierWait()
        {
            waypointWaitTimer += Time.deltaTime;
            ApplyGravityOnly();

            if (waypointWaitTimer >= waypointWaitTime)
            {
                waypointWaitTimer = 0f;
                AdvanceBezierSegment();
            }
        }

        private void AdvanceBezierSegment()
        {
            int segmentCount = patrolPath.CurveSegmentCount;
            if (segmentCount == 0)
            {
                currentBezierSegmentIndex = 0;
                currentBezierProgress = 0f;
                return;
            }

            if (patrolPath.Loop)
            {
                currentBezierSegmentIndex = (currentBezierSegmentIndex + 1) % segmentCount;
                currentBezierProgress = 0f;
                movingForward = true;
                return;
            }

            if (movingForward)
            {
                if (currentBezierSegmentIndex >= segmentCount - 1)
                {
                    movingForward = false;
                    currentBezierProgress = 1f;
                    return;
                }

                currentBezierSegmentIndex++;
                currentBezierProgress = 0f;
                return;
            }

            if (currentBezierSegmentIndex <= 0)
            {
                movingForward = true;
                currentBezierProgress = 0f;
                return;
            }

            currentBezierSegmentIndex--;
            currentBezierProgress = 1f;
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
                if (patrolPath.UseBezierMovement)
                {
                    patrolPath.GetClosestBezierProgress(transform.position, out currentBezierSegmentIndex, out currentBezierProgress);
                }
                else
                {
                    currentWaypointIndex = patrolPath.GetClosestPointIndex(transform.position);
                }
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

        private bool IsNearHorizontalPosition(Vector3 targetPosition, float distance)
        {
            Vector3 horizontalDelta = targetPosition - transform.position;
            horizontalDelta.y = 0f;
            return horizontalDelta.sqrMagnitude <= distance * distance;
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

        private void EnsureDetectionLight()
        {
            if (detectionLight != null)
            {
                return;
            }

            detectionLight = GetComponent<LDGuardDetectionLight>();
            if (detectionLight == null)
            {
                detectionLight = gameObject.AddComponent<LDGuardDetectionLight>();
            }
        }

        private void UpdateDetectionLight()
        {
            EnsureDetectionLight();

            bool active = LDGameSession.Instance == null || !LDGameSession.Instance.IsComplete;
            detectionLight.SetRange(detectionDistance, viewAngle, state == GuardState.Chase, active, catchDistance);
        }

        private bool CanCatchNearbyPlayer()
        {
            if (player == null)
            {
                return false;
            }

            Vector3 horizontalDelta = player.position - transform.position;
            horizontalDelta.y = 0f;
            return horizontalDelta.sqrMagnitude <= catchDistance * catchDistance;
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
