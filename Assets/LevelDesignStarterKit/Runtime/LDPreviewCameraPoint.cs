using UnityEngine;

namespace LevelDesignStarterKit
{
    public sealed class LDPreviewCameraPoint : MonoBehaviour
    {
        [SerializeField] private string displayName = "Preview Camera";
        [SerializeField] private int order;

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public int Order => order;

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.1f, 0.7f, 1f, 1f);
            Gizmos.DrawWireSphere(transform.position, 0.35f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 1.5f);

            Vector3 left = Quaternion.Euler(0f, -25f, 0f) * transform.forward;
            Vector3 right = Quaternion.Euler(0f, 25f, 0f) * transform.forward;
            Gizmos.DrawLine(transform.position + transform.forward * 0.45f, transform.position + left * 1.1f);
            Gizmos.DrawLine(transform.position + transform.forward * 0.45f, transform.position + right * 1.1f);
        }
    }
}
