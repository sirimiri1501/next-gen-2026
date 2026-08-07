using UnityEngine;

namespace LevelDesignStarterKit
{
    public sealed class LDThirdPersonCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField, Min(0f)] private float pivotHeight = 1.55f;
        [SerializeField, Min(0.1f)] private float mouseSensitivity = 3f;
        [SerializeField] private float minimumPitch = -80f;
        [SerializeField] private float maximumPitch = 80f;
        [SerializeField] private bool rotateTargetWithView = true;
        [SerializeField] private bool hideTargetRenderers = true;
        [SerializeField] private bool lockCursorOnStart = true;
        [SerializeField, Min(0f)] private float overviewOrbitSpeed = 8f;
        [SerializeField, Min(6f)] private float overviewMinimumDistance = 18f;
        [SerializeField, Min(6f)] private float overviewMinimumHeight = 18f;

        private float yaw;
        private float pitch;
        private bool overviewMode;
        private bool hasOverviewBounds;
        private Bounds overviewBounds;
        private LDPreviewCameraPoint[] overviewPoints;
        private int overviewPointIndex;
        private Transform hiddenRendererTarget;
        private Renderer[] hiddenRenderers;

        private void Start()
        {
            ResolveTarget();
            if (overviewMode)
            {
                ApplyOverviewView();
                return;
            }

            if (lockCursorOnStart)
            {
                LockCursor(true);
            }

            SnapBehindTarget();
        }

        private void Update()
        {
            if (overviewMode)
            {
                if (Cursor.lockState != CursorLockMode.None || !Cursor.visible)
                {
                    LockCursor(false);
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
            if (overviewMode)
            {
                ApplyOverviewView();
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

            ApplyFirstPersonView();
        }

        public void Configure(Transform followTarget)
        {
            target = followTarget;
        }

        public void SetOverviewMode(bool enabledState)
        {
            overviewMode = enabledState;

            if (overviewMode)
            {
                RestoreHiddenRenderers();
                CacheOverviewPoints();
                CacheOverviewBounds();
                LockCursor(false);
                ApplyOverviewView();
                return;
            }

            if (lockCursorOnStart)
            {
                LockCursor(true);
            }

            SnapBehindTarget();
        }

        public int OverviewPointCount => overviewPoints != null ? overviewPoints.Length : 0;
        public int CurrentOverviewPointNumber => OverviewPointCount > 0 ? overviewPointIndex + 1 : 0;
        public string CurrentOverviewPointName => OverviewPointCount > 0 ? overviewPoints[overviewPointIndex].DisplayName : "Orbit Overview";

        public void ShowNextOverviewPoint()
        {
            if (OverviewPointCount == 0)
            {
                CacheOverviewPoints();
            }

            if (OverviewPointCount == 0)
            {
                return;
            }

            overviewPointIndex = (overviewPointIndex + 1) % OverviewPointCount;
            ApplyOverviewView();
        }

        public void ShowPreviousOverviewPoint()
        {
            if (OverviewPointCount == 0)
            {
                CacheOverviewPoints();
            }

            if (OverviewPointCount == 0)
            {
                return;
            }

            overviewPointIndex = (overviewPointIndex + OverviewPointCount - 1) % OverviewPointCount;
            ApplyOverviewView();
        }

        public void SnapBehindTarget()
        {
            ResolveTarget();
            if (target == null)
            {
                return;
            }

            yaw = target.eulerAngles.y;
            pitch = 0f;
            ApplyFirstPersonView();
        }

        private void OnDisable()
        {
            RestoreHiddenRenderers();
        }

        private void OnDestroy()
        {
            RestoreHiddenRenderers();
        }

        private void ApplyOverviewView()
        {
            if (OverviewPointCount > 0)
            {
                LDPreviewCameraPoint previewPoint = overviewPoints[overviewPointIndex];
                transform.SetPositionAndRotation(previewPoint.transform.position, previewPoint.transform.rotation);
                return;
            }

            if (!hasOverviewBounds)
            {
                CacheOverviewBounds();
            }

            Vector3 focus = hasOverviewBounds ? overviewBounds.center : Vector3.zero;
            float horizontalExtent = hasOverviewBounds
                ? Mathf.Max(overviewBounds.extents.x, overviewBounds.extents.z)
                : overviewMinimumDistance;
            float distance = Mathf.Max(overviewMinimumDistance, horizontalExtent * 1.45f);
            float height = Mathf.Max(overviewMinimumHeight, horizontalExtent * 1.15f);
            float orbitAngle = Time.unscaledTime * overviewOrbitSpeed;
            Vector3 offset = Quaternion.Euler(0f, orbitAngle, 0f) * (Vector3.back * distance);
            Vector3 position = focus + offset + Vector3.up * height;
            Vector3 lookDirection = focus - position;

            if (lookDirection.sqrMagnitude <= 0.001f)
            {
                lookDirection = Vector3.forward;
            }

            transform.SetPositionAndRotation(position, Quaternion.LookRotation(lookDirection, Vector3.up));
        }

        private void ApplyFirstPersonView()
        {
            Quaternion viewRotation = Quaternion.Euler(pitch, yaw, 0f);
            transform.SetPositionAndRotation(target.position + Vector3.up * pivotHeight, viewRotation);

            if (rotateTargetWithView)
            {
                target.rotation = Quaternion.Euler(0f, yaw, 0f);
            }

            if (hideTargetRenderers)
            {
                HideTargetRenderers();
            }
            else
            {
                RestoreHiddenRenderers();
            }
        }

        private void HideTargetRenderers()
        {
            if (target == null || hiddenRendererTarget == target)
            {
                return;
            }

            RestoreHiddenRenderers();
            hiddenRendererTarget = target;
            hiddenRenderers = target.GetComponentsInChildren<Renderer>();
            foreach (Renderer targetRenderer in hiddenRenderers)
            {
                targetRenderer.enabled = false;
            }
        }

        private void RestoreHiddenRenderers()
        {
            if (hiddenRenderers == null)
            {
                return;
            }

            foreach (Renderer targetRenderer in hiddenRenderers)
            {
                if (targetRenderer != null)
                {
                    targetRenderer.enabled = true;
                }
            }

            hiddenRenderers = null;
            hiddenRendererTarget = null;
        }

        private void CacheOverviewPoints()
        {
            overviewPoints = FindObjectsOfType<LDPreviewCameraPoint>();
            System.Array.Sort(overviewPoints, CompareOverviewPoints);
            overviewPointIndex = Mathf.Clamp(overviewPointIndex, 0, Mathf.Max(0, OverviewPointCount - 1));
        }

        private static int CompareOverviewPoints(LDPreviewCameraPoint left, LDPreviewCameraPoint right)
        {
            int orderComparison = left.Order.CompareTo(right.Order);
            if (orderComparison != 0)
            {
                return orderComparison;
            }

            return string.Compare(left.name, right.name, System.StringComparison.Ordinal);
        }

        private void CacheOverviewBounds()
        {
            Renderer[] sceneRenderers = FindObjectsOfType<Renderer>();
            hasOverviewBounds = false;

            foreach (Renderer sceneRenderer in sceneRenderers)
            {
                if (sceneRenderer == null ||
                    !sceneRenderer.enabled ||
                    !sceneRenderer.gameObject.activeInHierarchy ||
                    sceneRenderer.transform == transform ||
                    sceneRenderer.transform.IsChildOf(transform))
                {
                    continue;
                }

                if (!hasOverviewBounds)
                {
                    overviewBounds = sceneRenderer.bounds;
                    hasOverviewBounds = true;
                    continue;
                }

                overviewBounds.Encapsulate(sceneRenderer.bounds);
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

        private static void LockCursor(bool shouldLock)
        {
            Cursor.lockState = shouldLock ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !shouldLock;
        }
    }
}
