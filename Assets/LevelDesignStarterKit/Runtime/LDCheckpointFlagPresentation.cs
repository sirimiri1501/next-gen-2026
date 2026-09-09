using UnityEngine;
using UnityEngine.Rendering;

namespace LevelDesignStarterKit
{
    /// <summary>
    /// Adds a small purple flag to checkpoints at runtime while preserving the
    /// existing checkpoint prefab, trigger, material, and gameplay behavior.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LDCheckpoint))]
    public sealed class LDCheckpointFlagPresentation : MonoBehaviour
    {
        [SerializeField, Min(0.2f)] private float poleHeight = 1.55f;
        [SerializeField, Min(0.1f)] private float flagWidth = 0.78f;
        [SerializeField, Min(0.1f)] private float flagHeight = 0.38f;

        private Transform flagPivot;

        private void Awake()
        {
            Material checkpointMaterial = FindCheckpointMaterial();
            CreateFlag(checkpointMaterial);
        }

        private void Update()
        {
            if (flagPivot == null)
            {
                return;
            }

            float wave = Mathf.Sin(Time.unscaledTime * 2.8f) * 7f;
            float lift = Mathf.Sin(Time.unscaledTime * 1.9f) * 2f;
            flagPivot.localRotation = Quaternion.Euler(0f, wave, lift);
        }

        private Material FindCheckpointMaterial()
        {
            Transform visual = transform.Find("Visual");
            Renderer renderer = visual != null
                ? visual.GetComponent<Renderer>()
                : GetComponentInChildren<Renderer>();
            return renderer != null ? renderer.sharedMaterial : null;
        }

        private void CreateFlag(Material checkpointMaterial)
        {
            GameObject rootObject = new GameObject("LD_CheckpointFlag_Runtime");
            rootObject.transform.SetParent(transform, false);
            rootObject.layer = gameObject.layer;

            GameObject pole = CreatePrimitive(
                "Pole",
                PrimitiveType.Cylinder,
                rootObject.transform,
                new Vector3(0f, poleHeight * 0.5f, 0f),
                new Vector3(0.055f, poleHeight * 0.5f, 0.055f),
                checkpointMaterial);

            GameObject finial = CreatePrimitive(
                "Finial",
                PrimitiveType.Sphere,
                rootObject.transform,
                new Vector3(0f, poleHeight, 0f),
                Vector3.one * 0.15f,
                checkpointMaterial);

            GameObject pivotObject = new GameObject("FlagPivot");
            flagPivot = pivotObject.transform;
            flagPivot.SetParent(rootObject.transform, false);
            flagPivot.localPosition = new Vector3(0f, poleHeight - flagHeight * 0.55f, 0f);
            pivotObject.layer = gameObject.layer;

            GameObject flag = CreatePrimitive(
                "Flag",
                PrimitiveType.Cube,
                flagPivot,
                new Vector3(flagWidth * 0.5f, 0f, 0f),
                new Vector3(flagWidth, flagHeight, 0.055f),
                checkpointMaterial);

            SetLayerRecursively(pole, gameObject.layer);
            SetLayerRecursively(finial, gameObject.layer);
            SetLayerRecursively(flag, gameObject.layer);
        }

        private static GameObject CreatePrimitive(
            string objectName,
            PrimitiveType primitiveType,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale,
            Material material)
        {
            GameObject primitive = GameObject.CreatePrimitive(primitiveType);
            primitive.name = objectName;
            primitive.transform.SetParent(parent, false);
            primitive.transform.localPosition = localPosition;
            primitive.transform.localScale = localScale;

            Collider primitiveCollider = primitive.GetComponent<Collider>();
            if (primitiveCollider != null)
            {
                Destroy(primitiveCollider);
            }

            Renderer renderer = primitive.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            return primitive;
        }

        private static void SetLayerRecursively(GameObject target, int layer)
        {
            target.layer = layer;
            foreach (Transform child in target.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }
    }

    internal static class LDCheckpointFlagInstaller
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InstallBeforeSceneLoad()
        {
            InstallOnExistingCheckpoints();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallAfterSceneLoad()
        {
            InstallOnExistingCheckpoints();
        }

        private static void InstallOnExistingCheckpoints()
        {
            LDCheckpoint[] checkpoints = Object.FindObjectsOfType<LDCheckpoint>();
            foreach (LDCheckpoint checkpoint in checkpoints)
            {
                if (checkpoint.GetComponent<LDCheckpointFlagPresentation>() == null)
                {
                    checkpoint.gameObject.AddComponent<LDCheckpointFlagPresentation>();
                }
            }
        }
    }
}
