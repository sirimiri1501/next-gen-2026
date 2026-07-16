using UnityEngine;

namespace LevelDesignStarterKit
{
    [RequireComponent(typeof(Collider))]
    public sealed class LDExitGoal : MonoBehaviour
    {
        private void Reset()
        {
            Collider trigger = GetComponent<Collider>();
            trigger.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<LDPlayerMotor>() != null && LDGameSession.Instance != null)
            {
                LDGameSession.Instance.TryCompleteLevel();
            }
        }
    }
}
