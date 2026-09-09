using UnityEngine;

namespace LevelDesignStarterKit
{
    public sealed class LDRotator : MonoBehaviour
    {
        [SerializeField] private Vector3 degreesPerSecond = new Vector3(0f, 80f, 0f);

        private void Update()
        {
            transform.Rotate(degreesPerSecond * Time.deltaTime, Space.Self);
        }
    }
}
