using System.Collections.Generic;
using System.IO;
using System.Linq;
using LevelDesignStarterKit;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LevelDesignStarterKit.Editor
{
    /// <summary>
    /// Generates all materials, prefabs, and a playable demo scene using only
    /// built-in Unity primitives. Safe to run repeatedly.
    /// </summary>
    public static class LDStarterKitBuilder
    {
        private const string RootFolder = "Assets/LevelDesignStarterKit";
        private const string GeneratedFolder = RootFolder + "/Generated";
        private const string MaterialsFolder = GeneratedFolder + "/Materials";
        private const string PrefabsFolder = GeneratedFolder + "/Prefabs";
        private const string ScenesFolder = GeneratedFolder + "/Scenes";
        private const string DemoScenePath = ScenesFolder + "/LD_StarterKit_Demo.unity";

        private sealed class KitAssets
        {
            public Material Gray;
            public Material DarkGray;
            public Material Blue;
            public Material Red;
            public Material Yellow;
            public Material Green;
            public Material Purple;
            public Material White;
            public GameObject PlayerPrefab;
            public GameObject GuardPrefab;
            public GameObject CheckpointPrefab;
            public GameObject KeyPrefab;
            public GameObject ExitPrefab;
        }

        [InitializeOnLoadMethod]
        private static void ScheduleFirstBuild()
        {
            EditorApplication.delayCall += TryBuildMissingGeneratedAssets;
        }

        private static void TryBuildMissingGeneratedAssets()
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += TryBuildMissingGeneratedAssets;
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(DemoScenePath) == null)
            {
                BuildAll();
            }
        }

        [MenuItem("Tools/Level Design Starter Kit/Build or Rebuild Complete Kit", priority = 1)]
        public static void BuildAll()
        {
            EnsureFolders();
            KitAssets assets = CreateGeneratedAssets();
            CreateDemoScene(assets);
            AddDemoSceneToBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            SceneAsset demoScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(DemoScenePath);
            Selection.activeObject = demoScene;
            Debug.Log("Level Design Starter Kit created successfully. Demo scene: " + DemoScenePath);
        }

        [MenuItem("GameObject/Level Design Starter Kit/Create Empty Patrol Path", false, 10)]
        private static void CreateEmptyPatrolPathMenu()
        {
            GameObject pathObject = new GameObject("LD_PatrolPath");
            Undo.RegisterCreatedObjectUndo(pathObject, "Create Patrol Path");
            pathObject.AddComponent<LDWaypointPath>();

            Vector3 center = GetSceneViewPlacementPosition();
            CreateWaypoint(pathObject.transform, "Waypoint_01", center + Vector3.left * 2f);
            CreateWaypoint(pathObject.transform, "Waypoint_02", center + Vector3.right * 2f);

            Selection.activeGameObject = pathObject;
        }

        [MenuItem("GameObject/Level Design Starter Kit/Create Guard With Patrol Path", false, 11)]
        private static void CreateGuardWithPathMenu()
        {
            GameObject guardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabsFolder + "/LD_Guard.prefab");
            if (guardPrefab == null)
            {
                Debug.LogWarning("Build the Starter Kit first: Tools > Level Design Starter Kit > Build or Rebuild Complete Kit");
                return;
            }

            Vector3 center = GetSceneViewPlacementPosition();
            GameObject pathObject = new GameObject("LD_PatrolPath");
            Undo.RegisterCreatedObjectUndo(pathObject, "Create Guard With Patrol Path");
            LDWaypointPath path = pathObject.AddComponent<LDWaypointPath>();
            CreateWaypoint(pathObject.transform, "Waypoint_01", center + Vector3.left * 2f);
            CreateWaypoint(pathObject.transform, "Waypoint_02", center + Vector3.right * 2f);

            GameObject guard = (GameObject)PrefabUtility.InstantiatePrefab(guardPrefab);
            Undo.RegisterCreatedObjectUndo(guard, "Create Guard With Patrol Path");
            guard.name = "LD_Guard";
            guard.transform.position = center + Vector3.left * 2f;
            guard.GetComponent<LDEnemyGuard>().Configure(path, null);
            Selection.activeGameObject = guard;
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets", "LevelDesignStarterKit");
            EnsureFolder(RootFolder, "Generated");
            EnsureFolder(GeneratedFolder, "Materials");
            EnsureFolder(GeneratedFolder, "Prefabs");
            EnsureFolder(GeneratedFolder, "Scenes");
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private static KitAssets CreateGeneratedAssets()
        {
            KitAssets assets = new KitAssets
            {
                Gray = GetOrCreateMaterial("LD_Gray", new Color(0.48f, 0.52f, 0.56f)),
                DarkGray = GetOrCreateMaterial("LD_DarkGray", new Color(0.20f, 0.23f, 0.27f)),
                Blue = GetOrCreateMaterial("LD_PlayerBlue", new Color(0.12f, 0.48f, 1f)),
                Red = GetOrCreateMaterial("LD_EnemyRed", new Color(0.95f, 0.16f, 0.12f)),
                Yellow = GetOrCreateMaterial("LD_ObjectiveYellow", new Color(1f, 0.72f, 0.05f)),
                Green = GetOrCreateMaterial("LD_ExitGreen", new Color(0.12f, 0.78f, 0.32f)),
                Purple = GetOrCreateMaterial("LD_CheckpointPurple", new Color(0.58f, 0.22f, 0.90f)),
                White = GetOrCreateMaterial("LD_White", new Color(0.92f, 0.94f, 0.96f))
            };

            assets.PlayerPrefab = CreatePlayerPrefab(assets.Blue, assets.White);
            assets.GuardPrefab = CreateGuardPrefab(assets.Red, assets.Yellow);
            assets.CheckpointPrefab = CreateCheckpointPrefab(assets.Purple);
            assets.KeyPrefab = CreateKeyPrefab(assets.Yellow);
            assets.ExitPrefab = CreateExitPrefab(assets.Green);
            return assets;
        }

        private static Material GetOrCreateMaterial(string name, Color color)
        {
            string path = MaterialsFolder + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null)
            {
                Shader shader = Shader.Find("Standard");
                if (shader == null)
                {
                    shader = Shader.Find("Universal Render Pipeline/Lit");
                }

                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = color;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject CreatePlayerPrefab(Material bodyMaterial, Material directionMaterial)
        {
            GameObject root = new GameObject("LD_Player");
            root.tag = "Player";
            root.layer = 2;

            CharacterController controller = root.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.36f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.stepOffset = 0.3f;
            controller.slopeLimit = 50f;
            root.AddComponent<LDPlayerMotor>();

            GameObject visual = CreatePrimitiveChild(
                root.transform,
                "Visual",
                PrimitiveType.Capsule,
                new Vector3(0f, 0.9f, 0f),
                new Vector3(0.72f, 0.9f, 0.72f),
                bodyMaterial,
                true);
            SetLayerRecursively(visual, 2);

            GameObject direction = CreatePrimitiveChild(
                root.transform,
                "ForwardMarker",
                PrimitiveType.Cube,
                new Vector3(0f, 1.05f, 0.37f),
                new Vector3(0.18f, 0.18f, 0.18f),
                directionMaterial,
                true);
            SetLayerRecursively(direction, 2);

            return SavePrefabAndDestroy(root, PrefabsFolder + "/LD_Player.prefab");
        }

        private static GameObject CreateGuardPrefab(Material bodyMaterial, Material directionMaterial)
        {
            GameObject root = new GameObject("LD_Guard");
            CharacterController controller = root.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.38f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.stepOffset = 0.3f;
            controller.slopeLimit = 50f;
            root.AddComponent<LDEnemyGuard>();

            CreatePrimitiveChild(
                root.transform,
                "Visual",
                PrimitiveType.Capsule,
                new Vector3(0f, 0.9f, 0f),
                new Vector3(0.76f, 0.9f, 0.76f),
                bodyMaterial,
                true);

            CreatePrimitiveChild(
                root.transform,
                "ViewDirection",
                PrimitiveType.Cube,
                new Vector3(0f, 1.05f, 0.41f),
                new Vector3(0.16f, 0.16f, 0.24f),
                directionMaterial,
                true);

            GameObject eye = new GameObject("Eye");
            eye.transform.SetParent(root.transform, false);
            eye.transform.localPosition = new Vector3(0f, 1.55f, 0f);

            SerializedObject serializedGuard = new SerializedObject(root.GetComponent<LDEnemyGuard>());
            serializedGuard.FindProperty("eye").objectReferenceValue = eye.transform;
            serializedGuard.ApplyModifiedPropertiesWithoutUndo();

            return SavePrefabAndDestroy(root, PrefabsFolder + "/LD_Guard.prefab");
        }

        private static GameObject CreateCheckpointPrefab(Material material)
        {
            GameObject root = new GameObject("LD_Checkpoint");
            BoxCollider trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 0.15f, 0f);
            trigger.size = new Vector3(2.2f, 0.5f, 2.2f);
            root.AddComponent<LDCheckpoint>();

            CreatePrimitiveChild(
                root.transform,
                "Visual",
                PrimitiveType.Cylinder,
                new Vector3(0f, 0.08f, 0f),
                new Vector3(1f, 0.08f, 1f),
                material,
                true);

            return SavePrefabAndDestroy(root, PrefabsFolder + "/LD_Checkpoint.prefab");
        }

        private static GameObject CreateKeyPrefab(Material material)
        {
            GameObject root = new GameObject("LD_EnergyCore");
            SphereCollider trigger = root.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 0.75f, 0f);
            trigger.radius = 0.75f;
            root.AddComponent<LDKeyCollectible>();

            GameObject visual = CreatePrimitiveChild(
                root.transform,
                "Visual",
                PrimitiveType.Cube,
                new Vector3(0f, 0.8f, 0f),
                new Vector3(0.65f, 0.65f, 0.65f),
                material,
                true);
            visual.transform.localRotation = Quaternion.Euler(25f, 45f, 20f);
            visual.AddComponent<LDRotator>();

            return SavePrefabAndDestroy(root, PrefabsFolder + "/LD_EnergyCore.prefab");
        }

        private static GameObject CreateExitPrefab(Material material)
        {
            GameObject root = new GameObject("LD_Exit");
            BoxCollider trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 1.5f, 0f);
            trigger.size = new Vector3(3.2f, 3f, 1.2f);
            root.AddComponent<LDExitGoal>();

            CreatePrimitiveChild(root.transform, "Pillar_L", PrimitiveType.Cube, new Vector3(-1.35f, 1.5f, 0f), new Vector3(0.35f, 3f, 0.45f), material, true);
            CreatePrimitiveChild(root.transform, "Pillar_R", PrimitiveType.Cube, new Vector3(1.35f, 1.5f, 0f), new Vector3(0.35f, 3f, 0.45f), material, true);
            CreatePrimitiveChild(root.transform, "Top", PrimitiveType.Cube, new Vector3(0f, 3f, 0f), new Vector3(3.05f, 0.35f, 0.45f), material, true);

            return SavePrefabAndDestroy(root, PrefabsFolder + "/LD_Exit.prefab");
        }

        private static GameObject SavePrefabAndDestroy(GameObject root, string path)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreatePrimitiveChild(
            Transform parent,
            string name,
            PrimitiveType primitiveType,
            Vector3 localPosition,
            Vector3 localScale,
            Material material,
            bool removeCollider)
        {
            GameObject child = GameObject.CreatePrimitive(primitiveType);
            child.name = name;
            child.transform.SetParent(parent, false);
            child.transform.localPosition = localPosition;
            child.transform.localScale = localScale;

            Renderer renderer = child.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }

            if (removeCollider)
            {
                Collider collider = child.GetComponent<Collider>();
                if (collider != null)
                {
                    Object.DestroyImmediate(collider);
                }
            }

            return child;
        }

        private static void CreateDemoScene(KitAssets assets)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject systemsRoot = new GameObject("--- SYSTEMS ---");
            GameObject gameplayRoot = new GameObject("--- GAMEPLAY ---");
            GameObject grayboxRoot = new GameObject("--- GRAYBOX ---");

            CreateLighting(systemsRoot.transform);

            GameObject player = InstantiatePrefab(assets.PlayerPrefab, "LD_Player", new Vector3(0f, 0f, -14f), Quaternion.identity, gameplayRoot.transform);
            LDPlayerMotor playerMotor = player.GetComponent<LDPlayerMotor>();

            GameObject cameraObject = new GameObject("LD_ThirdPersonCamera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(systemsRoot.transform);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 250f;
            cameraObject.AddComponent<AudioListener>();
            LDThirdPersonCamera followCamera = cameraObject.AddComponent<LDThirdPersonCamera>();
            followCamera.Configure(player.transform);
            playerMotor.ConfigureCamera(cameraObject.transform);

            GameObject sessionObject = new GameObject("LD_GameSession");
            sessionObject.transform.SetParent(systemsRoot.transform);
            LDGameSession session = sessionObject.AddComponent<LDGameSession>();
            session.Configure(playerMotor, player.transform);

            BuildGrayboxEnvironment(grayboxRoot.transform, assets);

            InstantiatePrefab(assets.CheckpointPrefab, "LD_Checkpoint", new Vector3(0f, 0f, 0f), Quaternion.identity, gameplayRoot.transform);
            InstantiatePrefab(assets.KeyPrefab, "LD_EnergyCore", new Vector3(9f, 0f, 10.5f), Quaternion.identity, gameplayRoot.transform);
            InstantiatePrefab(assets.ExitPrefab, "LD_Exit", new Vector3(0f, 0f, 14.8f), Quaternion.identity, gameplayRoot.transform);

            LDWaypointPath pathOne = CreatePatrolPath(
                "PatrolPath_01_Intro",
                gameplayRoot.transform,
                new Vector3(-5f, 0f, -7f),
                new Vector3(5f, 0f, -7f));
            GameObject guardOne = InstantiatePrefab(assets.GuardPrefab, "LD_Guard_01", new Vector3(-5f, 0f, -7f), Quaternion.LookRotation(Vector3.right), gameplayRoot.transform);
            guardOne.GetComponent<LDEnemyGuard>().Configure(pathOne, player.transform);

            LDWaypointPath pathTwo = CreatePatrolPath(
                "PatrolPath_02_RightRoute",
                gameplayRoot.transform,
                new Vector3(7f, 0f, 1.5f),
                new Vector3(7f, 0f, 8.5f));
            GameObject guardTwo = InstantiatePrefab(assets.GuardPrefab, "LD_Guard_02", new Vector3(7f, 0f, 1.5f), Quaternion.LookRotation(Vector3.forward), gameplayRoot.transform);
            guardTwo.GetComponent<LDEnemyGuard>().Configure(pathTwo, player.transform);

            LDWaypointPath pathThree = CreatePatrolPath(
                "PatrolPath_03_Core",
                gameplayRoot.transform,
                new Vector3(3f, 0f, 11f),
                new Vector3(10.5f, 0f, 11f));
            GameObject guardThree = InstantiatePrefab(assets.GuardPrefab, "LD_Guard_03", new Vector3(3f, 0f, 11f), Quaternion.LookRotation(Vector3.right), gameplayRoot.transform);
            guardThree.GetComponent<LDEnemyGuard>().Configure(pathThree, player.transform);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, DemoScenePath);
        }

        private static void CreateLighting(Transform parent)
        {
            GameObject lightObject = new GameObject("Directional Light");
            lightObject.transform.SetParent(parent);
            lightObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.shadows = LightShadows.Soft;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.68f, 0.78f);
            RenderSettings.ambientEquatorColor = new Color(0.42f, 0.45f, 0.50f);
            RenderSettings.ambientGroundColor = new Color(0.22f, 0.24f, 0.28f);
        }

        private static void BuildGrayboxEnvironment(Transform parent, KitAssets assets)
        {
            CreateGrayboxCube("Ground", new Vector3(0f, -0.25f, 0f), new Vector3(30f, 0.5f, 34f), assets.DarkGray, parent);

            CreateGrayboxCube("Boundary_Left", new Vector3(-15f, 1.5f, 0f), new Vector3(0.6f, 3f, 34f), assets.Gray, parent);
            CreateGrayboxCube("Boundary_Right", new Vector3(15f, 1.5f, 0f), new Vector3(0.6f, 3f, 34f), assets.Gray, parent);
            CreateGrayboxCube("Boundary_Back", new Vector3(0f, 1.5f, -17f), new Vector3(30f, 3f, 0.6f), assets.Gray, parent);
            CreateGrayboxCube("Boundary_Front_L", new Vector3(-8.5f, 1.5f, 17f), new Vector3(13f, 3f, 0.6f), assets.Gray, parent);
            CreateGrayboxCube("Boundary_Front_R", new Vector3(8.5f, 1.5f, 17f), new Vector3(13f, 3f, 0.6f), assets.Gray, parent);

            CreateGrayboxCube("IntroCover_L", new Vector3(-2.7f, 1.1f, -9.2f), new Vector3(2.2f, 2.2f, 1.2f), assets.Gray, parent);
            CreateGrayboxCube("IntroCover_R", new Vector3(2.8f, 1.1f, -5.3f), new Vector3(2f, 2.2f, 1.2f), assets.Gray, parent);
            CreateGrayboxCube("IntroSideWall_L", new Vector3(-8.5f, 1.5f, -4f), new Vector3(5f, 3f, 0.7f), assets.Gray, parent);
            CreateGrayboxCube("IntroSideWall_R", new Vector3(9.5f, 1.5f, -2.5f), new Vector3(7f, 3f, 0.7f), assets.Gray, parent);

            CreateGrayboxCube("CentralDivider", new Vector3(0f, 1.5f, 5f), new Vector3(1f, 3f, 7.5f), assets.Gray, parent);
            CreateGrayboxCube("LeftRouteCover_01", new Vector3(-5f, 1.25f, 2f), new Vector3(2.3f, 2.5f, 1f), assets.Gray, parent);
            CreateGrayboxCube("LeftRouteCover_02", new Vector3(-8f, 1.25f, 7f), new Vector3(1f, 2.5f, 3f), assets.Gray, parent);
            CreateGrayboxCube("RightRouteCover_01", new Vector3(5f, 1.25f, 4f), new Vector3(1.6f, 2.5f, 1f), assets.Gray, parent);
            CreateGrayboxCube("RightRouteCover_02", new Vector3(10f, 1.25f, 6.8f), new Vector3(1.2f, 2.5f, 2.4f), assets.Gray, parent);

            CreateGrayboxCube("CoreRoomWall_L", new Vector3(-6.5f, 1.5f, 12.8f), new Vector3(7f, 3f, 0.7f), assets.Gray, parent);
            CreateGrayboxCube("CoreCover_01", new Vector3(4.5f, 1.1f, 9.2f), new Vector3(1.5f, 2.2f, 1.5f), assets.Gray, parent);
            CreateGrayboxCube("CoreCover_02", new Vector3(8f, 1.1f, 13.1f), new Vector3(1.5f, 2.2f, 1.5f), assets.Gray, parent);

            GameObject optionalPlatform = CreateGrayboxCube("OptionalRewardPlatform", new Vector3(-10f, 0.55f, 10.5f), new Vector3(5f, 1.1f, 4f), assets.Gray, parent);
            optionalPlatform.name += "_PLACE_OPTIONAL_REWARD_HERE";

            GameObject ramp = CreateGrayboxCube("OptionalRouteRamp", new Vector3(-9.8f, 0.15f, 6.7f), new Vector3(3.5f, 0.5f, 4.5f), assets.Gray, parent);
            ramp.transform.rotation = Quaternion.Euler(-12f, 0f, 0f);
        }

        private static GameObject CreateGrayboxCube(string name, Vector3 position, Vector3 scale, Material material, Transform parent)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent);
            cube.transform.position = position;
            cube.transform.localScale = scale;
            cube.GetComponent<Renderer>().sharedMaterial = material;
            GameObjectUtility.SetStaticEditorFlags(
                cube,
                StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            return cube;
        }

        private static LDWaypointPath CreatePatrolPath(string name, Transform parent, params Vector3[] points)
        {
            GameObject pathObject = new GameObject(name);
            pathObject.transform.SetParent(parent);
            LDWaypointPath path = pathObject.AddComponent<LDWaypointPath>();

            for (int i = 0; i < points.Length; i++)
            {
                CreateWaypoint(pathObject.transform, "Waypoint_" + (i + 1).ToString("00"), points[i]);
            }

            return path;
        }

        private static Transform CreateWaypoint(Transform parent, string name, Vector3 position)
        {
            GameObject waypoint = new GameObject(name);
            waypoint.transform.SetParent(parent);
            waypoint.transform.position = position;
            return waypoint.transform;
        }

        private static GameObject InstantiatePrefab(
            GameObject prefab,
            string name,
            Vector3 position,
            Quaternion rotation,
            Transform parent)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = name;
            instance.transform.SetParent(parent);
            instance.transform.SetPositionAndRotation(position, rotation);
            return instance;
        }

        private static void AddDemoSceneToBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes
                .Where(scene => !string.Equals(scene.path, DemoScenePath, System.StringComparison.OrdinalIgnoreCase))
                .ToList();

            scenes.Insert(0, new EditorBuildSettingsScene(DemoScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static Vector3 GetSceneViewPlacementPosition()
        {
            if (SceneView.lastActiveSceneView != null)
            {
                Vector3 pivot = SceneView.lastActiveSceneView.pivot;
                return new Vector3(Mathf.Round(pivot.x), 0f, Mathf.Round(pivot.z));
            }

            return Vector3.zero;
        }

        private static void SetLayerRecursively(GameObject gameObject, int layer)
        {
            gameObject.layer = layer;
            foreach (Transform child in gameObject.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }
    }
}
