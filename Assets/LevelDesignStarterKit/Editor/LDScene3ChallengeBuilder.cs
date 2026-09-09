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
    /// Builds Level 3 as the culminating challenge. It recombines movement,
    /// sightline breaking, route choice, patrol timing, and a jump shortcut.
    /// Guard view-cone visuals are inherited from the shared LD_Guard prefab.
    /// It never opens, edits, or saves Level 1 or Level 2.
    /// </summary>
    public static class LDScene3ChallengeBuilder
    {
        private const string TargetScenePath = "Assets/LevelDesignStarterKit/Generated/Scenes/Level 3.unity";
        private const string LevelTwoScenePath = "Assets/LevelDesignStarterKit/Generated/Scenes/Level 2.unity";
        private const string RequestFileName = "BuildScene3Challenge.request";
        private const string MaterialsFolder = "Assets/LevelDesignStarterKit/Generated/Materials";
        private const string PrefabsFolder = "Assets/LevelDesignStarterKit/Generated/Prefabs";

        private sealed class SceneAssets
        {
            public Material Gray;
            public Material DarkGray;
            public Material Green;
            public Material Yellow;
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
                BuildSceneThreeChallenge();
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

        [MenuItem("Tools/Level Design Starter Kit/Build Scene 3 Challenge", priority = 40)]
        public static void BuildSceneThreeChallenge()
        {
            Scene loadedTarget = GetLoadedScene(TargetScenePath);
            if (loadedTarget.IsValid() && loadedTarget.isDirty)
            {
                throw new InvalidOperationException(
                    "Level 3 has unsaved changes. Save or revert them before rebuilding so no work is lost.");
            }

            Scene previousActive = SceneManager.GetActiveScene();
            bool targetWasLoaded = loadedTarget.IsValid();
            Scene challengeScene = targetWasLoaded
                ? loadedTarget
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

            BackupCurrentSceneFile();

            try
            {
                SceneManager.SetActiveScene(challengeScene);
                ClearScene(challengeScene);

                SceneAssets assets = LoadAssets();
                BuildSceneContents(challengeScene, assets);
                RenderReviewPreviews(challengeScene);

                EditorSceneManager.MarkSceneDirty(challengeScene);
                if (!EditorSceneManager.SaveScene(challengeScene, TargetScenePath))
                {
                    throw new IOException("Unity could not save " + TargetScenePath);
                }

                AddSceneToBuildSettings();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                bool valid = ValidateScene(challengeScene, out string validationSummary);
                if (!valid)
                {
                    throw new InvalidOperationException("Scene 3 structural validation failed: " + validationSummary);
                }

                SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(TargetScenePath);
                Selection.activeObject = sceneAsset;
                Debug.Log("SCENE 3 CHALLENGE BUILD COMPLETE — " + validationSummary + " — " + TargetScenePath);
            }
            finally
            {
                if (!targetWasLoaded && challengeScene.IsValid() && challengeScene.isLoaded)
                {
                    EditorSceneManager.CloseScene(challengeScene, true);
                }

                if (previousActive.IsValid() && previousActive.isLoaded)
                {
                    SceneManager.SetActiveScene(previousActive);
                }
            }
        }

        [MenuItem("Tools/Level Design Starter Kit/Validate Scene 3 Challenge", priority = 41)]
        public static void ValidateSceneThreeChallenge()
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
                    Debug.Log("SCENE 3 CHALLENGE VALIDATION PASS — " + summary);
                }
                else
                {
                    Debug.LogError("SCENE 3 CHALLENGE VALIDATION FAIL — " + summary);
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
                new Vector3(0f, 0f, -17f),
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
            GameObject arrival = CreateGroup("BEAT_01_ARRIVAL_RECON", grayboxRoot.transform);
            GameObject entry = CreateGroup("BEAT_02_ENTRY_ROUTE_CHOICE", grayboxRoot.transform);
            GameObject checkpoint = CreateGroup("BEAT_03_CHECKPOINT_SAFE_OBSERVATION", grayboxRoot.transform);
            GameObject crossfire = CreateGroup("BEAT_04_MULTILEVEL_CROSSFIRE", grayboxRoot.transform);
            GameObject goal = CreateGroup("BEAT_05_UPPER_RING_CORE_EXIT", grayboxRoot.transform);

            BuildShell(shell.transform, assets);
            BuildArrival(arrival.transform, assets);
            BuildEntryChoice(entry.transform, assets);
            BuildCheckpointObservation(checkpoint.transform, assets);
            BuildCrossfireChallenge(crossfire.transform, assets);
            BuildGoal(goal.transform, assets);

            InstantiatePrefab(
                assets.Checkpoint,
                "LD_Checkpoint_BeforeCrossfire",
                new Vector3(0f, 0f, 0f),
                Quaternion.identity,
                gameplayRoot.transform,
                scene);

            InstantiatePrefab(
                assets.EnergyCore,
                "LD_EnergyCore",
                new Vector3(0f, 3f, 15.3f),
                Quaternion.identity,
                gameplayRoot.transform,
                scene);

            InstantiatePrefab(
                assets.Exit,
                "LD_Exit",
                new Vector3(0f, 3f, 18.05f),
                Quaternion.identity,
                gameplayRoot.transform,
                scene);

            CreateGuard(
                "LD_Guard_01_Entry",
                "PatrolPath_01_Entry",
                new Vector3(-7f, 0f, -7.5f),
                new[] { new Vector3(-7f, 0f, -7.5f), new Vector3(7f, 0f, -7.5f) },
                player.transform,
                gameplayRoot.transform,
                assets.Guard,
                scene);

            CreateGuard(
                "LD_Guard_02_CrossfireHorizontal",
                "PatrolPath_02_CrossfireHorizontal",
                new Vector3(-7.5f, 0f, 4.8f),
                new[] { new Vector3(-7.5f, 0f, 4.8f), new Vector3(1.5f, 0f, 4.8f) },
                player.transform,
                gameplayRoot.transform,
                assets.Guard,
                scene);

            CreateGuard(
                "LD_Guard_03_CrossfireVertical",
                "PatrolPath_03_CrossfireVertical",
                new Vector3(4.8f, 0f, 2.2f),
                new[] { new Vector3(4.8f, 0f, 2.2f), new Vector3(4.8f, 0f, 9.5f) },
                player.transform,
                gameplayRoot.transform,
                assets.Guard,
                scene);

            CreateGuard(
                "LD_Guard_04_UpperClockwiseLoop",
                "PatrolPath_04_UpperClockwiseLoop",
                new Vector3(-4.6f, 3f, 12.7f),
                new[]
                {
                    new Vector3(-4.6f, 3f, 12.7f),
                    new Vector3(-4.6f, 3f, 17.5f),
                    new Vector3(4.6f, 3f, 17.5f),
                    new Vector3(4.6f, 3f, 12.7f)
                },
                player.transform,
                gameplayRoot.transform,
                assets.Guard,
                scene,
                true);
        }

        private static void BuildShell(Transform parent, SceneAssets assets)
        {
            CreateCube("Ground", new Vector3(0f, -0.25f, -0.5f), new Vector3(30f, 0.5f, 39f), assets.DarkGray, parent);
            CreateCube("Boundary_Left", new Vector3(-15f, 3f, -0.5f), new Vector3(0.6f, 6f, 39f), assets.Gray, parent);
            CreateCube("Boundary_Right", new Vector3(15f, 3f, -0.5f), new Vector3(0.6f, 6f, 39f), assets.Gray, parent);
            CreateCube("Boundary_Back", new Vector3(0f, 3f, -20f), new Vector3(30f, 6f, 0.6f), assets.Gray, parent);
            CreateCube("Boundary_Front_Left", new Vector3(-8.5f, 3f, 19f), new Vector3(13f, 6f, 0.6f), assets.Gray, parent);
            CreateCube("Boundary_Front_Right", new Vector3(8.5f, 3f, 19f), new Vector3(13f, 6f, 0.6f), assets.Gray, parent);
        }

        private static void BuildArrival(Transform parent, SceneAssets assets)
        {
            CreateCube("ArrivalGuide_Left", new Vector3(-6f, 1.5f, -14.5f), new Vector3(0.7f, 3f, 8f), assets.Gray, parent);
            CreateCube("ArrivalGuide_Right", new Vector3(6f, 1.5f, -14.5f), new Vector3(0.7f, 3f, 8f), assets.Gray, parent);
            CreateCube("ArrivalReconCover", new Vector3(0f, 1.1f, -11f), new Vector3(4.5f, 2.2f, 0.8f), assets.Gray, parent);
        }

        private static void BuildEntryChoice(Transform parent, SceneAssets assets)
        {
            GameObject safeRoute = CreateGroup("ENTRY_ROUTE_MAIN_LEFT_SAFE_LONG", parent);
            CreateCube("EntrySafeCover_01", new Vector3(-5.2f, 1.2f, -9.7f), new Vector3(2.2f, 2.4f, 1f), assets.Gray, safeRoute.transform);
            CreateCube("EntrySafeCover_02", new Vector3(-9f, 1.2f, -6f), new Vector3(1.2f, 2.4f, 3f), assets.Gray, safeRoute.transform);
            CreateCube("EntrySafeCover_03", new Vector3(-6.5f, 1.2f, -2f), new Vector3(2.4f, 2.4f, 1f), assets.Gray, safeRoute.transform);

            GameObject directRoute = CreateGroup("ENTRY_ROUTE_OPTIONAL_RIGHT_FAST_RISKY", parent);
            CreateCube("EntryDirectCover_01", new Vector3(5.2f, 1.1f, -6f), new Vector3(1.4f, 2.2f, 1.4f), assets.Gray, directRoute.transform);
            CreateCube("EntryDirectCover_02", new Vector3(3.2f, 1.1f, -2.1f), new Vector3(1.4f, 2.2f, 1.4f), assets.Gray, directRoute.transform);
        }

        private static void BuildCheckpointObservation(Transform parent, SceneAssets assets)
        {
            CreateCube("SAFE_OBSERVATION_BEFORE_CROSSFIRE_Left", new Vector3(-5f, 1.1f, 1.25f), new Vector3(5f, 2.2f, 0.7f), assets.Gray, parent);
            CreateCube("SAFE_OBSERVATION_BEFORE_CROSSFIRE_Right", new Vector3(5f, 1.1f, 1.25f), new Vector3(5f, 2.2f, 0.7f), assets.Gray, parent);
            CreateCube("ObservationUpperRouteSilhouette", new Vector3(0f, 3.35f, 8.8f), new Vector3(4f, 0.25f, 0.25f), assets.Yellow, parent, false);
        }

        private static void BuildCrossfireChallenge(Transform parent, SceneAssets assets)
        {
            GameObject safeRoute = CreateGroup("LOWER_ROUTE_A_LEFT_SAFE_LONG", parent);
            CreateCube("LeftSafeCover_01", new Vector3(-4.5f, 1.2f, 2.8f), new Vector3(2.2f, 2.4f, 1f), assets.Gray, safeRoute.transform);
            CreateCube("LeftSafeCover_02", new Vector3(-9f, 1.2f, 5.2f), new Vector3(1.2f, 2.4f, 3f), assets.Gray, safeRoute.transform);
            CreateCube("LeftSafeCover_03", new Vector3(-10.5f, 1.2f, 8.5f), new Vector3(2f, 2.4f, 1f), assets.Gray, safeRoute.transform);
            GameObject leftRamp = CreateCube("LeftSafe_RampToUpper", new Vector3(-6.2f, 1.3f, 8f), new Vector3(10f, 0.5f, 2.4f), assets.Gray, safeRoute.transform);
            leftRamp.transform.rotation = Quaternion.Euler(0f, 0f, 17f);
            CreateCube("LeftUpperConnector", new Vector3(-1.4f, 2.75f, 8f), new Vector3(3.2f, 0.5f, 2.4f), assets.Gray, safeRoute.transform);

            GameObject sprintRoute = CreateGroup("MID_ROUTE_B_RIGHT_FAST_SPRINT_JUMP", parent);
            CreateCube("RightRiskCover_Only", new Vector3(7f, 1.1f, 2.4f), new Vector3(1.4f, 2.2f, 1.4f), assets.Gray, sprintRoute.transform);
            GameObject rightRamp = CreateCube("RightFast_RampToMid", new Vector3(9f, 0.65f, 4.5f), new Vector3(2.6f, 0.5f, 6f), assets.Gray, sprintRoute.transform);
            rightRamp.transform.rotation = Quaternion.Euler(-14f, 0f, 0f);
            CreateCube("RightMid_Runway", new Vector3(8.8f, 1.25f, 7.4f), new Vector3(2.6f, 0.5f, 2.2f), assets.Gray, sprintRoute.transform);
            CreateCube("RightMid_SprintLanding", new Vector3(4f, 1.25f, 7.4f), new Vector3(1.4f, 0.5f, 2.2f), assets.Gray, sprintRoute.transform);
            GameObject upperRamp = CreateCube("RightMid_RampToUpper", new Vector3(0.45f, 2.05f, 7.4f), new Vector3(6.3f, 0.5f, 2.2f), assets.Gray, sprintRoute.transform);
            upperRamp.transform.rotation = Quaternion.Euler(0f, 0f, -14f);
            CreateCube("RightRoute_InformationMarker", new Vector3(8.8f, 3f, 7.4f), new Vector3(0.3f, 3.2f, 0.3f), assets.Yellow, sprintRoute.transform, false);

            GameObject finalJump = CreateGroup("FINAL_SPRINT_JUMP_REQUIRED_3M", parent);
            CreateCube("FinalSprintJump_Runway", new Vector3(0f, 2.75f, 8.2f), new Vector3(5f, 0.5f, 2f), assets.Gray, finalJump.transform);
            CreateCube("FinalSprintJump_EdgeMarker", new Vector3(0f, 3.25f, 9.15f), new Vector3(4.5f, 0.18f, 0.18f), assets.Yellow, finalJump.transform, false);

            GameObject upperRing = CreateGroup("UPPER_ROUTE_CLOCKWISE_RING", parent);
            CreateCube("UpperRing_SouthLanding", new Vector3(0f, 2.75f, 12.9f), new Vector3(11f, 0.5f, 1.4f), assets.Gray, upperRing.transform);
            CreateCube("UpperRing_West", new Vector3(-4.8f, 2.75f, 15.25f), new Vector3(1.4f, 0.5f, 6.1f), assets.Gray, upperRing.transform);
            CreateCube("UpperRing_North", new Vector3(0f, 2.75f, 17.6f), new Vector3(11f, 0.5f, 1.4f), assets.Gray, upperRing.transform);
            CreateCube("UpperRing_East", new Vector3(4.8f, 2.75f, 15.25f), new Vector3(1.4f, 0.5f, 6.1f), assets.Gray, upperRing.transform);
            CreateCube("UpperCoreIsland", new Vector3(0f, 2.75f, 15.3f), new Vector3(3.2f, 0.5f, 3f), assets.Gray, upperRing.transform);
            CreateCube("UpperCoreBridge_NorthOnly", new Vector3(0f, 2.75f, 16.85f), new Vector3(1.5f, 0.5f, 0.5f), assets.Gray, upperRing.transform);

            CreateCube("UpperCover_SW", new Vector3(-3.4f, 4f, 13.9f), new Vector3(0.45f, 2f, 1.2f), assets.Gray, upperRing.transform);
            CreateCube("UpperCover_NW", new Vector3(-3.4f, 4f, 16.4f), new Vector3(0.45f, 2f, 1.2f), assets.Gray, upperRing.transform);
            CreateCube("UpperCover_NE", new Vector3(3.4f, 4f, 16.4f), new Vector3(0.45f, 2f, 1.2f), assets.Gray, upperRing.transform);
            CreateCube("UpperCover_SE", new Vector3(3.4f, 4f, 13.9f), new Vector3(0.45f, 2f, 1.2f), assets.Gray, upperRing.transform);

            CreateCube("UpperSupport_SW", new Vector3(-4.8f, 1.5f, 12.9f), new Vector3(1f, 3f, 1f), assets.DarkGray, upperRing.transform);
            CreateCube("UpperSupport_NW", new Vector3(-4.8f, 1.5f, 17.6f), new Vector3(1f, 3f, 1f), assets.DarkGray, upperRing.transform);
            CreateCube("UpperSupport_NE", new Vector3(4.8f, 1.5f, 17.6f), new Vector3(1f, 3f, 1f), assets.DarkGray, upperRing.transform);
            CreateCube("UpperSupport_SE", new Vector3(4.8f, 1.5f, 12.9f), new Vector3(1f, 3f, 1f), assets.DarkGray, upperRing.transform);

            GameObject rails = CreateGroup("UPPER_RING_GUARD_RAILS", upperRing.transform);
            CreateCube("Rail_SouthOuter_Left", new Vector3(-3.8f, 3.5f, 12.15f), new Vector3(3.4f, 1f, 0.18f), assets.Gray, rails.transform);
            CreateCube("Rail_SouthOuter_Right", new Vector3(3.8f, 3.5f, 12.15f), new Vector3(3.4f, 1f, 0.18f), assets.Gray, rails.transform);
            CreateCube("Rail_SouthInner", new Vector3(0f, 3.5f, 13.65f), new Vector3(8.1f, 1f, 0.18f), assets.Gray, rails.transform);
            CreateCube("Rail_WestOuter", new Vector3(-5.55f, 3.5f, 15.25f), new Vector3(0.18f, 1f, 6.1f), assets.Gray, rails.transform);
            CreateCube("Rail_WestInner", new Vector3(-4.05f, 3.5f, 15.25f), new Vector3(0.18f, 1f, 3.2f), assets.Gray, rails.transform);
            CreateCube("Rail_NorthOuter_Left", new Vector3(-3.8f, 3.5f, 18.35f), new Vector3(3.4f, 1f, 0.18f), assets.Gray, rails.transform);
            CreateCube("Rail_NorthOuter_Right", new Vector3(3.8f, 3.5f, 18.35f), new Vector3(3.4f, 1f, 0.18f), assets.Gray, rails.transform);
            CreateCube("Rail_NorthInner_Left", new Vector3(-2.525f, 3.5f, 16.85f), new Vector3(3.05f, 1f, 0.18f), assets.Gray, rails.transform);
            CreateCube("Rail_NorthInner_Right", new Vector3(2.525f, 3.5f, 16.85f), new Vector3(3.05f, 1f, 0.18f), assets.Gray, rails.transform);
            CreateCube("Rail_EastOuter", new Vector3(5.55f, 3.5f, 15.25f), new Vector3(0.18f, 1f, 6.1f), assets.Gray, rails.transform);
            CreateCube("Rail_EastInner", new Vector3(4.05f, 3.5f, 15.25f), new Vector3(0.18f, 1f, 3.2f), assets.Gray, rails.transform);
        }

        private static void BuildGoal(Transform parent, SceneAssets assets)
        {
            CreateCube("CoreCover_Left", new Vector3(-1.25f, 4.05f, 15.3f), new Vector3(0.45f, 2.1f, 1.2f), assets.Gray, parent);
            CreateCube("CoreCover_Right", new Vector3(1.25f, 4.05f, 15.3f), new Vector3(0.45f, 2.1f, 1.2f), assets.Gray, parent);

            GameObject coreLandmark = CreateGroup("CORE_LANDMARK_YELLOW_TOWER", parent);
            CreateCube("CoreBeacon_Left", new Vector3(-1.45f, 6f, 15.3f), new Vector3(0.3f, 6f, 0.3f), assets.Yellow, coreLandmark.transform, false);
            CreateCube("CoreBeacon_Right", new Vector3(1.45f, 6f, 15.3f), new Vector3(0.3f, 6f, 0.3f), assets.Yellow, coreLandmark.transform, false);
            CreateCube("CoreBeacon_Top", new Vector3(0f, 9f, 15.3f), new Vector3(3.2f, 0.3f, 0.3f), assets.Yellow, coreLandmark.transform, false);

            GameObject exitLandmark = CreateGroup("EXIT_LANDMARK_GREEN", parent);
            CreateCube("ExitBeacon_Left", new Vector3(-2.1f, 6.2f, 18.2f), new Vector3(0.4f, 6.4f, 0.4f), assets.Green, exitLandmark.transform, false);
            CreateCube("ExitBeacon_Right", new Vector3(2.1f, 6.2f, 18.2f), new Vector3(0.4f, 6.4f, 0.4f), assets.Green, exitLandmark.transform, false);
            CreateCube("ExitBeacon_Top", new Vector3(0f, 9.4f, 18.2f), new Vector3(4.6f, 0.4f, 0.4f), assets.Green, exitLandmark.transform, false);
        }

        private static void CreateGuard(
            string guardName,
            string pathName,
            Vector3 guardPosition,
            Vector3[] waypointPositions,
            Transform player,
            Transform gameplayRoot,
            GameObject guardPrefab,
            Scene scene,
            bool loop = false)
        {
            GameObject pathObject = new GameObject(pathName);
            pathObject.transform.SetParent(gameplayRoot);
            LDWaypointPath path = pathObject.AddComponent<LDWaypointPath>();
            SerializedObject serializedPath = new SerializedObject(path);
            serializedPath.FindProperty("loop").boolValue = loop;
            serializedPath.ApplyModifiedPropertiesWithoutUndo();

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
            Check(CountComponents<LDPlayerMotor>(scene) == 1, "expected 1 player", failures, ref passed);
            Check(CountComponents<LDThirdPersonCamera>(scene) == 1, "expected 1 camera", failures, ref passed);
            Check(CountComponents<LDGameSession>(scene) == 1, "expected 1 session", failures, ref passed);
            Check(CountComponents<LDCheckpoint>(scene) == 1, "expected 1 checkpoint", failures, ref passed);
            Check(CountComponents<LDKeyCollectible>(scene) == 1, "expected 1 Energy Core", failures, ref passed);
            Check(CountComponents<LDExitGoal>(scene) == 1, "expected 1 Exit", failures, ref passed);
            Check(CountComponents<LDEnemyGuard>(scene) == 4, "expected 4 guards", failures, ref passed);
            Check(CountComponents<LDGuardVisionCone>(scene) == 4, "expected a red view cone on every guard", failures, ref passed);

            LDWaypointPath[] paths = GetComponentsInScene<LDWaypointPath>(scene);
            bool pathsValid = paths.Length == 4;
            foreach (LDWaypointPath path in paths)
            {
                pathsValid &= path.Count >= 2;
            }
            Check(pathsValid, "expected 4 valid patrol paths", failures, ref passed);

            Check(FindInScene(scene, "LD_Guard_01_Entry") != null, "missing entry guard", failures, ref passed);
            Check(FindInScene(scene, "LD_Guard_02_CrossfireHorizontal") != null, "missing horizontal crossfire guard", failures, ref passed);
            Check(FindInScene(scene, "LD_Guard_03_CrossfireVertical") != null, "missing vertical crossfire guard", failures, ref passed);
            Check(FindInScene(scene, "LD_Guard_04_UpperClockwiseLoop") != null, "missing upper clockwise guard", failures, ref passed);
            GameObject coreLoopObject = FindInScene(scene, "PatrolPath_04_UpperClockwiseLoop");
            LDWaypointPath coreLoopPath = coreLoopObject != null ? coreLoopObject.GetComponent<LDWaypointPath>() : null;
            Check(coreLoopPath != null && coreLoopPath.Loop && coreLoopPath.Count == 4, "upper patrol must be a four-point loop", failures, ref passed);
            bool clockwisePath = coreLoopPath != null && coreLoopPath.Count == 4
                && coreLoopPath.GetPoint(0).position.x < 0f && coreLoopPath.GetPoint(0).position.z < 14f
                && coreLoopPath.GetPoint(1).position.x < 0f && coreLoopPath.GetPoint(1).position.z > 16f
                && coreLoopPath.GetPoint(2).position.x > 0f && coreLoopPath.GetPoint(2).position.z > 16f
                && coreLoopPath.GetPoint(3).position.x > 0f && coreLoopPath.GetPoint(3).position.z < 14f;
            Check(clockwisePath, "upper patrol waypoint order must remain clockwise", failures, ref passed);
            Check(FindInScene(scene, "ENTRY_ROUTE_MAIN_LEFT_SAFE_LONG") != null, "missing entry main route", failures, ref passed);
            Check(FindInScene(scene, "ENTRY_ROUTE_OPTIONAL_RIGHT_FAST_RISKY") != null, "missing entry optional route", failures, ref passed);
            Check(FindInScene(scene, "SAFE_OBSERVATION_BEFORE_CROSSFIRE_Left") != null, "missing safe observation area", failures, ref passed);
            Check(FindInScene(scene, "LOWER_ROUTE_A_LEFT_SAFE_LONG") != null, "missing lower safe route", failures, ref passed);
            Check(FindInScene(scene, "MID_ROUTE_B_RIGHT_FAST_SPRINT_JUMP") != null, "missing mid-level sprint route", failures, ref passed);
            Check(FindInScene(scene, "FINAL_SPRINT_JUMP_REQUIRED_3M") != null, "missing mandatory final sprint jump", failures, ref passed);
            Check(FindInScene(scene, "UPPER_ROUTE_CLOCKWISE_RING") != null, "missing upper clockwise ring", failures, ref passed);

            GameObject midRunway = FindInScene(scene, "RightMid_Runway");
            GameObject midLanding = FindInScene(scene, "RightMid_SprintLanding");
            float midGap = midRunway != null && midLanding != null
                ? midRunway.GetComponent<Renderer>().bounds.min.x - midLanding.GetComponent<Renderer>().bounds.max.x
                : 0f;
            Check(midGap >= 2.5f && midGap <= 3.1f, "mid sprint-jump gap must remain 2.5-3.1m", failures, ref passed);

            GameObject finalRunway = FindInScene(scene, "FinalSprintJump_Runway");
            GameObject upperLanding = FindInScene(scene, "UpperRing_SouthLanding");
            float finalGap = finalRunway != null && upperLanding != null
                ? upperLanding.GetComponent<Renderer>().bounds.min.z - finalRunway.GetComponent<Renderer>().bounds.max.z
                : 0f;
            Check(finalGap >= 2.7f && finalGap <= 3.2f, "final sprint-jump gap must remain 2.7-3.2m", failures, ref passed);
            Check(upperLanding != null && upperLanding.transform.position.y >= 2.5f, "upper route must remain elevated", failures, ref passed);
            Check(FindInScene(scene, "UPPER_RING_GUARD_RAILS") != null, "missing upper guard containment rails", failures, ref passed);
            Check(FindInScene(scene, "CORE_LANDMARK_YELLOW_TOWER") != null, "missing Core landmark", failures, ref passed);
            Check(FindInScene(scene, "EXIT_LANDMARK_GREEN") != null, "missing Exit landmark", failures, ref passed);

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
                Yellow = LoadRequired<Material>(MaterialsFolder + "/LD_ObjectiveYellow.mat"),
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
            string previewFolder = Path.Combine(projectRoot, "Temp", "Scene3ChallengePreviews");
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
                reviewCamera.orthographicSize = 22f;
                cameraObject.transform.position = new Vector3(0f, 48f, 0f);
                cameraObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                RenderPreview(reviewCamera, 900, 900, Path.Combine(previewFolder, "TopDown.png"));

                reviewCamera.orthographic = false;
                reviewCamera.fieldOfView = 55f;
                cameraObject.transform.position = new Vector3(0f, 4f, -18.7f);
                cameraObject.transform.LookAt(new Vector3(0f, 2f, -6f));
                RenderPreview(reviewCamera, 960, 540, Path.Combine(previewFolder, "Gameplay_Start.png"));

                cameraObject.transform.position = new Vector3(-14f, 10f, -1f);
                cameraObject.transform.LookAt(new Vector3(0f, 2f, 8f));
                RenderPreview(reviewCamera, 960, 540, Path.Combine(previewFolder, "Crossfire.png"));

                cameraObject.transform.position = new Vector3(13f, 10f, 2f);
                cameraObject.transform.LookAt(new Vector3(0f, 2.6f, 10.5f));
                RenderPreview(reviewCamera, 960, 540, Path.Combine(previewFolder, "JumpShortcut.png"));

                cameraObject.transform.position = new Vector3(12f, 12f, 14.5f);
                cameraObject.transform.LookAt(new Vector3(0f, 3.2f, 15.3f));
                RenderPreview(reviewCamera, 960, 540, Path.Combine(previewFolder, "UpperClockwise.png"));
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
                if (string.Equals(scenes[i].path, LevelTwoScenePath, StringComparison.OrdinalIgnoreCase))
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

            string backupPath = Path.Combine(projectRoot, "Temp", "Level 3.before-build.unity");
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
