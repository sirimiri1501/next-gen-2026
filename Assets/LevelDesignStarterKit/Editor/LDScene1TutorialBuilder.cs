using System;
using System.Collections.Generic;
using System.IO;
using LevelDesignStarterKit;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LevelDesignStarterKit.Editor
{
    /// <summary>
    /// Builds the first playable tutorial level without touching the demo scene.
    /// The level teaches movement, camera control, jumping, sightline breaking,
    /// route choice, Energy Core collection, and Exit completion.
    /// </summary>
    public static class LDScene1TutorialBuilder
    {
        private const string TargetScenePath = "Assets/LevelDesignStarterKit/Generated/Scenes/Level 1 Tutorial.unity";
        private const string RequestFileName = "BuildScene1Tutorial.request";
        private const string MaterialsFolder = "Assets/LevelDesignStarterKit/Generated/Materials";
        private const string PrefabsFolder = "Assets/LevelDesignStarterKit/Generated/Prefabs";

        private sealed class SceneAssets
        {
            public Material Gray;
            public Material DarkGray;
            public Material Green;
            public Material White;
            public GameObject Player;
            public GameObject Guard;
            public GameObject Checkpoint;
            public GameObject EnergyCore;
            public GameObject Exit;
        }

        [InitializeOnLoadMethod]
        private static void ScheduleRequestedBuild()
        {
            if (File.Exists(GetRequestPath()))
            {
                EditorApplication.delayCall += TryProcessBuildRequest;
            }
        }

        private static void TryProcessBuildRequest()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += TryProcessBuildRequest;
                return;
            }

            string requestPath = GetRequestPath();
            if (!File.Exists(requestPath))
            {
                return;
            }

            try
            {
                BuildSceneOneTutorial();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                File.Delete(requestPath);
            }
        }

        [MenuItem("Tools/Level Design Starter Kit/Build Scene 1 Tutorial", priority = 20)]
        public static void BuildSceneOneTutorial()
        {
            Scene loadedTarget = GetLoadedScene(TargetScenePath);
            if (loadedTarget.IsValid() && loadedTarget.isDirty)
            {
                throw new InvalidOperationException(
                    "Level 1 Tutorial has unsaved changes. Save or revert them before rebuilding so no work is lost.");
            }

            Scene previousActive = SceneManager.GetActiveScene();
            bool targetWasLoaded = loadedTarget.IsValid();
            Scene tutorialScene = targetWasLoaded
                ? loadedTarget
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

            BackupCurrentSceneFile();

            try
            {
                SceneManager.SetActiveScene(tutorialScene);
                ClearScene(tutorialScene);

                SceneAssets assets = LoadAssets();
                BuildSceneContents(tutorialScene, assets);
                RenderReviewPreviews(tutorialScene);

                EditorSceneManager.MarkSceneDirty(tutorialScene);
                if (!EditorSceneManager.SaveScene(tutorialScene, TargetScenePath))
                {
                    throw new IOException("Unity could not save " + TargetScenePath);
                }

                AddSceneToBuildSettings();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                bool valid = ValidateScene(tutorialScene, out string validationSummary);
                if (!valid)
                {
                    throw new InvalidOperationException("Scene 1 structural validation failed: " + validationSummary);
                }

                SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(TargetScenePath);
                Selection.activeObject = sceneAsset;
                Debug.Log("SCENE 1 TUTORIAL BUILD COMPLETE — " + validationSummary + " — " + TargetScenePath);
            }
            finally
            {
                if (!targetWasLoaded && tutorialScene.IsValid() && tutorialScene.isLoaded)
                {
                    EditorSceneManager.CloseScene(tutorialScene, true);
                }

                if (previousActive.IsValid() && previousActive.isLoaded)
                {
                    SceneManager.SetActiveScene(previousActive);
                }
            }
        }

        [MenuItem("Tools/Level Design Starter Kit/Validate Scene 1 Tutorial", priority = 21)]
        public static void ValidateSceneOneTutorial()
        {
            Scene scene = GetLoadedScene(TargetScenePath);
            bool openedForValidation = !scene.IsValid();

            if (openedForValidation)
            {
                scene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Additive);
            }

            try
            {
                bool valid = ValidateScene(scene, out string summary);
                if (valid)
                {
                    Debug.Log("SCENE 1 TUTORIAL VALIDATION PASS — " + summary);
                }
                else
                {
                    Debug.LogError("SCENE 1 TUTORIAL VALIDATION FAIL — " + summary);
                }
            }
            finally
            {
                if (openedForValidation && scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static void BuildSceneContents(Scene scene, SceneAssets assets)
        {
            GameObject systemsRoot = CreateRoot("--- SYSTEMS ---");
            GameObject gameplayRoot = CreateRoot("--- GAMEPLAY ---");
            GameObject grayboxRoot = CreateRoot("--- GRAYBOX ---");

            CreateLighting(systemsRoot.transform);

            GameObject player = InstantiatePrefab(
                assets.Player,
                "LD_Player",
                new Vector3(0f, 0f, -16.2f),
                Quaternion.identity,
                gameplayRoot.transform,
                scene);
            LDPlayerMotor playerMotor = player.GetComponent<LDPlayerMotor>();

            GameObject cameraObject = new GameObject("LD_ThirdPersonCamera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(systemsRoot.transform);
            cameraObject.transform.position = new Vector3(0f, 3.5f, -21f);
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

            GameObject arrivalBeat = CreateGroup("BEAT_01_ARRIVAL_MOVEMENT", grayboxRoot.transform);
            GameObject learnBeat = CreateGroup("BEAT_02_LEARN_SIGHTLINE", grayboxRoot.transform);
            GameObject choiceBeat = CreateGroup("BEAT_03_ROUTE_CHOICE", grayboxRoot.transform);
            GameObject challengeBeat = CreateGroup("BEAT_04_CORE_CHALLENGE", grayboxRoot.transform);
            GameObject rewardBeat = CreateGroup("BEAT_05_REWARD_EXIT", grayboxRoot.transform);

            BuildArrival(arrivalBeat.transform, assets);
            BuildLearnArea(learnBeat.transform, assets);
            BuildRouteChoice(choiceBeat.transform, assets);
            BuildCoreChallenge(challengeBeat.transform, assets);
            BuildExitLandmark(rewardBeat.transform, assets);

            InstantiatePrefab(
                assets.Checkpoint,
                "LD_Checkpoint_AfterJump",
                new Vector3(0f, 0f, -6.4f),
                Quaternion.identity,
                gameplayRoot.transform,
                scene);

            InstantiatePrefab(
                assets.EnergyCore,
                "LD_EnergyCore",
                new Vector3(8.7f, 0f, 13.8f),
                Quaternion.identity,
                gameplayRoot.transform,
                scene);

            InstantiatePrefab(
                assets.Exit,
                "LD_Exit",
                new Vector3(0f, 0f, 16.1f),
                Quaternion.identity,
                gameplayRoot.transform,
                scene);

            CreateGuard(
                "LD_Guard_01_Learn",
                "PatrolPath_01_Learn",
                new Vector3(-7f, 0f, -0.8f),
                new[] { new Vector3(-7f, 0f, -0.8f), new Vector3(7f, 0f, -0.8f) },
                player.transform,
                gameplayRoot.transform,
                assets.Guard,
                scene);

            CreateGuard(
                "LD_Guard_02_RiskyRoute",
                "PatrolPath_02_RiskyRoute",
                new Vector3(7.2f, 0f, 3.2f),
                new[] { new Vector3(7.2f, 0f, 3.2f), new Vector3(7.2f, 0f, 10.2f) },
                player.transform,
                gameplayRoot.transform,
                assets.Guard,
                scene);

            CreateGuard(
                "LD_Guard_03_Core",
                "PatrolPath_03_Core",
                new Vector3(-4.5f, 0f, 12.4f),
                new[] { new Vector3(-4.5f, 0f, 12.4f), new Vector3(9.5f, 0f, 12.4f) },
                player.transform,
                gameplayRoot.transform,
                assets.Guard,
                scene);
        }

        private static void BuildArrival(Transform parent, SceneAssets assets)
        {
            CreateCube("StartGround", new Vector3(0f, -0.25f, -17f), new Vector3(8f, 0.5f, 10f), assets.DarkGray, parent);
            CreateCube("StartBoundary_Back", new Vector3(0f, 1.5f, -22f), new Vector3(8.5f, 3f, 0.5f), assets.Gray, parent);
            CreateCube("StartBoundary_Left", new Vector3(-4.2f, 1.5f, -17f), new Vector3(0.5f, 3f, 10f), assets.Gray, parent);
            CreateCube("StartBoundary_Right", new Vector3(4.2f, 1.5f, -17f), new Vector3(0.5f, 3f, 10f), assets.Gray, parent);

            GameObject ramp = CreateCube(
                "MandatoryJump_Ramp",
                new Vector3(0f, 0.1f, -11.3f),
                new Vector3(4.2f, 0.5f, 2.7f),
                assets.Gray,
                parent);
            ramp.transform.rotation = Quaternion.Euler(-12f, 0f, 0f);

            GameObject spikeGroup = CreateGroup("JumpGap_Spikes_VISUAL_ONLY", parent);
            for (int row = 0; row < 3; row++)
            {
                for (int column = -3; column <= 3; column++)
                {
                    CreateCube(
                        "SpikeMarker",
                        new Vector3(column * 0.85f, -0.65f, -9.7f + row * 0.6f),
                        new Vector3(0.18f, 1.1f, 0.18f),
                        assets.White,
                        spikeGroup.transform,
                        false);
                }
            }

            CreateCube("LandingGround", new Vector3(0f, -0.25f, 4.5f), new Vector3(30f, 0.5f, 25.5f), assets.DarkGray, parent);
            CreateCube("LevelBoundary_Left", new Vector3(-15f, 1.5f, 4.5f), new Vector3(0.6f, 3f, 25.5f), assets.Gray, parent);
            CreateCube("LevelBoundary_Right", new Vector3(15f, 1.5f, 4.5f), new Vector3(0.6f, 3f, 25.5f), assets.Gray, parent);
            CreateCube("LevelBoundary_Front_L", new Vector3(-8.5f, 1.5f, 17.3f), new Vector3(13f, 3f, 0.6f), assets.Gray, parent);
            CreateCube("LevelBoundary_Front_R", new Vector3(8.5f, 1.5f, 17.3f), new Vector3(13f, 3f, 0.6f), assets.Gray, parent);
        }

        private static void BuildLearnArea(Transform parent, SceneAssets assets)
        {
            CreateCube("SafeObservationCover", new Vector3(0f, 1.1f, -4.2f), new Vector3(5.4f, 2.2f, 0.8f), assets.Gray, parent);
            CreateCube("LearnCover_Left", new Vector3(-4.2f, 1.1f, -1.9f), new Vector3(2.2f, 2.2f, 1.2f), assets.Gray, parent);
            CreateCube("LearnCover_Right", new Vector3(4.2f, 1.1f, 1.1f), new Vector3(2.2f, 2.2f, 1.2f), assets.Gray, parent);
            CreateCube("LearnExitCover", new Vector3(0f, 1.1f, 2f), new Vector3(3f, 2.2f, 0.9f), assets.Gray, parent);
        }

        private static void BuildRouteChoice(Transform parent, SceneAssets assets)
        {
            CreateCube("RouteDivider", new Vector3(0f, 1.5f, 6.3f), new Vector3(1.1f, 3f, 7f), assets.Gray, parent);

            GameObject mainRoute = CreateGroup("MAIN_ROUTE_SAFE_LONG", parent);
            CreateCube("MainRouteCover_01", new Vector3(-4.2f, 1.2f, 4.1f), new Vector3(2.2f, 2.4f, 1f), assets.Gray, mainRoute.transform);
            CreateCube("MainRouteCover_02", new Vector3(-7.5f, 1.2f, 7.2f), new Vector3(1.2f, 2.4f, 3f), assets.Gray, mainRoute.transform);
            CreateCube("MainRouteCover_03", new Vector3(-4.8f, 1.2f, 10.1f), new Vector3(2.4f, 2.4f, 1f), assets.Gray, mainRoute.transform);

            GameObject optionalRoute = CreateGroup("OPTIONAL_ROUTE_RISKY_SHORT_INFORMATION_REWARD", parent);
            CreateCube("RiskyRouteCover_01", new Vector3(5f, 1.2f, 5.4f), new Vector3(1.5f, 2.4f, 1f), assets.Gray, optionalRoute.transform);
            CreateCube("RiskyRouteCover_02", new Vector3(10f, 1.2f, 9.2f), new Vector3(1.2f, 2.4f, 2.2f), assets.Gray, optionalRoute.transform);
            CreateCube("ObservationDeck_INFORMATION_REWARD", new Vector3(11.4f, 0.25f, 6.2f), new Vector3(4.5f, 0.5f, 3.8f), assets.Gray, optionalRoute.transform);
            GameObject deckRamp = CreateCube("ObservationDeck_Ramp", new Vector3(9.4f, 0.05f, 6.2f), new Vector3(2.2f, 0.45f, 2.2f), assets.Gray, optionalRoute.transform);
            deckRamp.transform.rotation = Quaternion.Euler(0f, 0f, -12f);
            CreateCube("ObservationDeck_SafeCover", new Vector3(12.5f, 1.1f, 7.6f), new Vector3(3.8f, 2.2f, 0.6f), assets.Gray, optionalRoute.transform);
        }

        private static void BuildCoreChallenge(Transform parent, SceneAssets assets)
        {
            CreateCube("CoreApproachCover_Left", new Vector3(-7f, 1.25f, 12.2f), new Vector3(2.5f, 2.5f, 1f), assets.Gray, parent);
            CreateCube("CoreApproachCover_Center", new Vector3(0f, 1.25f, 10.8f), new Vector3(1.2f, 2.5f, 2.8f), assets.Gray, parent);
            CreateCube("CoreCover_01", new Vector3(4.5f, 1.1f, 13.5f), new Vector3(1.5f, 2.2f, 1.5f), assets.Gray, parent);
            CreateCube("CoreCover_02", new Vector3(10.8f, 1.1f, 14.5f), new Vector3(1.5f, 2.2f, 1.5f), assets.Gray, parent);
        }

        private static void BuildExitLandmark(Transform parent, SceneAssets assets)
        {
            GameObject landmark = CreateGroup("GOAL_LANDMARK_EXIT_BEACON", parent);
            CreateCube("ExitBeacon_Left", new Vector3(-2.2f, 4f, 16.45f), new Vector3(0.5f, 8f, 0.5f), assets.Green, landmark.transform, false);
            CreateCube("ExitBeacon_Right", new Vector3(2.2f, 4f, 16.45f), new Vector3(0.5f, 8f, 0.5f), assets.Green, landmark.transform, false);
            CreateCube("ExitBeacon_Top", new Vector3(0f, 7.8f, 16.45f), new Vector3(4.9f, 0.5f, 0.5f), assets.Green, landmark.transform, false);
        }

        private static void CreateGuard(
            string guardName,
            string pathName,
            Vector3 guardPosition,
            Vector3[] waypointPositions,
            Transform player,
            Transform gameplayRoot,
            GameObject guardPrefab,
            Scene scene)
        {
            GameObject pathObject = new GameObject(pathName);
            pathObject.transform.SetParent(gameplayRoot);
            LDWaypointPath path = pathObject.AddComponent<LDWaypointPath>();

            for (int i = 0; i < waypointPositions.Length; i++)
            {
                GameObject waypoint = new GameObject("Waypoint_" + (i + 1).ToString("00"));
                waypoint.transform.SetParent(pathObject.transform);
                waypoint.transform.position = waypointPositions[i];
            }

            Vector3 direction = waypointPositions.Length > 1
                ? (waypointPositions[1] - waypointPositions[0]).normalized
                : Vector3.forward;
            GameObject guard = InstantiatePrefab(
                guardPrefab,
                guardName,
                guardPosition,
                Quaternion.LookRotation(direction, Vector3.up),
                gameplayRoot,
                scene);
            guard.GetComponent<LDEnemyGuard>().Configure(path, player);
        }

        private static bool ValidateScene(Scene scene, out string summary)
        {
            List<string> failures = new List<string>();
            int passed = 0;

            Check(HasRoot(scene, "--- SYSTEMS ---"), "missing SYSTEMS root", failures, ref passed);
            Check(HasRoot(scene, "--- GAMEPLAY ---"), "missing GAMEPLAY root", failures, ref passed);
            Check(HasRoot(scene, "--- GRAYBOX ---"), "missing GRAYBOX root", failures, ref passed);
            Check(CountComponents<LDPlayerMotor>(scene) == 1, "expected exactly 1 player", failures, ref passed);
            Check(CountComponents<LDThirdPersonCamera>(scene) == 1, "expected exactly 1 third-person camera", failures, ref passed);
            Check(CountComponents<LDGameSession>(scene) == 1, "expected exactly 1 game session", failures, ref passed);
            Check(CountComponents<LDCheckpoint>(scene) == 1, "expected exactly 1 checkpoint", failures, ref passed);
            Check(CountComponents<LDKeyCollectible>(scene) == 1, "expected exactly 1 Energy Core", failures, ref passed);
            Check(CountComponents<LDExitGoal>(scene) == 1, "expected exactly 1 Exit", failures, ref passed);
            Check(CountComponents<LDEnemyGuard>(scene) == 3, "expected exactly 3 guards", failures, ref passed);

            LDWaypointPath[] paths = GetComponentsInScene<LDWaypointPath>(scene);
            bool pathsValid = paths.Length == 3;
            foreach (LDWaypointPath path in paths)
            {
                pathsValid &= path.Count >= 2;
            }
            Check(pathsValid, "expected 3 patrol paths with at least 2 points each", failures, ref passed);

            Check(FindInScene(scene, "MandatoryJump_Ramp") != null, "missing mandatory jump ramp", failures, ref passed);
            Check(FindInScene(scene, "MAIN_ROUTE_SAFE_LONG") != null, "missing main route", failures, ref passed);
            Check(FindInScene(scene, "OPTIONAL_ROUTE_RISKY_SHORT_INFORMATION_REWARD") != null, "missing optional route reward", failures, ref passed);
            Check(FindInScene(scene, "GOAL_LANDMARK_EXIT_BEACON") != null, "missing Exit landmark", failures, ref passed);

            summary = failures.Count == 0
                ? passed + "/" + passed + " structural checks PASS"
                : passed + " checks passed; " + string.Join("; ", failures);
            return failures.Count == 0;
        }

        private static void Check(bool condition, string failure, List<string> failures, ref int passed)
        {
            if (condition)
            {
                passed++;
            }
            else
            {
                failures.Add(failure);
            }
        }

        private static SceneAssets LoadAssets()
        {
            SceneAssets assets = new SceneAssets
            {
                Gray = LoadRequired<Material>(MaterialsFolder + "/LD_Gray.mat"),
                DarkGray = LoadRequired<Material>(MaterialsFolder + "/LD_DarkGray.mat"),
                Green = LoadRequired<Material>(MaterialsFolder + "/LD_ExitGreen.mat"),
                White = LoadRequired<Material>(MaterialsFolder + "/LD_White.mat"),
                Player = LoadRequired<GameObject>(PrefabsFolder + "/LD_Player.prefab"),
                Guard = LoadRequired<GameObject>(PrefabsFolder + "/LD_Guard.prefab"),
                Checkpoint = LoadRequired<GameObject>(PrefabsFolder + "/LD_Checkpoint.prefab"),
                EnergyCore = LoadRequired<GameObject>(PrefabsFolder + "/LD_EnergyCore.prefab"),
                Exit = LoadRequired<GameObject>(PrefabsFolder + "/LD_Exit.prefab")
            };
            return assets;
        }

        private static T LoadRequired<T>(string path) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                throw new FileNotFoundException("Required Starter Kit asset is missing", path);
            }
            return asset;
        }

        private static GameObject CreateRoot(string name)
        {
            return new GameObject(name);
        }

        private static GameObject CreateGroup(string name, Transform parent)
        {
            GameObject group = new GameObject(name);
            group.transform.SetParent(parent);
            return group;
        }

        private static GameObject CreateCube(
            string name,
            Vector3 position,
            Vector3 scale,
            Material material,
            Transform parent,
            bool keepCollider = true)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent);
            cube.transform.position = position;
            cube.transform.localScale = scale;
            cube.GetComponent<Renderer>().sharedMaterial = material;

            if (!keepCollider)
            {
                UnityEngine.Object.DestroyImmediate(cube.GetComponent<Collider>());
            }
            else
            {
                GameObjectUtility.SetStaticEditorFlags(
                    cube,
                    StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            }

            return cube;
        }

        private static GameObject InstantiatePrefab(
            GameObject prefab,
            string name,
            Vector3 position,
            Quaternion rotation,
            Transform parent,
            Scene scene)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = name;
            instance.transform.SetParent(parent);
            instance.transform.SetPositionAndRotation(position, rotation);
            return instance;
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

        private static void RenderReviewPreviews(Scene scene)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string previewFolder = Path.Combine(projectRoot, "Temp", "Scene1TutorialPreviews");
            Directory.CreateDirectory(previewFolder);

            const int reviewLayer = 31;
            Dictionary<GameObject, int> originalLayers = new Dictionary<GameObject, int>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                {
                    originalLayers[child.gameObject] = child.gameObject.layer;
                    child.gameObject.layer = reviewLayer;
                }
            }

            GameObject cameraObject = new GameObject("__TEMP_REVIEW_CAMERA__");
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            cameraObject.layer = reviewLayer;
            Camera reviewCamera = cameraObject.AddComponent<Camera>();
            reviewCamera.clearFlags = CameraClearFlags.SolidColor;
            reviewCamera.backgroundColor = new Color(0.16f, 0.19f, 0.23f, 1f);
            reviewCamera.nearClipPlane = 0.1f;
            reviewCamera.farClipPlane = 150f;
            reviewCamera.cullingMask = 1 << reviewLayer;

            try
            {
                reviewCamera.orthographic = true;
                reviewCamera.orthographicSize = 22.5f;
                cameraObject.transform.position = new Vector3(0f, 44f, -2f);
                cameraObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                RenderPreview(reviewCamera, 900, 900, Path.Combine(previewFolder, "TopDown.png"));

                reviewCamera.orthographic = false;
                reviewCamera.fieldOfView = 55f;
                cameraObject.transform.position = new Vector3(12f, 11f, -20f);
                cameraObject.transform.LookAt(new Vector3(0f, 1f, -1f));
                RenderPreview(reviewCamera, 960, 540, Path.Combine(previewFolder, "Arrival.png"));

                cameraObject.transform.position = new Vector3(-16f, 13f, -2f);
                cameraObject.transform.LookAt(new Vector3(0f, 1.5f, 10f));
                RenderPreview(reviewCamera, 960, 540, Path.Combine(previewFolder, "RouteChoice.png"));

                cameraObject.transform.position = new Vector3(0f, 2.8f, -20.8f);
                cameraObject.transform.LookAt(new Vector3(0f, 1f, -10.5f));
                RenderPreview(reviewCamera, 960, 540, Path.Combine(previewFolder, "Gameplay_Start.png"));

                cameraObject.transform.position = new Vector3(0f, 3f, -7.7f);
                cameraObject.transform.LookAt(new Vector3(0f, 1f, -0.5f));
                RenderPreview(reviewCamera, 960, 540, Path.Combine(previewFolder, "Gameplay_Learn.png"));

                cameraObject.transform.position = new Vector3(0f, 3f, 1.2f);
                cameraObject.transform.LookAt(new Vector3(0f, 1f, 10f));
                RenderPreview(reviewCamera, 960, 540, Path.Combine(previewFolder, "Gameplay_Choice.png"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cameraObject);
                foreach (KeyValuePair<GameObject, int> layerEntry in originalLayers)
                {
                    if (layerEntry.Key != null)
                    {
                        layerEntry.Key.layer = layerEntry.Value;
                    }
                }
            }
        }

        private static void RenderPreview(Camera camera, int width, int height, string outputPath)
        {
            RenderTexture renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            RenderTexture previousActive = RenderTexture.active;

            try
            {
                camera.targetTexture = renderTexture;
                camera.Render();
                RenderTexture.active = renderTexture;
                texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                texture.Apply();
                File.WriteAllBytes(outputPath, texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previousActive;
                UnityEngine.Object.DestroyImmediate(texture);
                renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(renderTexture);
            }
        }

        private static void ClearScene(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static Scene GetLoadedScene(string path)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (string.Equals(scene.path, path, StringComparison.OrdinalIgnoreCase))
                {
                    return scene;
                }
            }
            return default;
        }

        private static bool HasRoot(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == name)
                {
                    return true;
                }
            }
            return false;
        }

        private static int CountComponents<T>(Scene scene) where T : Component
        {
            return GetComponentsInScene<T>(scene).Length;
        }

        private static T[] GetComponentsInScene<T>(Scene scene) where T : Component
        {
            List<T> components = new List<T>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                components.AddRange(root.GetComponentsInChildren<T>(true));
            }
            return components.ToArray();
        }

        private static GameObject FindInScene(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
                foreach (Transform candidate in transforms)
                {
                    if (candidate.name == name)
                    {
                        return candidate.gameObject;
                    }
                }
            }
            return null;
        }

        private static void AddSceneToBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (EditorBuildSettingsScene scene in scenes)
            {
                if (string.Equals(scene.path, TargetScenePath, StringComparison.OrdinalIgnoreCase))
                {
                    scene.enabled = true;
                    EditorBuildSettings.scenes = scenes.ToArray();
                    return;
                }
            }

            scenes.Insert(0, new EditorBuildSettingsScene(TargetScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void BackupCurrentSceneFile()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string sourcePath = Path.Combine(projectRoot, TargetScenePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(sourcePath))
            {
                return;
            }

            string backupPath = Path.Combine(projectRoot, "Temp", "Level 1 Tutorial.before-build.unity");
            Directory.CreateDirectory(Path.GetDirectoryName(backupPath));
            File.Copy(sourcePath, backupPath, true);
        }

        private static string GetRequestPath()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.Combine(projectRoot, "Temp", RequestFileName);
        }
    }
}
