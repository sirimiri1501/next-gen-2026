using UnityEngine;
using UnityEngine.Rendering;

namespace LevelDesignStarterKit
{
    /// <summary>
    /// Gives an Energy Core a pulsing glow and sparkle effect. This component is
    /// installed at runtime so the collectible prefab does not need to change.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LDPowerPickupPresentation : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float pulseAmount = 0.1f;
        [SerializeField, Min(0.1f)] private float pulseSpeed = 3.5f;
        [SerializeField] private Color glowColor = new Color(1f, 0.72f, 0.05f, 1f);

        private Transform visual;
        private Vector3 baseScale;
        private Light glowLight;
        private Material sparkleMaterial;
        private Texture2D sparkleTexture;

        private void Awake()
        {
            visual = transform.Find("KeyVisual_Runtime");
            if (visual == null && GetComponent<LDKeyCollectible>() != null)
            {
                Transform originalVisual = transform.Find("Visual");
                if (originalVisual != null)
                {
                    visual = CreateKeyVisual(originalVisual);
                }
            }

            if (visual == null && GetComponentInChildren<Renderer>() != null)
            {
                visual = transform;
            }

            if (visual != null)
            {
                baseScale = visual.localScale;
            }

            CreateGlowLight();
            CreateSparkles();
        }

        private Transform CreateKeyVisual(Transform originalVisual)
        {
            Renderer originalRenderer = originalVisual.GetComponent<Renderer>();
            Material keyMaterial = originalRenderer != null ? originalRenderer.sharedMaterial : null;
            if (originalRenderer != null)
            {
                originalRenderer.enabled = false;
            }

            LDRotator originalRotator = originalVisual.GetComponent<LDRotator>();
            if (originalRotator != null)
            {
                originalRotator.enabled = false;
            }

            GameObject keyObject = new GameObject("KeyVisual_Runtime");
            keyObject.transform.SetParent(transform, false);
            keyObject.transform.localPosition = originalVisual.localPosition;
            keyObject.transform.localRotation = originalVisual.localRotation;
            keyObject.transform.localScale = Vector3.one;
            keyObject.layer = gameObject.layer;

            const int ringSegments = 14;
            const float ringRadius = 0.27f;
            Vector3 ringCenter = new Vector3(-0.34f, 0f, 0f);
            for (int i = 0; i < ringSegments; i++)
            {
                float angle = i * 360f / ringSegments;
                float radians = angle * Mathf.Deg2Rad;
                Vector3 position = ringCenter + new Vector3(
                    Mathf.Cos(radians) * ringRadius,
                    Mathf.Sin(radians) * ringRadius,
                    0f);

                GameObject ringPiece = CreateKeyPrimitive(
                    "Bow_" + i.ToString("00"),
                    position,
                    new Vector3(0.14f, 0.095f, 0.13f),
                    keyMaterial,
                    keyObject.transform);
                ringPiece.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            }

            CreateKeyPrimitive(
                "Shaft",
                new Vector3(0.22f, 0f, 0f),
                new Vector3(0.74f, 0.12f, 0.13f),
                keyMaterial,
                keyObject.transform);

            CreateKeyPrimitive(
                "Tooth_01",
                new Vector3(0.38f, -0.14f, 0f),
                new Vector3(0.13f, 0.25f, 0.13f),
                keyMaterial,
                keyObject.transform);

            CreateKeyPrimitive(
                "Tooth_02",
                new Vector3(0.55f, -0.11f, 0f),
                new Vector3(0.11f, 0.19f, 0.13f),
                keyMaterial,
                keyObject.transform);

            keyObject.AddComponent<LDRotator>();
            SetLayerRecursively(keyObject, gameObject.layer);
            return keyObject.transform;
        }

        private static GameObject CreateKeyPrimitive(
            string objectName,
            Vector3 localPosition,
            Vector3 localScale,
            Material material,
            Transform parent)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = objectName;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;

            Collider collider = part.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            Renderer renderer = part.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            return part;
        }

        private static void SetLayerRecursively(GameObject target, int layer)
        {
            target.layer = layer;
            foreach (Transform child in target.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }

        private void Update()
        {
            float wave = (Mathf.Sin(Time.unscaledTime * pulseSpeed) + 1f) * 0.5f;

            if (visual != null)
            {
                visual.localScale = baseScale * (1f + wave * pulseAmount);
            }

            if (glowLight != null)
            {
                glowLight.intensity = Mathf.Lerp(1.1f, 2.1f, wave);
                glowLight.range = Mathf.Lerp(2.2f, 3.1f, wave);
            }
        }

        private void OnDestroy()
        {
            DestroyRuntimeObject(sparkleMaterial);
            DestroyRuntimeObject(sparkleTexture);
        }

        private void CreateGlowLight()
        {
            GameObject lightObject = new GameObject("CollectibleGlow");
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.localPosition = GetVisualLocalPosition();
            lightObject.layer = gameObject.layer;

            glowLight = lightObject.AddComponent<Light>();
            glowLight.type = LightType.Point;
            glowLight.color = glowColor;
            glowLight.intensity = 1.5f;
            glowLight.range = 2.5f;
            glowLight.shadows = LightShadows.None;
        }

        private void CreateSparkles()
        {
            GameObject sparkleObject = new GameObject("CollectibleSparkles");
            sparkleObject.transform.SetParent(transform, false);
            sparkleObject.transform.localPosition = GetVisualLocalPosition();
            sparkleObject.layer = gameObject.layer;

            ParticleSystem sparkles = sparkleObject.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = sparkles.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.75f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.28f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.045f, 0.12f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.55f, 0.02f, 0.9f),
                new Color(1f, 1f, 0.65f, 1f));
            main.maxParticles = 40;

            ParticleSystem.EmissionModule emission = sparkles.emission;
            emission.rateOverTime = 16f;

            ParticleSystem.ShapeModule shape = sparkles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.62f;
            shape.radiusThickness = 0.2f;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = sparkles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient fadeGradient = new Gradient();
            fadeGradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.65f, 0.05f), 0f),
                    new GradientColorKey(Color.white, 0.55f),
                    new GradientColorKey(new Color(1f, 0.45f, 0.02f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.18f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = fadeGradient;

            ParticleSystemRenderer sparkleRenderer = sparkleObject.GetComponent<ParticleSystemRenderer>();
            sparkleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            sparkleRenderer.shadowCastingMode = ShadowCastingMode.Off;
            sparkleRenderer.receiveShadows = false;
            sparkleRenderer.sortingOrder = 60;

            Shader sparkleShader = Shader.Find("Sprites/Default");
            if (sparkleShader != null)
            {
                sparkleTexture = CreateSparkleTexture();
                sparkleMaterial = new Material(sparkleShader)
                {
                    name = "LD Power Sparkle Runtime",
                    color = Color.white,
                    mainTexture = sparkleTexture,
                    hideFlags = HideFlags.HideAndDontSave,
                    renderQueue = (int)RenderQueue.Transparent
                };
                sparkleRenderer.sharedMaterial = sparkleMaterial;
            }
        }

        private static Texture2D CreateSparkleTexture()
        {
            const int size = 32;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "LD Power Sparkle Texture Runtime",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            Color[] pixels = new Color[size * size];
            float center = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float normalizedX = (x - center) / center;
                    float normalizedY = (y - center) / center;
                    float distance = Mathf.Sqrt(normalizedX * normalizedX + normalizedY * normalizedY);
                    float alpha = Mathf.Pow(Mathf.Clamp01(1f - distance), 1.8f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
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

        private Vector3 GetVisualLocalPosition()
        {
            if (visual == null || visual == transform)
            {
                return Vector3.zero;
            }

            return visual.localPosition;
        }
    }

    /// <summary>
    /// Keeps the collected Energy Core visibly attached above the player. Since
    /// respawning moves the same player Transform, the carried Core follows it.
    /// </summary>
    internal sealed class LDPowerCarrySystem : MonoBehaviour
    {
        private const string CarriedPowerName = "LD_CarriedPower";

        private GameObject sourceVisual;
        private Transform carriedPower;

        private void Awake()
        {
            EnsureInitialized();
        }

        public void EnsureInitialized()
        {
            InstallCollectibleEffects();
            CacheSourceVisual();
        }

        private void Update()
        {
            LDGameSession session = LDGameSession.Instance;
            if (session == null || !session.HasKey)
            {
                return;
            }

            LDPlayerMotor player = session.Player;
            if (player == null)
            {
                player = FindObjectOfType<LDPlayerMotor>();
            }

            if (player != null)
            {
                EnsureCarriedPower(player.transform);
            }
        }

        private void InstallCollectibleEffects()
        {
            LDKeyCollectible[] collectibles = FindObjectsOfType<LDKeyCollectible>();
            foreach (LDKeyCollectible collectible in collectibles)
            {
                if (collectible.GetComponent<LDPowerPickupPresentation>() == null)
                {
                    collectible.gameObject.AddComponent<LDPowerPickupPresentation>();
                }
            }
        }

        private void CacheSourceVisual()
        {
            LDKeyCollectible collectible = FindObjectOfType<LDKeyCollectible>();
            if (collectible == null)
            {
                return;
            }

            Transform visual = collectible.transform.Find("KeyVisual_Runtime");
            if (visual == null)
            {
                visual = collectible.transform.Find("Visual");
            }
            if (visual != null)
            {
                sourceVisual = visual.gameObject;
            }
        }

        private void EnsureCarriedPower(Transform player)
        {
            if (carriedPower != null)
            {
                return;
            }

            Transform existing = player.Find(CarriedPowerName);
            if (existing != null)
            {
                carriedPower = existing;
                return;
            }

            GameObject powerObject = sourceVisual != null
                ? Instantiate(sourceVisual)
                : CreateFallbackPower();

            powerObject.name = CarriedPowerName;
            powerObject.SetActive(true);
            powerObject.transform.SetParent(player, false);
            powerObject.transform.localPosition = new Vector3(0f, 2.32f, 0f);
            powerObject.transform.localRotation = Quaternion.identity;
            powerObject.transform.localScale *= 0.82f;
            SetLayerRecursively(powerObject, player.gameObject.layer);

            Collider carriedCollider = powerObject.GetComponent<Collider>();
            if (carriedCollider != null)
            {
                Destroy(carriedCollider);
            }

            if (powerObject.GetComponent<LDRotator>() == null)
            {
                powerObject.AddComponent<LDRotator>();
            }

            powerObject.AddComponent<LDPowerPickupPresentation>();
            powerObject.AddComponent<LDCarriedPowerFloat>();
            carriedPower = powerObject.transform;
        }

        private static GameObject CreateFallbackPower()
        {
            GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Renderer renderer = fallback.GetComponent<Renderer>();
            renderer.material.color = new Color(1f, 0.72f, 0.05f, 1f);
            fallback.transform.localScale = Vector3.one * 0.65f;
            return fallback;
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

    internal sealed class LDCarriedPowerFloat : MonoBehaviour
    {
        private Vector3 baseLocalPosition;

        private void Awake()
        {
            baseLocalPosition = transform.localPosition;
        }

        private void Update()
        {
            float bob = Mathf.Sin(Time.unscaledTime * 3.2f) * 0.08f;
            transform.localPosition = baseLocalPosition + Vector3.up * bob;
        }
    }

    internal static class LDPowerPresentationInstaller
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InstallBeforeSceneLoad()
        {
            InstallOnce();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallAfterSceneLoad()
        {
            InstallOnce();
        }

        private static void InstallOnce()
        {
            LDPowerCarrySystem system = Object.FindObjectOfType<LDPowerCarrySystem>();
            if (system == null)
            {
                GameObject systemObject = new GameObject("LD_PowerPresentationSystem_Runtime");
                system = systemObject.AddComponent<LDPowerCarrySystem>();
            }

            system.EnsureInitialized();
        }
    }
}
