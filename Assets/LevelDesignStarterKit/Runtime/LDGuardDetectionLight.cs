using UnityEngine;
using UnityEngine.Rendering;

namespace LevelDesignStarterKit
{
    [DisallowMultipleComponent]
    public sealed class LDGuardDetectionLight : MonoBehaviour
    {
        private const string RangeObjectName = "Detection Light Range";

        [Header("Display")]
        [SerializeField] private bool showLightRange = true;
        [SerializeField] private Color patrolColor = new Color(1f, 0.75f, 0.1f, 0.28f);
        [SerializeField] private Color alertColor = new Color(1f, 0.05f, 0.02f, 0.35f);
        [SerializeField, Min(0f)] private float heightOffset = 0.04f;
        [SerializeField, Range(4, 96)] private int segmentCount = 32;

        private MeshFilter rangeFilter;
        private MeshRenderer rangeRenderer;
        private Mesh rangeMesh;
        private Material rangeMaterial;
        private Transform rangeTransform;
        private float cachedDistance = -1f;
        private float cachedViewAngle = -1f;
        private int cachedSegmentCount = -1;

        public void SetRange(float detectionDistance, float viewAngle, bool alert, bool active)
        {
            bool shouldShow = isActiveAndEnabled && showLightRange && active && detectionDistance > 0f && viewAngle > 0f;
            if (!shouldShow)
            {
                SetRendererEnabled(false);
                return;
            }

            EnsureVisuals();
            SetRendererEnabled(true);

            float clampedDistance = Mathf.Max(0f, detectionDistance);
            float clampedViewAngle = Mathf.Clamp(viewAngle, 1f, 179f);
            int clampedSegmentCount = Mathf.Max(4, segmentCount);

            rangeTransform.localPosition = Vector3.up * heightOffset;
            rangeTransform.localRotation = Quaternion.identity;
            rangeTransform.localScale = Vector3.one;

            if (!Mathf.Approximately(cachedDistance, clampedDistance) ||
                !Mathf.Approximately(cachedViewAngle, clampedViewAngle) ||
                cachedSegmentCount != clampedSegmentCount)
            {
                RebuildMesh(clampedDistance, clampedViewAngle, clampedSegmentCount);
            }

            rangeMaterial.color = alert ? alertColor : patrolColor;
        }

        private void EnsureVisuals()
        {
            if (rangeRenderer != null && rangeFilter != null && rangeTransform != null)
            {
                return;
            }

            Transform existingRange = transform.Find(RangeObjectName);
            GameObject rangeObject = existingRange != null ? existingRange.gameObject : new GameObject(RangeObjectName);
            rangeObject.transform.SetParent(transform, false);

            rangeTransform = rangeObject.transform;
            rangeFilter = rangeObject.GetComponent<MeshFilter>();
            if (rangeFilter == null)
            {
                rangeFilter = rangeObject.AddComponent<MeshFilter>();
            }

            rangeRenderer = rangeObject.GetComponent<MeshRenderer>();
            if (rangeRenderer == null)
            {
                rangeRenderer = rangeObject.AddComponent<MeshRenderer>();
            }

            if (rangeMesh == null)
            {
                rangeMesh = new Mesh
                {
                    name = "Guard Detection Light Range"
                };
                rangeMesh.MarkDynamic();
            }

            if (rangeMaterial == null)
            {
                rangeMaterial = CreateRangeMaterial();
            }

            rangeFilter.sharedMesh = rangeMesh;
            rangeRenderer.sharedMaterial = rangeMaterial;
            rangeRenderer.shadowCastingMode = ShadowCastingMode.Off;
            rangeRenderer.receiveShadows = false;
        }

        private Material CreateRangeMaterial()
        {
            Shader shader = Shader.Find("Unlit/Transparent");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Material material = new Material(shader)
            {
                name = "Guard Detection Light Range",
                hideFlags = HideFlags.HideAndDontSave,
                renderQueue = (int)RenderQueue.Transparent
            };

            material.SetOverrideTag("RenderType", "Transparent");
            material.SetFloat("_Mode", 3f);
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.SetInt("_Cull", (int)CullMode.Off);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            return material;
        }

        private void RebuildMesh(float detectionDistance, float viewAngle, int clampedSegmentCount)
        {
            Vector3[] vertices = new Vector3[clampedSegmentCount + 2];
            int[] triangles = new int[clampedSegmentCount * 3];

            vertices[0] = Vector3.zero;
            for (int segmentIndex = 0; segmentIndex <= clampedSegmentCount; segmentIndex++)
            {
                float angle = Mathf.Lerp(-viewAngle * 0.5f, viewAngle * 0.5f, segmentIndex / (float)clampedSegmentCount);
                Vector3 direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                vertices[segmentIndex + 1] = direction * detectionDistance;
            }

            for (int segmentIndex = 0; segmentIndex < clampedSegmentCount; segmentIndex++)
            {
                int triangleIndex = segmentIndex * 3;
                triangles[triangleIndex] = 0;
                triangles[triangleIndex + 1] = segmentIndex + 1;
                triangles[triangleIndex + 2] = segmentIndex + 2;
            }

            rangeMesh.Clear();
            rangeMesh.vertices = vertices;
            rangeMesh.triangles = triangles;
            rangeMesh.RecalculateBounds();

            cachedDistance = detectionDistance;
            cachedViewAngle = viewAngle;
            cachedSegmentCount = clampedSegmentCount;
        }

        private void SetRendererEnabled(bool enabledState)
        {
            if (rangeRenderer != null)
            {
                rangeRenderer.enabled = enabledState;
            }
        }

        private void OnDestroy()
        {
            if (rangeMesh != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(rangeMesh);
                }
                else
                {
                    DestroyImmediate(rangeMesh);
                }
            }

            if (rangeMaterial != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(rangeMaterial);
                }
                else
                {
                    DestroyImmediate(rangeMaterial);
                }
            }
        }
    }
}
