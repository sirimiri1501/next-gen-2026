using UnityEngine;

namespace LevelDesignStarterKit
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(LDGuardDetectionLight))]
    public sealed class LDEnemySpinGuard : MonoBehaviour
    {
        private enum GuardState
        {
            Spin,
            Chase,
            Search,
            Return
        }

        [Header("References")]
        [SerializeField] private Transform player;
        [SerializeField] private Transform eye;
        [SerializeField] private LDGuardDetectionLight detectionLight;

        [Header("Idle Spin")]
        [SerializeField, Min(1f)] private float spinSpeed = 90f;
        [SerializeField] private bool clockwise = true;

        [Header("Movement")]
        [SerializeField, Min(0.1f)] private float chaseSpeed = 4.2f;
        [SerializeField, Min(0.1f)] private float returnSpeed = 2.2f;
        [SerializeField, Min(1f)] private float rotationSpeed = 360f;
        [SerializeField, Min(0.05f)] private float returnTolerance = 0.25f;
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
                        ReturnToPost();
                    }

                    break;

                case GuardState.Return:
                    UpdateReturnToPost();
                    break;

                default:
                    SpinInPlace();
                    break;
            }
        }

        private void LateUpdate()
        {
            UpdateDetectionLight();
        }

        public void Configure(Transform playerTransform)
        {
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

            state = GuardState.Spin;
            timeWithoutSight = 0f;
            verticalVelocity = 0f;
        }

        private void SpinInPlace()
        {
            float direction = clockwise ? 1f : -1f;
            transform.Rotate(Vector3.up, spinSpeed * direction * Time.deltaTime, Space.World);
            ApplyGravityOnly();
        }

        private void ReturnToPost()
        {
            state = GuardState.Return;
            timeWithoutSight = 0f;
        }

        private void UpdateReturnToPost()
        {
            Vector3 horizontalDelta = initialPosition - transform.position;
            horizontalDelta.y = 0f;

            if (horizontalDelta.magnitude <= returnTolerance)
            {
                ApplyGravityOnly();
                state = GuardState.Spin;
                return;
            }

            MoveTowards(initialPosition, returnSpeed, returnTolerance);
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
                LDGameSession.Instance.RespawnPlayer("Caught by a spinning guard. Returned to checkpoint.");
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
            detectionLight.SetRange(detectionDistance, viewAngle, state == GuardState.Chase, active);
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
            Color color = state == GuardState.Chase ? Color.red : new Color(1f, 0.7f, 0.05f, 1f);
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
            Vector3 postPosition = Application.isPlaying ? initialPosition : transform.position;
            Gizmos.DrawWireSphere(postPosition + Vector3.up * 0.08f, returnTolerance);
        }
    }
}
