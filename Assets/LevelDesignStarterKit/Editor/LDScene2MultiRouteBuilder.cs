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
    /// Builds Level 2 as a route-choice exercise. It never opens, edits, or saves
    /// Level 1 Tutorial. The first half uses one guard across three routes; the
    /// second half uses two guards across two routes that converge at the Core.
    /// </summary>
    public static class LDScene2MultiRouteBuilder
    {
        private const string TargetScenePath = "Assets/LevelDesignStarterKit/Generated/Scenes/Level 2.unity";
        private const string LevelOneScenePath = "Assets/LevelDesignStarterKit/Generated/Scenes/Level 1 Tutorial.unity";
        private const string RequestFileName = "BuildScene2MultiRoute.request";
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
                BuildSceneTwoMultiRoute();
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

        [MenuItem("Tools/Level Design Starter Kit/Build Scene 2 Multi Route", priority = 30)]
        public static void BuildSceneTwoMultiRoute()
        {
            Scene loadedTarget = GetLoadedScene(TargetScenePath);
            if (loadedTarget.IsValid() && loadedTarget.isDirty)
            {
                throw new InvalidOperationException(
                    "Level 2 has unsaved changes. Save or revert them before rebuilding so no work is lost.");
            }

            Scene previousActive = SceneManager.GetActiveScene();
            bool targetWasLoaded = loadedTarget.IsValid();
            Scene levelTwoScene = targetWasLoaded
                ? loadedTarget
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

            BackupCurrentSceneFile();

            try
            {
                SceneManager.SetActiveScene(levelTwoScene);
                ClearScene(levelTwoScene);

                SceneAssets assets = LoadAssets();
                BuildSceneContents(levelTwoScene, assets);
                RenderReviewPreviews(levelTwoScene);

                EditorSceneManager.MarkSceneDirty(levelTwoScene);
                if (!EditorSceneManager.SaveScene(levelTwoScene, TargetScenePath))
                {
                    throw new IOException("Unity could not save " + TargetScenePath);
                }

                AddSceneToBuildSettings();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                bool valid = ValidateScene(levelTwoScene, out string validationSummary);
                if (!valid)
                {
                    throw new InvalidOperationException("Scene 2 structural validation failed: " + validationSummary);
                }

                SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(TargetScenePath);
                Selection.activeObject = sceneAsset;
                Debug.Log("SCENE 2 MULTI ROUTE BUILD COMPLETE — " + validationSummary + " — " + TargetScenePath);
            }
            finally
            {
                if (!targetWasLoaded && levelTwoScene.IsValid() && levelTwoScene.isLoaded)
                {
                    EditorSceneManager.CloseScene(levelTwoScene, true);
                }

                if (previousActive.IsValid() && previousActive.isLoaded)
                {
                    SceneManager.SetActiveScene(previousActive);
                }
            }
        }

        [MenuItem("Tools/Level Design Starter Kit/Validate Scene 2 Multi Route", priority = 31)]
        public static void ValidateSceneTwoMultiRoute()
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
                    Debug.Log("SCENE 2 MULTI ROUTE VALIDATION PASS — " + summary);
                }
                else
                {
                    Debug.LogError("SCENE 2 MULTI ROUTE VALIDATION FAIL — " + summary);
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
            GameObject systemsRoot = new GameObject("--- SYSTEMS ---");
            GameObject gameplayRoot = new GameObject("--- GAMEPLAY ---");
            GameObject grayboxRoot = new GameObject("--- GRAYBOX ---");

            CreateLighting(systemsRoot.transform);

            GameObject player = InstantiatePrefab(
                assets.Player,
                "LD_Player",
                new Vector3(0f, 0f, -14.2f),
                Quaternion.identity,
                gameplayRoot.transform,
                scene);
            LDPlayerMotor playerMotor = player.GetComponent<LDPlayerMotor>();

            GameObject cameraObject = new GameObject("LD_ThirdPersonCamera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(systemsRoot.transform);
            cameraObject.transform.position = new Vector3(0f, 3.5f, -19.7f);
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

            GameObject shell = CreateGroup("LEVEL_SHELL", grayboxRoot.transform);
            GameObject arrival = CreateGroup("BEAT_01_ARRIVAL_READ_ROUTES", grayboxRoot.transform);
            GameObject firstHalf = CreateGroup("HALF_01_THREE_ROUTE_EXPERIMENT", grayboxRoot.transform);
            GameObject midpoint = CreateGroup("BEAT_03_MIDPOINT_CHECKPOINT", grayboxRoot.transform);
            GameObject secondHalf = CreateGroup("HALF_02_TWO_GUARD_ROUTE_OVERLAP", grayboxRoot.transform);
            GameObject goal = CreateGroup("BEAT_05_CORE_EXIT", grayboxRoot.transform);

            BuildShell(shell.transform, assets);
            BuildArrival(arrival.transform, assets);
            BuildFirstHalfRoutes(firstHalf.transform, assets);
            BuildMidpoint(midpoint.transform, assets);
            BuildSecondHalfRoutes(secondHalf.transform, assets);
            BuildGoal(goal.transform, assets);

            InstantiatePrefab(
                assets.Checkpoint,
                "LD_Checkpoint_Midpoint",
                new Vector3(0f, 0f, 5.2f),
                Quaternion.identity,
                gameplayRoot.transform,
                scene);

            InstantiatePrefab(
                assets.EnergyCore,
                "LD_EnergyCore",
                new Vector3(0f, 0f, 15.2f),
                Quaternion.identity,
                gameplayRoot.transform,
                scene);

            InstantiatePrefab(
                assets.Exit,
                "LD_Exit",
                new Vector3(0f, 0f, 17.6f),
                Quaternion.identity,
                gameplayRoot.transform,
                scene);

            CreateGuard(
                "LD_Guard_01_FirstHalf",
                "PatrolPath_01_FirstHalf",
                new Vector3(-8f, 0f, -5f),
                new[] { new Vector3(-8f, 0f, -5f), new Vector3(8f, 0f, -5f) },
                player.transform,
                gameplayRoot.transform,
                assets.Guard,
                scene);

            CreateGuard(
                "LD_Guard_02_SecondHalfEntry",
                "PatrolPath_02_SecondHalfEntry",
                new Vector3(-8f, 0f, 8.3f),
                new[] { new Vector3(-8f, 0f, 8.3f), new Vector3(3f, 0f, 8.3f) },
                player.transform,
                gameplayRoot.transform,
                assets.Guard,
                scene);

            CreateGuard(
                "LD_Guard_03_SecondHalfCore",
                "PatrolPath_03_SecondHalfCore",
                new Vector3(-2f, 0f, 13.8f),
                new[] { new Vector3(-2f, 0f, 13.8f), new Vector3(9f, 0f, 13.8f) },
                player.transform,
                gameplayRoot.transform,
                assets.Guard,
                scene);
        }

        private static void BuildShell(Transform parent, SceneAssets assets)
        {
            CreateCube("Ground", new Vector3(0f, -0.25f, -0.5f), new Vector3(30f, 0.5f, 39f), assets.DarkGray, parent);
            CreateCube("Boundary_Left", new Vector3(-15f, 1.5f, -0.5f), new Vector3(0.6f, 3f, 39f), assets.Gray, parent);
            CreateCube("Boundary_Right", new Vector3(15f, 1.5f, -0.5f), new Vector3(0.6f, 3f, 39f), assets.Gray, parent);
            CreateCube("Boundary_Back", new Vector3(0f, 1.5f, -20f), new Vector3(30f, 3f, 0.6f), assets.Gray, parent);
            CreateCube("Boundary_Front_Left", new Vector3(-8.5f, 1.5f, 19f), new Vector3(13f, 3f, 0.6f), assets.Gray, parent);
            CreateCube("Boundary_Front_Right", new Vector3(8.5f, 1.5f, 19f), new Vector3(13f, 3f, 0.6f), assets.Gray, parent);
        }

        private static void BuildArrival(Transform parent, SceneAssets assets)
        {
            CreateCube("ArrivalFrame_Left", new Vector3(-5f, 1.5f, -13f), new Vector3(0.7f, 3f, 7f), assets.Gray, parent);
            CreateCube("ArrivalFrame_Right", new Vector3(5f, 1.5f, -13f), new Vector3(0.7f, 3f, 7f), assets.Gray, parent);
            CreateCube("SafeObservationCover", new Vector3(0f, 1.1f, -10f), new Vector3(4f, 2.2f, 0.8f), assets.Gray, parent);
        }

        private static void BuildFirstHalfRoutes(Transform parent, SceneAssets assets)
        {
            GameObject mainRoute = CreateGroup("ROUTE_A_MAIN_LOWER_SAFE_LONG", parent);
            CreateCube("MainRoute_GuideWall", new Vector3(-2.7f, 1.5f, -3f), new Vector3(0.8f, 3f, 10f), assets.Gray, mainRoute.transform);
            CreateCube("MainRouteCover_01", new Vector3(-6f, 1.2f, -7.5f), new Vector3(2.2f, 2.4f, 1f), assets.Gray, mainRoute.transform);
            CreateCube("MainRouteCover_02", new Vector3(-9f, 1.2f, -3.2f), new Vector3(1.2f, 2.4f, 3f), assets.Gray, mainRoute.transform);
            CreateCube("MainRouteCover_03", new Vector3(-6.5f, 1.2f, 1f), new Vector3(2.5f, 2.4f, 1f), assets.Gray, mainRoute.transform);
            CreateCube("MainRouteCover_04", new Vector3(-4f, 1.2f, 3.6f), new Vector3(1.2f, 2.4f, 2.4f), assets.Gray, mainRoute.transform);

            GameObject directRoute = CreateGroup("ROUTE_B_CENTER_FAST_RISKY", parent);
            CreateCube("DirectRouteCover_Only", new Vector3(0.7f, 1.1f, -1f), new Vector3(1.4f, 2.2f, 1.4f), assets.Gray, directRoute.transform);

            GameObject upperRoute = CreateGroup("ROUTE_C_UPPER_LANDMARK_INFORMATION", parent);
            GameObject rampUp = CreateCube(
                "UpperRoute_RampUp_EXPOSED_ENTRY",
                new Vector3(8f, 1.45f, -6.5f),
                new Vector3(3f, 0.5f, 7.5f),
                assets.Gray,
                upperRoute.transform);
            rampUp.transform.rotation = Quaternion.Euler(-22f, 0f, 0f);

            CreateCube("UpperRoute_LookoutPlatform", new Vector3(6f, 3f, -2.5f), new Vector3(5f, 0.45f, 3f), assets.Gray, upperRoute.transform);
            CreateCube("UpperRoute_NarrowBridge", new Vector3(2.5f, 3f, -1.8f), new Vector3(7f, 0.45f, 1.8f), assets.Gray, upperRoute.transform);

            GameObject landmark = CreateGroup("UPPER_LANDMARK_SMALL_LOOKOUT_INFORMATION_REWARD", upperRoute.transform);
            CreateCube("Landmark_LeftPillar", new Vector3(6.8f, 4.3f, -2.5f), new Vector3(0.35f, 2.6f, 0.35f), assets.White, landmark.transform, false);
            CreateCube("Landmark_RightPillar", new Vector3(8.2f, 4.3f, -2.5f), new Vector3(0.35f, 2.6f, 0.35f), assets.White, landmark.transform, false);
            CreateCube("Landmark_Top", new Vector3(7.5f, 5.5f, -2.5f), new Vector3(1.75f, 0.3f, 0.35f), assets.White, landmark.transform, false);

            GameObject rampDown = CreateCube(
                "UpperRoute_RampDown_TO_CHECKPOINT",
                new Vector3(0f, 1.45f, 1.35f),
                new Vector3(3f, 0.5f, 6.7f),
                assets.Gray,
                upperRoute.transform);
            rampDown.transform.rotation = Quaternion.Euler(24f, 0f, 0f);
        }

        private static void BuildMidpoint(Transform parent, SceneAssets assets)
        {
            CreateCube("MidpointSafeCover_Left", new Vector3(-5f, 1.1f, 4.6f), new Vector3(5f, 2.2f, 0.7f), assets.Gray, parent);
            CreateCube("MidpointSafeCover_Right", new Vector3(5f, 1.1f, 4.6f), new Vector3(5f, 2.2f, 0.7f), assets.Gray, parent);
        }

        private static void BuildSecondHalfRoutes(Transform parent, SceneAssets assets)
        {
            CreateCube("SecondHalfDivider", new Vector3(0f, 1.5f, 10.7f), new Vector3(1f, 3f, 6f), assets.Gray, parent);

            GameObject safeRoute = CreateGroup("SECOND_HALF_ROUTE_LEFT_SAFE_LONG", parent);
            CreateCube("SecondSafeCover_01", new Vector3(-4.5f, 1.2f, 7.2f), new Vector3(2.3f, 2.4f, 1f), assets.Gray, safeRoute.transform);
            CreateCube("SecondSafeCover_02", new Vector3(-8f, 1.2f, 10.3f), new Vector3(1.2f, 2.4f, 3f), assets.Gray, safeRoute.transform);
            CreateCube("SecondSafeCover_03", new Vector3(-5.5f, 1.2f, 13.7f), new Vector3(2.5f, 2.4f, 1f), assets.Gray, safeRoute.transform);

            GameObject fastRoute = CreateGroup("SECOND_HALF_ROUTE_RIGHT_FAST_OVERLAP", parent);
            CreateCube("SecondFastCover_01", new Vector3(4.5f, 1.2f, 9f), new Vector3(1.5f, 2.4f, 1f), assets.Gray, fastRoute.transform);
            CreateCube("SecondFastCover_02", new Vector3(9f, 1.2f, 11.8f), new Vector3(1.2f, 2.4f, 2.2f), assets.Gray, fastRoute.transform);
            CreateCube("SecondFastCover_03", new Vector3(5.5f, 1.2f, 15.2f), new Vector3(1.5f, 2.4f, 1f), assets.Gray, fastRoute.transform);
        }

        private static void BuildGoal(Transform parent, SceneAssets assets)
        {
            CreateCube("CoreCover_Left", new Vector3(-3.2f, 1.1f, 15.5f), new Vector3(1.2f, 2.2f, 1.8f), assets.Gray, parent);
            CreateCube("CoreCover_Right", new Vector3(3.2f, 1.1f, 15.5f), new Vector3(1.2f, 2.2f, 1.8f), assets.Gray, parent);

            GameObject exitLandmark = CreateGroup("EXIT_LANDMARK", parent);
            CreateCube("ExitBeacon_Left", new Vector3(-2.1f, 3.8f, 18f), new Vector3(0.4f, 7.6f, 0.4f), assets.Green, exitLandmark.transform, false);
            CreateCube("ExitBeacon_Right", new Vector3(2.1f, 3.8f, 18f), new Vector3(0.4f, 7.6f, 0.4f), assets.Green, exitLandmark.transform, false);
            CreateCube("ExitBeacon_Top", new Vector3(0f, 7.4f, 18f), new Vector3(4.6f, 0.4f, 0.4f), assets.Green, exitLandmark.transform, false);
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
            Check(CountComponents<LDThirdPersonCamera>(scene) == 1, "expected exactly 1 camera", failures, ref passed);
            Check(CountComponents<LDGameSession>(scene) == 1, "expected exactly 1 session", failures, ref passed);
            Check(CountComponents<LDCheckpoint>(scene) == 1, "expected exactly 1 midpoint checkpoint", failures, ref passed);
            Check(CountComponents<LDKeyCollectible>(scene) == 1, "expected exactly 1 Energy Core", failures, ref passed);
            Check(CountComponents<LDExitGoal>(scene) == 1, "expected exactly 1 Exit", failures, ref passed);
            Check(CountComponents<LDEnemyGuard>(scene) == 3, "expected exactly 3 guards", failures, ref passed);

            LDWaypointPath[] paths = GetComponentsInScene<LDWaypointPath>(scene);
            bool pathsValid = paths.Length == 3;
            foreach (LDWaypointPath path in paths)
            {
                pathsValid &= path.Count >= 2;
            }
            Check(pathsValid, "expected 3 valid patrol paths", failures, ref passed);

            Check(FindInScene(scene, "LD_Guard_01_FirstHalf") != null, "missing first-half guard", failures, ref passed);
            Check(FindInScene(scene, "LD_Guard_02_SecondHalfEntry") != null && FindInScene(scene, "LD_Guard_03_SecondHalfCore") != null, "missing two second-half guards", failures, ref passed);
            Check(FindInScene(scene, "ROUTE_A_MAIN_LOWER_SAFE_LONG") != null, "missing safe main route", failures, ref passed);
            Check(FindInScene(scene, "ROUTE_B_CENTER_FAST_RISKY") != null, "missing direct risky route", failures, ref passed);
            Check(FindInScene(scene, "ROUTE_C_UPPER_LANDMARK_INFORMATION") != null, "missing upper route", failures, ref passed);
            Check(FindInScene(scene, "UpperRoute_RampUp_EXPOSED_ENTRY") != null && FindInScene(scene, "UpperRoute_RampDown_TO_CHECKPOINT") != null, "upper route does not reconnect", failures, ref passed);
            Check(FindInScene(scene, "UPPER_LANDMARK_SMALL_LOOKOUT_INFORMATION_REWARD") != null, "missing upper landmark reward", failures, ref passed);
            Check(FindInScene(scene, "SECOND_HALF_ROUTE_LEFT_SAFE_LONG") != null, "missing second-half safe route", failures, ref passed);
            Check(FindInScene(scene, "SECOND_HALF_ROUTE_RIGHT_FAST_OVERLAP") != null, "missing second-half risky route", failures, ref passed);
            Check(FindInScene(scene, "EXIT_LANDMARK") != null, "missing Exit landmark", failures, ref passed);

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
            return new SceneAssets
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
            string previewFolder = Path.Combine(projectRoot, "Temp", "Scene2MultiRoutePreviews");
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
                reviewCamera.orthographicSize = 21.5f;
                cameraObject.transform.position = new Vector3(0f, 45f, 0f);
                cameraObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                RenderPreview(reviewCamera, 900, 900, Path.Combine(previewFolder, "TopDown.png"));

                reviewCamera.orthographic = false;
                reviewCamera.fieldOfView = 55f;
                cameraObject.transform.position = new Vector3(0f, 3f, -19.5f);
                cameraObject.transform.LookAt(new Vector3(0f, 1f, -7f));
                RenderPreview(reviewCamera, 960, 540, Path.Combine(previewFolder, "Gameplay_Start.png"));

                cameraObject.transform.position = new Vector3(13f, 9f, -13f);
                cameraObject.transform.LookAt(new Vector3(2f, 2f, -1f));
                RenderPreview(reviewCamera, 960, 540, Path.Combine(previewFolder, "FirstHalfRoutes.png"));

                cameraObject.transform.position = new Vector3(11f, 7f, -6f);
                cameraObject.transform.LookAt(new Vector3(4f, 2f, 5f));
                RenderPreview(reviewCamera, 960, 540, Path.Combine(previewFolder, "UpperLandmark.png"));

                cameraObject.transform.position = new Vector3(-13f, 9f, 3f);
                cameraObject.transform.LookAt(new Vector3(0f, 1.5f, 13f));
                RenderPreview(reviewCamera, 960, 540, Path.Combine(previewFolder, "SecondHalfRoutes.png"));
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
                foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
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
            for (int i = 0; i < scenes.Count; i++)
            {
                if (string.Equals(scenes[i].path, TargetScenePath, StringComparison.OrdinalIgnoreCase))
                {
                    scenes[i].enabled = true;
                    EditorBuildSettings.scenes = scenes.ToArray();
                    return;
                }
            }

            int insertionIndex = scenes.Count;
            for (int i = 0; i < scenes.Count; i++)
            {
                if (string.Equals(scenes[i].path, LevelOneScenePath, StringComparison.OrdinalIgnoreCase))
                {
                    insertionIndex = i + 1;
                    break;
                }
            }

            scenes.Insert(insertionIndex, new EditorBuildSettingsScene(TargetScenePath, true));
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

            string backupPath = Path.Combine(projectRoot, "Temp", "Level 2.before-build.unity");
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
