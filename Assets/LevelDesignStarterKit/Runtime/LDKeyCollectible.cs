using UnityEngine;

namespace LevelDesignStarterKit
{
    [RequireComponent(typeof(Collider))]
    public sealed class LDKeyCollectible : MonoBehaviour
    {
        private bool collected;

        private void Reset()
        {
            Collider trigger = GetComponent<Collider>();
            trigger.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (collected || other.GetComponentInParent<LDPlayerMotor>() == null || LDGameSession.Instance == null)
            {
                return;
            }

            collected = true;
            LDGameSession.Instance.CollectKey();
            gameObject.SetActive(false);
        }

        public void ResetCollectible()
        {
            collected = false;
            gameObject.SetActive(true);
        }
    }
}
