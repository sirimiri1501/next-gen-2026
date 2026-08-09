using UnityEngine;

namespace LevelDesignStarterKit
{
    [RequireComponent(typeof(Collider))]
    public sealed class LDBonusHealth : MonoBehaviour
    {
        [SerializeField, Min(1)] private int healthAmount = 1;

        private bool collected;

        private void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (collected)
            {
                return;
            }

            LDPlayerMotor player = other.GetComponentInParent<LDPlayerMotor>();
            if (player == null)
            {
                return;
            }

            collected = true;
            player.AddHealth(healthAmount);
            gameObject.SetActive(false);
        }
    }
}
