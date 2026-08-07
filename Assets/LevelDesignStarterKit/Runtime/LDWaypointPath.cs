using UnityEngine;

namespace LevelDesignStarterKit
{
    public sealed class LDWaypointPath : MonoBehaviour
    {
        [SerializeField] private bool loop;
        [SerializeField] private bool useBezierMovement;
        [SerializeField] private Color gizmoColor = new Color(0.1f, 0.9f, 1f, 1f);
        [SerializeField, Range(4, 48)] private int bezierPreviewStepsPerSegment = 16;

        public int Count => transform.childCount;
        public bool Loop => loop;
        public bool UseBezierMovement => useBezierMovement && Count > 1;
        public int CurveSegmentCount
        {
            get
            {
                if (Count < 2)
                {
                    return 0;
                }

                return loop && Count > 2 ? Count : Count - 1;
            }
        }

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

        public Vector3 GetBezierPoint(int segmentIndex, float t)
        {
            if (Count == 0)
            {
                return transform.position;
            }

            if (Count == 1)
            {
                return GetPoint(0).position;
            }

            int clampedSegmentIndex = Mathf.Clamp(segmentIndex, 0, Mathf.Max(0, CurveSegmentCount - 1));
            int startIndex = clampedSegmentIndex;
            int endIndex = GetNextIndex(startIndex);
            Vector3 previous = GetPoint(GetPreviousIndex(startIndex)).position;
            Vector3 start = GetPoint(startIndex).position;
            Vector3 end = GetPoint(endIndex).position;
            Vector3 next = GetPoint(GetNextIndex(endIndex)).position;
            Vector3 controlStart = start + (end - previous) / 6f;
            Vector3 controlEnd = end - (next - start) / 6f;
            float clampedT = Mathf.Clamp01(t);
            float inverseT = 1f - clampedT;

            return inverseT * inverseT * inverseT * start +
                3f * inverseT * inverseT * clampedT * controlStart +
                3f * inverseT * clampedT * clampedT * controlEnd +
                clampedT * clampedT * clampedT * end;
        }

        public float GetBezierSegmentLength(int segmentIndex)
        {
            int sampleCount = Mathf.Max(4, bezierPreviewStepsPerSegment);
            Vector3 previous = GetBezierPoint(segmentIndex, 0f);
            float length = 0f;

            for (int i = 1; i <= sampleCount; i++)
            {
                Vector3 next = GetBezierPoint(segmentIndex, i / (float)sampleCount);
                length += Vector3.Distance(previous, next);
                previous = next;
            }

            return Mathf.Max(0.001f, length);
        }

        public void GetClosestBezierProgress(Vector3 worldPosition, out int segmentIndex, out float segmentT)
        {
            segmentIndex = 0;
            segmentT = 0f;

            int segmentCount = CurveSegmentCount;
            if (segmentCount == 0)
            {
                return;
            }

            int sampleCount = Mathf.Max(4, bezierPreviewStepsPerSegment);
            float closestDistance = float.PositiveInfinity;

            for (int currentSegment = 0; currentSegment < segmentCount; currentSegment++)
            {
                for (int sampleIndex = 0; sampleIndex <= sampleCount; sampleIndex++)
                {
                    float t = sampleIndex / (float)sampleCount;
                    float distance = (GetBezierPoint(currentSegment, t) - worldPosition).sqrMagnitude;
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        segmentIndex = currentSegment;
                        segmentT = t;
                    }
                }
            }
        }

        private int GetPreviousIndex(int index)
        {
            if (loop && Count > 2)
            {
                return (index - 1 + Count) % Count;
            }

            return Mathf.Clamp(index - 1, 0, Count - 1);
        }

        private int GetNextIndex(int index)
        {
            if (loop && Count > 2)
            {
                return (index + 1) % Count;
            }

            return Mathf.Clamp(index + 1, 0, Count - 1);
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
            }

            if (UseBezierMovement)
            {
                DrawBezierGizmos();
                return;
            }

            for (int i = 0; i < Count; i++)
            {
                Transform point = GetPoint(i);
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

        private void DrawBezierGizmos()
        {
            int segmentCount = CurveSegmentCount;
            int sampleCount = Mathf.Max(4, bezierPreviewStepsPerSegment);

            for (int segmentIndex = 0; segmentIndex < segmentCount; segmentIndex++)
            {
                Vector3 previous = GetBezierPoint(segmentIndex, 0f) + Vector3.up * 0.08f;
                for (int sampleIndex = 1; sampleIndex <= sampleCount; sampleIndex++)
                {
                    Vector3 next = GetBezierPoint(segmentIndex, sampleIndex / (float)sampleCount) + Vector3.up * 0.08f;
                    Gizmos.DrawLine(previous, next);
                    previous = next;
                }
            }
        }
    }
}
