using UnityEngine;

namespace LevelDesignStarterKit
{
    public sealed class LDWaypointPath : MonoBehaviour
    {
        [SerializeField] private bool loop;
        [SerializeField] private Color gizmoColor = new Color(0.1f, 0.9f, 1f, 1f);

        public int Count => transform.childCount;
        public bool Loop => loop;

        public Transform GetPoint(int index)
        {
            if (Count == 0)
            {
                return null;
            }

            return transform.GetChild(Mathf.Clamp(index, 0, Count - 1));
        }

        public int GetClosestPointIndex(Vector3 worldPosition)
        {
            int closestIndex = 0;
            float closestDistance = float.PositiveInfinity;

            for (int i = 0; i < Count; i++)
            {
                float distance = (GetPoint(i).position - worldPosition).sqrMagnitude;
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestIndex = i;
                }
            }

            return closestIndex;
        }

        private void OnDrawGizmos()
        {
            if (Count == 0)
            {
                return;
            }

            Gizmos.color = gizmoColor;
            for (int i = 0; i < Count; i++)
            {
                Transform point = GetPoint(i);
                Gizmos.DrawSphere(point.position + Vector3.up * 0.08f, 0.18f);

                if (i < Count - 1)
                {
                    Gizmos.DrawLine(point.position + Vector3.up * 0.08f, GetPoint(i + 1).position + Vector3.up * 0.08f);
                }
            }

            if (loop && Count > 2)
            {
                Gizmos.DrawLine(GetPoint(Count - 1).position + Vector3.up * 0.08f, GetPoint(0).position + Vector3.up * 0.08f);
            }
        }
    }
}
