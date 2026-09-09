using Cinemachine;
using UnityEngine;

namespace LevelDesignStarterKit
{
    /// <summary>
    /// Configures a Cinemachine FreeLook camera while keeping the starter kit's
    /// simple cursor and respawn API on the Main Camera.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class LDThirdPersonCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField, Min(1f)] private float distance = 5.5f;
        [SerializeField, Min(0f)] private float pivotHeight = 1.45f;
        [SerializeField, Min(0.1f)] private float mouseSensitivity = 3f;
        [SerializeField] private float minimumPitch = -20f;
        [SerializeField] private float maximumPitch = 65f;
        [SerializeField, Min(0.01f)] private float collisionRadius = 0.2f;
        [SerializeField] private LayerMask collisionMask = ~(1 << 2);
        [SerializeField] private bool lockCursorOnStart = true;

        private const float InitialPitch = 18f;
        private const string VirtualCameraName = "LD_ThirdPersonFreeLook";

        private CinemachineFreeLook freeLook;
        private Transform cameraTarget;

        private void Start()
        {
            ResolveTarget();
            EnsureCinemachineCamera();

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

            if (target == null)
            {
                ResolveTarget();
                EnsureCinemachineCamera();
            }
        }

        public void Configure(Transform followTarget)
        {
            target = followTarget;
        }

        public void SnapBehindTarget()
        {
            ResolveTarget();
            EnsureCinemachineCamera();
            if (target == null || freeLook == null)
            {
                return;
            }

            freeLook.m_XAxis.Value = target.eulerAngles.y;
            freeLook.m_YAxis.Value = Mathf.InverseLerp(minimumPitch, maximumPitch, InitialPitch);
            freeLook.PreviousStateIsValid = false;
        }

        private void EnsureCinemachineCamera()
        {
            if (target == null)
            {
                return;
            }

            CinemachineBrain brain = GetComponent<CinemachineBrain>();
            if (brain == null)
            {
                brain = gameObject.AddComponent<CinemachineBrain>();
            }

            // A cut is preferable here: this is the only gameplay camera and it also
            // prevents an unwanted blend from the Main Camera's scene transform.
            brain.m_DefaultBlend = new CinemachineBlendDefinition(
                CinemachineBlendDefinition.Style.Cut,
                0f);

            EnsureCameraTarget();
            if (freeLook == null)
            {
                GameObject virtualCameraObject = new GameObject(VirtualCameraName);
                virtualCameraObject.transform.SetParent(transform.parent, false);
                virtualCameraObject.AddComponent<CursorLockedAxisProvider>();
                freeLook = virtualCameraObject.AddComponent<CinemachineFreeLook>();
            }

            ConfigureFreeLook();
        }

        private void EnsureCameraTarget()
        {
            if (cameraTarget != null && cameraTarget.parent == target)
            {
                cameraTarget.localPosition = Vector3.up * pivotHeight;
                return;
            }

            Transform existingTarget = target.Find("LD_CameraTarget");
            if (existingTarget != null)
            {
                cameraTarget = existingTarget;
            }
            else
            {
                GameObject targetObject = new GameObject("LD_CameraTarget");
                cameraTarget = targetObject.transform;
                cameraTarget.SetParent(target, false);
            }

            cameraTarget.localPosition = Vector3.up * pivotHeight;
            cameraTarget.localRotation = Quaternion.identity;
        }

        private void ConfigureFreeLook()
        {
            float pitchRange = Mathf.Max(0.01f, maximumPitch - minimumPitch);
            freeLook.Follow = cameraTarget;
            freeLook.LookAt = cameraTarget;
            freeLook.m_Lens = LensSettings.FromCamera(GetComponent<Camera>());
            freeLook.m_BindingMode = CinemachineTransposer.BindingMode.WorldSpace;
            freeLook.m_Heading = new CinemachineOrbitalTransposer.Heading(
                CinemachineOrbitalTransposer.Heading.HeadingDefinition.WorldForward,
                0,
                0f);
            freeLook.m_RecenterToTargetHeading = new AxisState.Recentering(false, 0f, 0f);
            freeLook.m_YAxisRecentering = new AxisState.Recentering(false, 0f, 0f);

            freeLook.m_XAxis.m_SpeedMode = AxisState.SpeedMode.InputValueGain;
            freeLook.m_XAxis.m_MaxSpeed = mouseSensitivity;
            freeLook.m_XAxis.m_AccelTime = 0f;
            freeLook.m_XAxis.m_DecelTime = 0f;
            freeLook.m_XAxis.m_InputAxisName = string.Empty;
            freeLook.m_XAxis.m_InvertInput = false;

            freeLook.m_YAxis.m_SpeedMode = AxisState.SpeedMode.InputValueGain;
            freeLook.m_YAxis.m_MaxSpeed = mouseSensitivity / pitchRange;
            freeLook.m_YAxis.m_AccelTime = 0f;
            freeLook.m_YAxis.m_DecelTime = 0f;
            freeLook.m_YAxis.m_InputAxisName = string.Empty;
            freeLook.m_YAxis.m_InvertInput = true;

            freeLook.m_Orbits = new[]
            {
                OrbitForPitch(maximumPitch),
                OrbitForPitch(InitialPitch),
                OrbitForPitch(minimumPitch)
            };

            // Zero positional damping keeps A/D movement from producing camera yaw sway.
            for (int i = 0; i < 3; i++)
            {
                CinemachineVirtualCamera rig = freeLook.GetRig(i);
                CinemachineOrbitalTransposer orbital =
                    rig != null ? rig.GetCinemachineComponent<CinemachineOrbitalTransposer>() : null;
                if (orbital != null)
                {
                    orbital.m_XDamping = 0f;
                    orbital.m_YDamping = 0f;
                    orbital.m_ZDamping = 0f;
                }

                CinemachineComposer composer =
                    rig != null ? rig.GetCinemachineComponent<CinemachineComposer>() : null;
                if (composer != null)
                {
                    composer.m_HorizontalDamping = 0f;
                    composer.m_VerticalDamping = 0f;
                }
            }

            CinemachineCollider cameraCollider = freeLook.GetComponent<CinemachineCollider>();
            if (cameraCollider == null)
            {
                cameraCollider = freeLook.gameObject.AddComponent<CinemachineCollider>();
            }

            cameraCollider.m_CollideAgainst = collisionMask;
            cameraCollider.m_IgnoreTag = target.CompareTag("Player") ? "Player" : string.Empty;
            cameraCollider.m_CameraRadius = collisionRadius;
            cameraCollider.m_MinimumDistanceFromTarget = Mathf.Max(0.15f, collisionRadius);
            cameraCollider.m_Strategy = CinemachineCollider.ResolutionStrategy.PullCameraForward;
            cameraCollider.m_SmoothingTime = 0f;
            cameraCollider.m_DampingWhenOccluded = 0f;
            cameraCollider.m_Damping = 0.25f;

            freeLook.UpdateInputAxisProvider();
        }

        private CinemachineFreeLook.Orbit OrbitForPitch(float pitch)
        {
            float radians = pitch * Mathf.Deg2Rad;
            return new CinemachineFreeLook.Orbit(
                Mathf.Sin(radians) * distance,
                Mathf.Cos(radians) * distance);
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

    /// <summary>Feeds FreeLook only while gameplay owns the mouse cursor.</summary>
    internal sealed class CursorLockedAxisProvider : MonoBehaviour, AxisState.IInputAxisProvider
    {
        public float GetAxisValue(int axis)
        {
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                return 0f;
            }

            return axis == 0 ? Input.GetAxis("Mouse X") : Input.GetAxis("Mouse Y");
        }
    }
}
