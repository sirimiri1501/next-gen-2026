using UnityEngine;

namespace LevelDesignStarterKit
{
    [RequireComponent(typeof(Collider))]
    public sealed class LDCheckpoint : MonoBehaviour
    {
        [SerializeField] private bool activateOnlyOnce = true;
        private bool activated;

        private void Reset()
        {
            Collider trigger = GetComponent<Collider>();
            trigger.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (activateOnlyOnce && activated)
            {
                return;
            }

            LDPlayerMotor player = other.GetComponentInParent<LDPlayerMotor>();
            if (player == null || LDGameSession.Instance == null)
            {
                return;
            }

            activated = true;
            LDGameSession.Instance.SetCheckpoint(transform);
        }
    }
}
