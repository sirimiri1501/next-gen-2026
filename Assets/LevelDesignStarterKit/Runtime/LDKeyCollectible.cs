using UnityEngine;

namespace LevelDesignStarterKit
{
    [RequireComponent(typeof(Collider))]
    public sealed class LDKeyCollectible : MonoBehaviour
    {
        private bool collected;

        public bool IsCollected => collected;

        private void Reset()
        {
            Collider trigger = GetComponent<Collider>();
            trigger.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (collected || other.GetComponentInParent<LDPlayerMotor>() == null)
            {
                return;
            }

            LDGameSession session = LDGameSession.Instance;
            if (session == null || !session.TryCollectKey(this))
            {
                return;
            }

            collected = true;
            gameObject.SetActive(false);
        }

        public void ResetCollectible()
        {
            if (!collected)
            {
                return;
            }

            collected = false;
            gameObject.SetActive(true);
        }
    }
}
