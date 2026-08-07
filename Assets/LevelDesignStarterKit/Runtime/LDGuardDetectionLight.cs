using UnityEngine;
using UnityEngine.Rendering;

namespace LevelDesignStarterKit
{
    [DisallowMultipleComponent]
    public sealed class LDGuardDetectionLight : MonoBehaviour
    {
        private const string RangeObjectName = "Detection Light Range";
        private const string CatchRingObjectName = "Detection Catch Ring";
        private static readonly Color DefaultRangeColor = new Color(1f, 0f, 0f, 0.2f);
        private static readonly Color DefaultCatchRingColor = new Color(1f, 0f, 0f, 0.6f);

        [Header("Display")]
        [SerializeField] private bool showLightRange = true;
        [SerializeField] private Color patrolColor = DefaultRangeColor;
        [SerializeField] private Color alertColor = DefaultRangeColor;
        [SerializeField] private bool showCatchRing = true;
        [SerializeField] private Color catchRingColor = DefaultCatchRingColor;
        [SerializeField, Min(0.01f)] private float catchRingThickness = 0.08f;
        [SerializeField, Min(0f)] private float heightOffset = 0.04f;
        [SerializeField, Range(4, 96)] private int segmentCount = 32;

        private MeshFilter rangeFilter;
        private MeshRenderer rangeRenderer;
        private Mesh rangeMesh;
        private Material rangeMaterial;
        private Transform rangeTransform;
        private MeshFilter catchRingFilter;
        private MeshRenderer catchRingRenderer;
        private Mesh catchRingMesh;
        private Material catchRingMaterial;
        private Transform catchRingTransform;
        private float cachedDistance = -1f;
        private float cachedViewAngle = -1f;
        private int cachedSegmentCount = -1;
        private float cachedCatchRadius = -1f;
        private float cachedCatchRingThickness = -1f;
        private int cachedCatchRingSegmentCount = -1;

        public void SetRange(float detectionDistance, float viewAngle, bool alert, bool active, float catchDistance = 0f)
        {
            bool shouldShowRange = isActiveAndEnabled && showLightRange && active && detectionDistance > 0f && viewAngle > 0f;
            bool shouldShowCatchRing = isActiveAndEnabled && showCatchRing && active && catchDistance > 0f;

            if (shouldShowRange)
            {
                EnsureRangeVisuals();
                SetRangeRendererEnabled(true);
                UpdateRange(detectionDistance, viewAngle, alert);
            }
            else
            {
                SetRangeRendererEnabled(false);
            }

            if (shouldShowCatchRing)
            {
                EnsureCatchRingVisuals();
                SetCatchRingRendererEnabled(true);
                UpdateCatchRing(catchDistance);
            }
            else
            {
                SetCatchRingRendererEnabled(false);
            }
        }

        private void UpdateRange(float detectionDistance, float viewAngle, bool alert)
        {
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

            ApplyMaterialColor(rangeMaterial, alert ? alertColor : patrolColor);
        }

        private void UpdateCatchRing(float catchDistance)
        {
            float clampedCatchDistance = Mathf.Max(0f, catchDistance);
            float clampedThickness = Mathf.Max(0.01f, catchRingThickness);
            int clampedSegmentCount = Mathf.Max(12, segmentCount);

            catchRingTransform.localPosition = Vector3.up * (heightOffset + 0.01f);
            catchRingTransform.localRotation = Quaternion.identity;
            catchRingTransform.localScale = Vector3.one;

            if (!Mathf.Approximately(cachedCatchRadius, clampedCatchDistance) ||
                !Mathf.Approximately(cachedCatchRingThickness, clampedThickness) ||
                cachedCatchRingSegmentCount != clampedSegmentCount)
            {
                RebuildCatchRingMesh(clampedCatchDistance, clampedThickness, clampedSegmentCount);
            }

            ApplyMaterialColor(catchRingMaterial, catchRingColor);
        }

        private void EnsureRangeVisuals()
        {
            if (rangeRenderer != null &&
                rangeFilter != null &&
                rangeTransform != null &&
                rangeMesh != null &&
                MaterialSupportsColor(rangeMaterial))
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

            if (!MaterialSupportsColor(rangeMaterial))
            {
                rangeMaterial = CreateRangeMaterial();
            }

            rangeFilter.sharedMesh = rangeMesh;
            rangeRenderer.sharedMaterial = rangeMaterial;
            rangeRenderer.shadowCastingMode = ShadowCastingMode.Off;
            rangeRenderer.receiveShadows = false;
        }

        private void EnsureCatchRingVisuals()
        {
            if (catchRingRenderer != null &&
                catchRingFilter != null &&
                catchRingTransform != null &&
                catchRingMesh != null &&
                MaterialSupportsColor(catchRingMaterial))
            {
                return;
            }

            Transform existingRing = transform.Find(CatchRingObjectName);
            GameObject ringObject = existingRing != null ? existingRing.gameObject : new GameObject(CatchRingObjectName);
            ringObject.transform.SetParent(transform, false);

            catchRingTransform = ringObject.transform;
            catchRingFilter = ringObject.GetComponent<MeshFilter>();
            if (catchRingFilter == null)
            {
                catchRingFilter = ringObject.AddComponent<MeshFilter>();
            }

            catchRingRenderer = ringObject.GetComponent<MeshRenderer>();
            if (catchRingRenderer == null)
            {
                catchRingRenderer = ringObject.AddComponent<MeshRenderer>();
            }

            if (catchRingMesh == null)
            {
                catchRingMesh = new Mesh
                {
                    name = "Guard Detection Catch Ring"
                };
                catchRingMesh.MarkDynamic();
            }

            if (!MaterialSupportsColor(catchRingMaterial))
            {
                catchRingMaterial = CreateRangeMaterial("Guard Detection Catch Ring", 1);
            }

            catchRingFilter.sharedMesh = catchRingMesh;
            catchRingRenderer.sharedMaterial = catchRingMaterial;
            catchRingRenderer.shadowCastingMode = ShadowCastingMode.Off;
            catchRingRenderer.receiveShadows = false;
        }

        private Material CreateRangeMaterial()
        {
            return CreateRangeMaterial("Guard Detection Light Range", 0);
        }

        private Material CreateRangeMaterial(string materialName, int renderQueueOffset)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Legacy Shaders/Transparent/Diffuse");
            }

            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Material material = new Material(shader)
            {
                name = materialName,
                hideFlags = HideFlags.HideAndDontSave,
                renderQueue = (int)RenderQueue.Transparent + renderQueueOffset
            };

            material.SetOverrideTag("RenderType", "Transparent");
            SetFloatIfPresent(material, "_Mode", 3f);
            SetIntIfPresent(material, "_SrcBlend", (int)BlendMode.SrcAlpha);
            SetIntIfPresent(material, "_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            SetIntIfPresent(material, "_ZWrite", 0);
            SetIntIfPresent(material, "_Cull", (int)CullMode.Off);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            return material;
        }

        private static void ApplyMaterialColor(Material material, Color color)
        {
            if (material == null)
            {
                return;
            }

            SetColorIfPresent(material, "_Color", color);
            SetColorIfPresent(material, "_BaseColor", color);
            SetColorIfPresent(material, "_TintColor", color);
            SetColorIfPresent(material, "_RendererColor", color);
        }

        private static bool MaterialSupportsColor(Material material)
        {
            return material != null &&
                (material.HasProperty("_Color") ||
                    material.HasProperty("_BaseColor") ||
                    material.HasProperty("_TintColor") ||
                    material.HasProperty("_RendererColor"));
        }

        private static void SetColorIfPresent(Material material, string propertyName, Color color)
        {
            if (material.HasProperty(propertyName))
            {
                material.SetColor(propertyName, color);
            }
        }

        private static void SetFloatIfPresent(Material material, string propertyName, float value)
        {
            if (material.HasProperty(propertyName))
            {
                material.SetFloat(propertyName, value);
            }
        }

        private static void SetIntIfPresent(Material material, string propertyName, int value)
        {
            if (material.HasProperty(propertyName))
            {
                material.SetInt(propertyName, value);
            }
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

        private void RebuildCatchRingMesh(float catchDistance, float thickness, int clampedSegmentCount)
        {
            float innerRadius = Mathf.Max(0f, catchDistance - thickness * 0.5f);
            float outerRadius = catchDistance + thickness * 0.5f;
            Vector3[] vertices = new Vector3[(clampedSegmentCount + 1) * 2];
            int[] triangles = new int[clampedSegmentCount * 6];

            for (int segmentIndex = 0; segmentIndex <= clampedSegmentCount; segmentIndex++)
            {
                float radians = (segmentIndex / (float)clampedSegmentCount) * Mathf.PI * 2f;
                Vector3 direction = new Vector3(Mathf.Cos(radians), 0f, Mathf.Sin(radians));
                int vertexIndex = segmentIndex * 2;
                vertices[vertexIndex] = direction * innerRadius;
                vertices[vertexIndex + 1] = direction * outerRadius;
            }

            for (int segmentIndex = 0; segmentIndex < clampedSegmentCount; segmentIndex++)
            {
                int triangleIndex = segmentIndex * 6;
                int vertexIndex = segmentIndex * 2;
                triangles[triangleIndex] = vertexIndex;
                triangles[triangleIndex + 1] = vertexIndex + 1;
                triangles[triangleIndex + 2] = vertexIndex + 2;
                triangles[triangleIndex + 3] = vertexIndex + 1;
                triangles[triangleIndex + 4] = vertexIndex + 3;
                triangles[triangleIndex + 5] = vertexIndex + 2;
            }

            catchRingMesh.Clear();
            catchRingMesh.vertices = vertices;
            catchRingMesh.triangles = triangles;
            catchRingMesh.RecalculateBounds();

            cachedCatchRadius = catchDistance;
            cachedCatchRingThickness = thickness;
            cachedCatchRingSegmentCount = clampedSegmentCount;
        }

        private void SetRangeRendererEnabled(bool enabledState)
        {
            if (rangeRenderer != null)
            {
                rangeRenderer.enabled = enabledState;
            }
        }

        private void SetCatchRingRendererEnabled(bool enabledState)
        {
            if (catchRingRenderer != null)
            {
                catchRingRenderer.enabled = enabledState;
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

            if (catchRingMesh != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(catchRingMesh);
                }
                else
                {
                    DestroyImmediate(catchRingMesh);
                }
            }

            if (catchRingMaterial != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(catchRingMaterial);
                }
                else
                {
                    DestroyImmediate(catchRingMaterial);
                }
            }
        }
    }
}
