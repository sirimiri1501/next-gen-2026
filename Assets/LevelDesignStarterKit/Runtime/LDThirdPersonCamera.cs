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

        private float yaw;
        private float pitch = 18f;
        private Vector3 positionVelocity;

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
        }

        private void Update()
        {
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
            Vector3 pivot = target.position + Vector3.up * pivotHeight;
            Quaternion orbitRotation = Quaternion.Euler(pitch, yaw, 0f);
            transform.position = pivot + orbitRotation * (Vector3.back * distance);
            transform.LookAt(pivot);
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

        private static void LockCursor(bool shouldLock)
        {
            Cursor.lockState = shouldLock ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !shouldLock;
        }
    }
}
