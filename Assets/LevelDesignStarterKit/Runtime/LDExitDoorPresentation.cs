using UnityEngine;
using UnityEngine.Rendering;

namespace LevelDesignStarterKit
{
    /// <summary>
    /// Adds a closed door and keyhole to LDExitGoal at runtime without changing
    /// the existing exit prefab or its completion trigger.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LDExitGoal))]
    public sealed class LDExitDoorPresentation : MonoBehaviour
    {
        private const string DoorRootName = "LD_ClosedDoor_Runtime";

        private Transform doorRoot;
        private Material keyholeMaterial;

        private void Awake()
        {
            EnsureDoor();
        }

        private void OnDestroy()
        {
            if (keyholeMaterial == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(keyholeMaterial);
            }
            else
            {
                DestroyImmediate(keyholeMaterial);
            }
        }

        public void EnsureDoor()
        {
            if (doorRoot != null)
            {
                return;
            }

            Transform existingDoor = transform.Find(DoorRootName);
            if (existingDoor != null)
            {
                doorRoot = existingDoor;
                return;
            }

            Material doorMaterial = FindExitMaterial();
            keyholeMaterial = CreateKeyholeMaterial();

            GameObject rootObject = new GameObject(DoorRootName);
            doorRoot = rootObject.transform;
            doorRoot.SetParent(transform, false);
            rootObject.layer = gameObject.layer;

            CreatePrimitive(
                "DoorPanel",
                PrimitiveType.Cube,
                doorRoot,
                new Vector3(0f, 1.5f, 0f),
                new Vector3(2.35f, 2.75f, 0.24f),
                doorMaterial);

            CreatePrimitive(
                "KeyholeRound",
                PrimitiveType.Sphere,
                doorRoot,
                new Vector3(0f, 1.62f, -0.145f),
                new Vector3(0.18f, 0.18f, 0.065f),
                keyholeMaterial);

            CreatePrimitive(
                "KeyholeStem",
                PrimitiveType.Cube,
                doorRoot,
                new Vector3(0f, 1.43f, -0.145f),
                new Vector3(0.105f, 0.28f, 0.06f),
                keyholeMaterial);

            SetLayerRecursively(rootObject, gameObject.layer);
        }

        private Material FindExitMaterial()
        {
            Transform leftPillar = transform.Find("Pillar_L");
            Renderer renderer = leftPillar != null
                ? leftPillar.GetComponent<Renderer>()
                : GetComponentInChildren<Renderer>();
            return renderer != null ? renderer.sharedMaterial : null;
        }

        private static Material CreateKeyholeMaterial()
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }

            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            if (shader == null)
            {
                return null;
            }

            return new Material(shader)
            {
                name = "LD Keyhole Dark Runtime",
                color = new Color(0.055f, 0.065f, 0.08f, 1f),
                hideFlags = HideFlags.HideAndDontSave
            };
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

            Collider collider = primitive.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
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

    internal static class LDExitDoorInstaller
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InstallBeforeSceneLoad()
        {
            InstallOnExistingExits();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallAfterSceneLoad()
        {
            InstallOnExistingExits();
        }

        private static void InstallOnExistingExits()
        {
            LDExitGoal[] exits = Object.FindObjectsOfType<LDExitGoal>();
            foreach (LDExitGoal exit in exits)
            {
                LDExitDoorPresentation presentation = exit.GetComponent<LDExitDoorPresentation>();
                if (presentation == null)
                {
                    presentation = exit.gameObject.AddComponent<LDExitDoorPresentation>();
                }

                presentation.EnsureDoor();
            }
        }
    }
}
