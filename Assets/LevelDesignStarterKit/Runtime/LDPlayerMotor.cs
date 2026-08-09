using UnityEngine;

namespace LevelDesignStarterKit
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class LDPlayerMotor : MonoBehaviour
    {
        [Header("Health")]
        [SerializeField, Min(1)] private int startingHealth = 1;
        [SerializeField, Min(0f)] private float invulnerableDuration = 3f;

        private int health;
        private bool isInvulnerable;
        private float invulnerableTimer;

        [Header("Movement")]
        [SerializeField, Min(0.1f)] private float walkSpeed = 4f;
        [SerializeField, Min(0.1f)] private float runSpeed = 7f;
        [SerializeField, Min(0.1f)] private float rotationSmoothTime = 0.08f;
        [SerializeField, Min(0.1f)] private float jumpHeight = 2.2f;
        [SerializeField] private float gravity = -25f;
        [SerializeField] private float respawnBelowY = -15f;

        [Header("Reference")]
        [SerializeField] private Transform cameraTransform;

        private CharacterController controller;
        private float verticalVelocity;
        private float rotationVelocity;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
        }

        private void Start()
        {
            if (cameraTransform == null && Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }

            if (LDGameSession.Instance != null)
            {
                LDGameSession.Instance.RegisterPlayer(this);
            }

            health = Mathf.Max(1, startingHealth);
        }

        private void Update()
        {
            if (LDGameSession.Instance != null && LDGameSession.Instance.IsComplete)
            {
                return;
            }

            if (cameraTransform == null && Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }

            bool grounded = controller.isGrounded;
            if (grounded && verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
            }

            Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            input = Vector2.ClampMagnitude(input, 1f);

            Vector3 moveDirection = GetCameraRelativeDirection(input);
            if (moveDirection.sqrMagnitude > 0.001f)
            {
                float targetAngle = Mathf.Atan2(moveDirection.x, moveDirection.z) * Mathf.Rad2Deg;
                float smoothedAngle = Mathf.SmoothDampAngle(
                    transform.eulerAngles.y,
                    targetAngle,
                    ref rotationVelocity,
                    rotationSmoothTime);

                transform.rotation = Quaternion.Euler(0f, smoothedAngle, 0f);
            }

            if (grounded && Input.GetButtonDown("Jump"))
            {
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }

            verticalVelocity += gravity * Time.deltaTime;
            float speed = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)
                ? runSpeed
                : walkSpeed;

            Vector3 velocity = moveDirection * speed;
            velocity.y = verticalVelocity;
            controller.Move(velocity * Time.deltaTime);

            if (transform.position.y < respawnBelowY && LDGameSession.Instance != null)
            {
                LDGameSession.Instance.RespawnPlayer("You fell. Returned to checkpoint.");
            }

            // Update invulnerability timer
            if (isInvulnerable)
            {
                invulnerableTimer -= Time.deltaTime;
                if (invulnerableTimer <= 0f)
                {
                    isInvulnerable = false;
                }
            }
        }

        public void ConfigureCamera(Transform newCameraTransform)
        {
            cameraTransform = newCameraTransform;
        }

        public void ResetMotion()
        {
            verticalVelocity = 0f;
            rotationVelocity = 0f;
        }

        public void AddHealth(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            health += amount;

            if (LDGameSession.Instance != null)
            {
                LDGameSession.Instance.SetStatus($"Health bonus collected! HP: {health}", 2.5f);
            }
        }

        public void ResetHealth()
        {
            health = Mathf.Max(1, startingHealth);
            isInvulnerable = false;
            invulnerableTimer = 0f;
        }

        public void OnCaughtByGuard()
        {
            if (isInvulnerable)
            {
                return;
            }

            health = Mathf.Max(0, health - 1);

            if (LDGameSession.Instance != null)
            {
                if (health <= 0)
                {
                    LDGameSession.Instance.RespawnPlayer("Caught by a guard. Returned to checkpoint.");
                    ResetHealth();
                }
                else
                {
                    LDGameSession.Instance.SetStatus($"You were caught! HP: {health}", 2.5f);
                    isInvulnerable = true;
                    invulnerableTimer = invulnerableDuration;
                }
            }
            else
            {
                // Fallback: if no game session, still apply invulnerability or immediate respawn
                if (health <= 0)
                {
                    // try to find a session to respawn
                    LDGameSession session = FindObjectOfType<LDGameSession>();
                    if (session != null)
                    {
                        session.RespawnPlayer("Caught by a guard. Returned to checkpoint.");
                        ResetHealth();
                    }
                }
                else
                {
                    isInvulnerable = true;
                    invulnerableTimer = invulnerableDuration;
                }
            }
        }

        private Vector3 GetCameraRelativeDirection(Vector2 input)
        {
            if (cameraTransform == null)
            {
                return new Vector3(input.x, 0f, input.y).normalized;
            }

            Vector3 forward = cameraTransform.forward;
            Vector3 right = cameraTransform.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();
            return (forward * input.y + right * input.x).normalized;
        }
    }
}
