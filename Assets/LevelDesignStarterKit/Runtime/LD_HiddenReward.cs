using UnityEngine;
using UnityEngine.Events;

namespace LevelDesignStarterKit
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class LD_HiddenReward : MonoBehaviour
    {
        private const string DefaultRewardMessage = "You found a hidden gem!";

        [Header("Hidden Reward")]
        [SerializeField, TextArea(1, 3)] private string rewardMessage = DefaultRewardMessage;
        [SerializeField, Min(0.1f)] private float messageDuration = 3f;
        [Tooltip("Disable this object after the Player collects it.")]
        [SerializeField] private bool hideAfterCollected = true;

        [Header("Events")]
        [SerializeField] private UnityEvent onCollected = new UnityEvent();

        private bool collected;

        public bool IsCollected => collected;

        private void Reset()
        {
            SetColliderAsTrigger();
        }

        private void Awake()
        {
            SetColliderAsTrigger();
        }

        private void OnEnable()
        {
            if (LDGameSession.Instance != null)
            {
                LDGameSession.Instance.RegisterHiddenReward(this);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (collected || other.GetComponentInParent<LDPlayerMotor>() == null)
            {
                return;
            }

            Collect();
        }

        public void Collect()
        {
            if (collected)
            {
                return;
            }

            if (LDGameSession.Instance == null)
            {
                Debug.LogWarning("LD_HiddenReward needs an LDGameSession in the scene to show its UI message.", this);
                return;
            }

            collected = true;
            LDGameSession.Instance.NotifyHiddenRewardCollected(this);

            string message = string.IsNullOrWhiteSpace(rewardMessage)
                ? DefaultRewardMessage
                : rewardMessage;
            LDGameSession.Instance.SetStatus(message, messageDuration);
            onCollected.Invoke();

            if (hideAfterCollected)
            {
                gameObject.SetActive(false);
            }
        }

        public void ResetReward()
        {
            collected = false;
            gameObject.SetActive(true);

            if (LDGameSession.Instance != null)
            {
                LDGameSession.Instance.NotifyHiddenRewardReset(this);
            }
        }

        private void SetColliderAsTrigger()
        {
            Collider rewardCollider = GetComponent<Collider>();
            if (rewardCollider != null)
            {
                rewardCollider.isTrigger = true;
            }
        }
    }
}
