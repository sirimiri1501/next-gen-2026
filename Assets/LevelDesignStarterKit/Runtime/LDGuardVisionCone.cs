using UnityEngine;

namespace LevelDesignStarterKit
{
    /// <summary>
    /// Visual-only representation of a guard's configured sight cone.
    /// The mesh is clipped against the same collision mask used by detection,
    /// but never participates in gameplay logic.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(LDEnemyGuard))]
    public sealed class LDGuardVisionCone : MonoBehaviour
    {
        [SerializeField, Range(6, 64)] private int segmentCount = 28;
        [SerializeField, Range(0.02f, 0.8f)] private float fillAlpha = 0.2f;
        [SerializeField, Range(0.01f, 0.2f)] private float outlineWidth = 0.055f;
        [SerializeField] private float groundOffset = 0.06f;

        private readonly RaycastHit[] raycastHits = new RaycastHit[16];
        private LDEnemyGuard guard;
        private GameObject visualRoot;
        private Mesh coneMesh;
        private Material fillMaterial;
        private Material outlineMaterial;
        private LineRenderer outline;
        private Vector3[] vertices;
        private int[] triangles;
        private Vector3[] outlinePoints;
        private bool rebuildRequested;

        private void OnEnable()
        {
            EnsureVisual();
            UpdateCone();
        }

        private void LateUpdate()
        {
            if (rebuildRequested)
            {
                rebuildRequested = false;
                CleanupVisual();
            }

            EnsureVisual();
            UpdateCone();
        }

        private void OnValidate()
        {
            rebuildRequested = true;
        }

        private void OnDisable()
        {
            CleanupVisual();
        }

        private void EnsureVisual()
        {
            if (guard == null)
            {
                guard = GetComponent<LDEnemyGuard>();
            }

            if (visualRoot != null || guard == null)
            {
                return;
            }

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            if (shader == null)
            {
                return;
            }

            visualRoot = new GameObject("LD_ViewCone_Red_Runtime");
            visualRoot.hideFlags = HideFlags.HideAndDontSave;
            visualRoot.transform.SetParent(transform, false);
            visualRoot.transform.localPosition = new Vector3(0f, groundOffset, 0f);

            coneMesh = new Mesh
            {
                name = "LD Guard View Cone Runtime Mesh",
                hideFlags = HideFlags.HideAndDontSave
            };

            fillMaterial = new Material(shader)
            {
                name = "LD Guard View Cone Fill Runtime",
                color = new Color(1f, 0.03f, 0.03f, fillAlpha),
                hideFlags = HideFlags.HideAndDontSave
            };

            outlineMaterial = new Material(shader)
            {
                name = "LD Guard View Cone Outline Runtime",
                color = new Color(1f, 0.02f, 0.02f, 0.9f),
                hideFlags = HideFlags.HideAndDontSave
            };

            MeshFilter filter = visualRoot.AddComponent<MeshFilter>();
            filter.sharedMesh = coneMesh;
            MeshRenderer renderer = visualRoot.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = fillMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            outline = visualRoot.AddComponent<LineRenderer>();
            outline.useWorldSpace = false;
            outline.loop = false;
            outline.widthMultiplier = outlineWidth;
            outline.numCornerVertices = 2;
            outline.numCapVertices = 2;
            outline.sharedMaterial = outlineMaterial;
            outline.startColor = outlineMaterial.color;
            outline.endColor = outlineMaterial.color;
            outline.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            outline.receiveShadows = false;

            int arcVertexCount = segmentCount + 1;
            vertices = new Vector3[arcVertexCount + 1];
            triangles = new int[segmentCount * 3];
            outlinePoints = new Vector3[arcVertexCount + 2];

            for (int i = 0; i < segmentCount; i++)
            {
                int triangle = i * 3;
                triangles[triangle] = 0;
                triangles[triangle + 1] = i + 1;
                triangles[triangle + 2] = i + 2;
            }

            coneMesh.vertices = vertices;
            coneMesh.triangles = triangles;
            outline.positionCount = outlinePoints.Length;
        }

        private void UpdateCone()
        {
            if (guard == null || coneMesh == null || vertices == null || outline == null)
            {
                return;
            }

            vertices[0] = Vector3.zero;
            outlinePoints[0] = Vector3.zero;

            float halfAngle = guard.ViewAngle * 0.5f;
            for (int i = 0; i <= segmentCount; i++)
            {
                float angle = Mathf.Lerp(-halfAngle, halfAngle, i / (float)segmentCount);
                Vector3 localDirection = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                Vector3 worldDirection = transform.TransformDirection(localDirection);
                float visibleDistance = GetVisibleDistance(worldDirection, guard.DetectionDistance);
                Vector3 point = localDirection * visibleDistance;
                vertices[i + 1] = point;
                outlinePoints[i + 1] = point;
            }

            outlinePoints[outlinePoints.Length - 1] = Vector3.zero;
            coneMesh.vertices = vertices;
            coneMesh.RecalculateBounds();
            outline.SetPositions(outlinePoints);
        }

        private float GetVisibleDistance(Vector3 worldDirection, float maximumDistance)
        {
            int hitCount = Physics.RaycastNonAlloc(
                guard.VisionOrigin,
                worldDirection,
                raycastHits,
                maximumDistance,
                guard.VisionMask,
                QueryTriggerInteraction.Ignore);

            float nearestDistance = maximumDistance;
            for (int i = 0; i < hitCount; i++)
            {
                Transform hitTransform = raycastHits[i].transform;
                if (hitTransform == null || hitTransform == transform || hitTransform.IsChildOf(transform))
                {
                    continue;
                }

                nearestDistance = Mathf.Min(nearestDistance, raycastHits[i].distance);
            }

            return nearestDistance;
        }

        private void CleanupVisual()
        {
            DestroyRuntimeObject(visualRoot);
            DestroyRuntimeObject(coneMesh);
            DestroyRuntimeObject(fillMaterial);
            DestroyRuntimeObject(outlineMaterial);
            visualRoot = null;
            coneMesh = null;
            fillMaterial = null;
            outlineMaterial = null;
            outline = null;
            vertices = null;
            triangles = null;
            outlinePoints = null;
        }

        private static void DestroyRuntimeObject(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
