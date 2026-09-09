using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;

namespace LevelDesignStarterKit
{
    /// <summary>
    /// Adds a player-visible radar cone to LDEnemyGuard without requiring changes
    /// to the guard implementation or its prefabs. Existing guards receive this
    /// component automatically after a scene loads.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LDEnemyGuard))]
    public sealed class LDGuardVisionVisualizer : MonoBehaviour
    {
        private const BindingFlags GuardFieldFlags = BindingFlags.Instance | BindingFlags.NonPublic;

        private static readonly FieldInfo DetectionDistanceField =
            typeof(LDEnemyGuard).GetField("detectionDistance", GuardFieldFlags);
        private static readonly FieldInfo ViewAngleField =
            typeof(LDEnemyGuard).GetField("viewAngle", GuardFieldFlags);
        private static readonly FieldInfo VisionMaskField =
            typeof(LDEnemyGuard).GetField("visionMask", GuardFieldFlags);
        private static readonly FieldInfo EyeField =
            typeof(LDEnemyGuard).GetField("eye", GuardFieldFlags);
        private static readonly FieldInfo StateField =
            typeof(LDEnemyGuard).GetField("state", GuardFieldFlags);

        [Header("Radar Shape")]
        [SerializeField, Range(12, 72)] private int segmentCount = 36;
        [SerializeField, Min(0.02f)] private float geometryUpdateInterval = 0.08f;
        [SerializeField, Min(0f)] private float surfaceOffset = 0.045f;
        [SerializeField] private bool clipAgainstObstacles = true;

        [Header("Radar Animation")]
        [SerializeField, Min(0.05f)] private float scanSpeed = 0.7f;
        [SerializeField, Min(0.01f)] private float borderWidth = 0.055f;
        [SerializeField, Min(0.01f)] private float scanLineWidth = 0.09f;

        [Header("Radar Colors")]
        [SerializeField] private Color patrolFill = new Color(1f, 0.12f, 0.02f, 0.16f);
        [SerializeField] private Color searchFill = new Color(1f, 0.42f, 0.02f, 0.22f);
        [SerializeField] private Color chaseFill = new Color(1f, 0.02f, 0.01f, 0.32f);
        [SerializeField] private Color borderColor = new Color(1f, 0.22f, 0.03f, 0.82f);
        [SerializeField] private Color scanColor = new Color(1f, 0.86f, 0.12f, 1f);

        [Header("Alert Indicator")]
        [SerializeField, Min(0.1f)] private float alertHeight = 2.55f;
        [SerializeField, Min(0.01f)] private float alertCharacterSize = 0.08f;
        [SerializeField] private Color alertColor = new Color(1f, 0.02f, 0.01f, 1f);

        private readonly RaycastHit[] obstacleHits = new RaycastHit[16];

        private LDEnemyGuard guard;
        private Transform eye;
        private Transform visualRoot;
        private Mesh radarMesh;
        private LineRenderer borderLine;
        private LineRenderer scanLine;
        private Transform alertTransform;
        private Material fillMaterial;
        private Material borderMaterial;
        private Material scanMaterial;
        private Vector3[] vertices;
        private int[] triangles;
        private float[] sampledDistances;
        private float detectionDistance = 8f;
        private float viewAngle = 75f;
        private int visionMask = ~0;
        private float nextGeometryUpdate;
        private float nextSettingsUpdate;
        private Vector3 radarOriginLocal;
        private string displayedState;

        private void Awake()
        {
            guard = GetComponent<LDEnemyGuard>();
            ReadGuardSettings();
            CreateVisuals();
            RebuildGeometry();
        }

        private void OnEnable()
        {
            if (visualRoot != null)
            {
                visualRoot.gameObject.SetActive(true);
            }
        }

        private void OnDisable()
        {
            if (visualRoot != null)
            {
                visualRoot.gameObject.SetActive(false);
            }
        }

        private void LateUpdate()
        {
            if (visualRoot == null)
            {
                return;
            }

            if (Time.unscaledTime >= nextSettingsUpdate)
            {
                ReadGuardSettings();
                nextSettingsUpdate = Time.unscaledTime + 0.5f;
            }

            if (Time.unscaledTime >= nextGeometryUpdate)
            {
                RebuildGeometry();
                nextGeometryUpdate = Time.unscaledTime + geometryUpdateInterval;
            }

            UpdateScanLine();
            UpdateStateColor();
            UpdateAlertIndicator();
        }

        private void OnDestroy()
        {
            DestroyRuntimeObject(radarMesh);
            DestroyRuntimeObject(fillMaterial);
            DestroyRuntimeObject(borderMaterial);
            DestroyRuntimeObject(scanMaterial);
        }

        private void ReadGuardSettings()
        {
            if (guard == null)
            {
                return;
            }

            if (DetectionDistanceField != null)
            {
                detectionDistance = Mathf.Max(0.1f, (float)DetectionDistanceField.GetValue(guard));
            }

            if (ViewAngleField != null)
            {
                viewAngle = Mathf.Clamp((float)ViewAngleField.GetValue(guard), 1f, 179f);
            }

            if (VisionMaskField != null)
            {
                visionMask = ((LayerMask)VisionMaskField.GetValue(guard)).value;
            }

            if (EyeField != null)
            {
                eye = EyeField.GetValue(guard) as Transform;
            }
        }

        private void CreateVisuals()
        {
            RemoveExistingRuntimeVisuals();

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            if (shader == null)
            {
                Debug.LogWarning("Guard radar could not find a transparent unlit shader.", this);
                enabled = false;
                return;
            }

            GameObject rootObject = new GameObject("LD_VisionRadar_Runtime");
            visualRoot = rootObject.transform;
            visualRoot.SetParent(transform, false);
            visualRoot.localPosition = Vector3.zero;
            rootObject.layer = gameObject.layer;

            radarMesh = new Mesh
            {
                name = "LD Guard Vision Radar",
                hideFlags = HideFlags.HideAndDontSave
            };
            radarMesh.MarkDynamic();

            fillMaterial = CreateRuntimeMaterial(shader, patrolFill, "LD Guard Radar Fill");
            borderMaterial = CreateRuntimeMaterial(shader, borderColor, "LD Guard Radar Border");
            scanMaterial = CreateRuntimeMaterial(shader, scanColor, "LD Guard Radar Scan");

            MeshFilter meshFilter = rootObject.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = radarMesh;

            MeshRenderer meshRenderer = rootObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = fillMaterial;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            meshRenderer.sortingOrder = -10;

            borderLine = CreateLineRenderer("Border", borderMaterial, borderWidth, 10);
            borderLine.enabled = false;
            scanLine = CreateLineRenderer("Scanner", scanMaterial, scanLineWidth, 20);
            CreateAlertIndicator();
        }

        private void RemoveExistingRuntimeVisuals()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child.name != "LD_VisionRadar_Runtime")
                {
                    continue;
                }

                child.gameObject.SetActive(false);
                DestroyRuntimeObject(child.gameObject);
            }
        }

        private void CreateAlertIndicator()
        {
            GameObject alertObject = new GameObject("AlertIndicator");
            alertTransform = alertObject.transform;
            alertTransform.SetParent(visualRoot, false);
            alertTransform.localPosition = Vector3.up * alertHeight;
            alertObject.layer = gameObject.layer;

            TextMesh alertText = alertObject.AddComponent<TextMesh>();
            alertText.text = "!";
            alertText.anchor = TextAnchor.MiddleCenter;
            alertText.alignment = TextAlignment.Center;
            alertText.fontSize = 96;
            alertText.fontStyle = FontStyle.Bold;
            alertText.characterSize = alertCharacterSize;
            alertText.color = alertColor;

            MeshRenderer alertRenderer = alertObject.GetComponent<MeshRenderer>();
            alertRenderer.shadowCastingMode = ShadowCastingMode.Off;
            alertRenderer.receiveShadows = false;
            alertRenderer.lightProbeUsage = LightProbeUsage.Off;
            alertRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            alertRenderer.sortingOrder = 100;

            alertObject.SetActive(false);
        }

        private LineRenderer CreateLineRenderer(string objectName, Material material, float width, int sortingOrder)
        {
            GameObject lineObject = new GameObject(objectName);
            lineObject.transform.SetParent(visualRoot, false);
            lineObject.layer = gameObject.layer;

            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = false;
            line.loop = false;
            line.startWidth = width;
            line.endWidth = width;
            line.numCapVertices = 2;
            line.numCornerVertices = 2;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.lightProbeUsage = LightProbeUsage.Off;
            line.reflectionProbeUsage = ReflectionProbeUsage.Off;
            line.sortingOrder = sortingOrder;
            return line;
        }

        private static Material CreateRuntimeMaterial(Shader shader, Color color, string materialName)
        {
            Material material = new Material(shader)
            {
                name = materialName,
                color = color,
                hideFlags = HideFlags.HideAndDontSave,
                renderQueue = (int)RenderQueue.Transparent
            };
            return material;
        }

        private void EnsureBuffers()
        {
            int safeSegmentCount = Mathf.Clamp(segmentCount, 12, 72);
            if (sampledDistances != null && sampledDistances.Length == safeSegmentCount + 1)
            {
                return;
            }

            segmentCount = safeSegmentCount;
            sampledDistances = new float[segmentCount + 1];
            vertices = new Vector3[segmentCount + 2];
            triangles = new int[segmentCount * 3];

            for (int i = 0; i < segmentCount; i++)
            {
                int triangleIndex = i * 3;
                triangles[triangleIndex] = 0;
                triangles[triangleIndex + 1] = i + 1;
                triangles[triangleIndex + 2] = i + 2;
            }
        }

        private void RebuildGeometry()
        {
            if (radarMesh == null || borderLine == null)
            {
                return;
            }

            EnsureBuffers();

            Vector3 rayOrigin = eye != null
                ? eye.position
                : transform.position + Vector3.up * 1.55f;
            float halfAngle = viewAngle * 0.5f;
            radarOriginLocal = transform.InverseTransformPoint(rayOrigin) + Vector3.up * surfaceOffset;
            vertices[0] = radarOriginLocal;

            borderLine.positionCount = segmentCount + 3;
            borderLine.SetPosition(0, vertices[0]);

            for (int i = 0; i <= segmentCount; i++)
            {
                float progress = i / (float)segmentCount;
                float angle = Mathf.Lerp(-halfAngle, halfAngle, progress);
                Vector3 localDirection = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                Vector3 worldDirection = transform.TransformDirection(localDirection);
                float visibleDistance = GetVisibleDistance(rayOrigin, worldDirection);

                sampledDistances[i] = visibleDistance;
                Vector3 point = new Vector3(
                    radarOriginLocal.x + localDirection.x * visibleDistance,
                    surfaceOffset,
                    radarOriginLocal.z + localDirection.z * visibleDistance);
                vertices[i + 1] = point;
                borderLine.SetPosition(i + 1, point + Vector3.up * 0.015f);
            }

            borderLine.SetPosition(segmentCount + 2, vertices[0]);

            radarMesh.Clear();
            radarMesh.vertices = vertices;
            radarMesh.triangles = triangles;
            radarMesh.RecalculateNormals();
            radarMesh.RecalculateBounds();
        }

        private float GetVisibleDistance(Vector3 origin, Vector3 direction)
        {
            if (!clipAgainstObstacles)
            {
                return detectionDistance;
            }

            int hitCount = Physics.RaycastNonAlloc(
                origin,
                direction,
                obstacleHits,
                detectionDistance,
                visionMask,
                QueryTriggerInteraction.Ignore);

            float nearestDistance = detectionDistance;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = obstacleHits[i];
                if (hit.transform == null || hit.transform == transform || hit.transform.IsChildOf(transform))
                {
                    continue;
                }

                if (hit.transform.GetComponentInParent<LDPlayerMotor>() != null)
                {
                    continue;
                }

                nearestDistance = Mathf.Min(nearestDistance, hit.distance);
            }

            return nearestDistance;
        }

        private void UpdateScanLine()
        {
            if (scanLine == null || sampledDistances == null || sampledDistances.Length < 2)
            {
                return;
            }

            float scanProgress = Mathf.PingPong(Time.unscaledTime * scanSpeed, 1f);
            float samplePosition = scanProgress * segmentCount;
            int lowerIndex = Mathf.Clamp(Mathf.FloorToInt(samplePosition), 0, segmentCount);
            int upperIndex = Mathf.Min(lowerIndex + 1, segmentCount);
            float interpolation = samplePosition - lowerIndex;
            Vector3 raisedOrigin = radarOriginLocal + Vector3.up * 0.035f;
            Vector3 raisedEnd = Vector3.Lerp(
                vertices[lowerIndex + 1],
                vertices[upperIndex + 1],
                interpolation) + Vector3.up * 0.035f;
            Vector3 scanDirection = (raisedEnd - raisedOrigin).normalized;

            scanLine.positionCount = 2;
            scanLine.SetPosition(0, raisedOrigin + scanDirection * 0.35f);
            scanLine.SetPosition(1, raisedEnd);
        }

        private void UpdateStateColor()
        {
            if (StateField == null || fillMaterial == null || guard == null)
            {
                return;
            }

            object stateValue = StateField.GetValue(guard);
            string stateName = stateValue != null ? stateValue.ToString() : string.Empty;
            if (stateName == displayedState)
            {
                return;
            }

            displayedState = stateName;
            switch (stateName)
            {
                case "Chase":
                    fillMaterial.color = chaseFill;
                    break;
                case "Search":
                    fillMaterial.color = searchFill;
                    break;
                default:
                    fillMaterial.color = patrolFill;
                    break;
            }
        }

        private void UpdateAlertIndicator()
        {
            if (alertTransform == null)
            {
                return;
            }

            bool shouldShow = displayedState == "Chase";
            if (alertTransform.gameObject.activeSelf != shouldShow)
            {
                alertTransform.gameObject.SetActive(shouldShow);
            }

            if (!shouldShow)
            {
                return;
            }

            Camera activeCamera = Camera.main;
            if (activeCamera != null)
            {
                alertTransform.rotation = activeCamera.transform.rotation;
            }

            float bob = Mathf.Sin(Time.unscaledTime * 7f) * 0.08f;
            float pulse = 1f + Mathf.Sin(Time.unscaledTime * 10f) * 0.12f;
            alertTransform.localPosition = Vector3.up * (alertHeight + bob);
            alertTransform.localScale = Vector3.one * pulse;
        }

        private static void DestroyRuntimeObject(Object runtimeObject)
        {
            if (runtimeObject == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(runtimeObject);
            }
            else
            {
                DestroyImmediate(runtimeObject);
            }
        }
    }

    internal static class LDGuardVisionVisualizerInstaller
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AttachBeforeSceneLoad()
        {
            AttachToExistingGuards();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AttachAfterSceneLoad()
        {
            AttachToExistingGuards();
        }

        private static void AttachToExistingGuards()
        {
            LDEnemyGuard[] guards = Object.FindObjectsOfType<LDEnemyGuard>();
            foreach (LDEnemyGuard guard in guards)
            {
                if (guard.GetComponent<LDGuardVisionVisualizer>() == null)
                {
                    guard.gameObject.AddComponent<LDGuardVisionVisualizer>();
                }

                DisableAllBorderLines(guard);
            }
        }

        private static void DisableAllBorderLines(LDEnemyGuard guard)
        {
            LineRenderer[] lines = guard.GetComponentsInChildren<LineRenderer>(true);
            foreach (LineRenderer line in lines)
            {
                if (line.name == "Border")
                {
                    line.enabled = false;
                }
            }
        }
    }
}
