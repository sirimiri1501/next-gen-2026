using UnityEditor;
using UnityEngine;

namespace LevelDesignStarterKit.Editor
{
    public sealed class LDPatrolPathTool : EditorWindow
    {
        private LDEnemyGuard selectedGuard;
        private GameObject guardPrefab;
        private bool isPlacing;
        private bool isCreatingGuard;
        private bool hasStartPoint;
        private Vector3 startPoint;
        private Vector3 hoverPoint;
        private bool hasHoverPoint;

        [MenuItem("Tools/Level Design Starter Kit/Patrol Path Painter")]
        private static void OpenWindow()
        {
            GetWindow<LDPatrolPathTool>("Patrol Path");
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            FindDefaultGuardPrefab();
            UseGuardFromSelection();
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        private void OnSelectionChange()
        {
            if (!isPlacing)
            {
                UseGuardFromSelection();
                Repaint();
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Create a new enemy and its path with three Scene-view clicks, or assign a new path to an existing guard with two clicks.",
                MessageType.Info);

            using (new EditorGUI.DisabledScope(isPlacing))
            {
                EditorGUILayout.LabelField("Create Enemy", EditorStyles.boldLabel);
                guardPrefab = (GameObject)EditorGUILayout.ObjectField(
                    "Guard Prefab",
                    guardPrefab,
                    typeof(GameObject),
                    false);

                using (new EditorGUI.DisabledScope(!IsGuardPrefabValid()))
                {
                    if (GUILayout.Button("Create Enemy & Path", GUILayout.Height(30f)))
                    {
                        BeginEnemyPlacement();
                    }
                }

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Existing Enemy", EditorStyles.boldLabel);
                selectedGuard = (LDEnemyGuard)EditorGUILayout.ObjectField(
                    "Enemy Guard",
                    selectedGuard,
                    typeof(LDEnemyGuard),
                    true);
            }

            if (!isPlacing)
            {
                using (new EditorGUI.DisabledScope(selectedGuard == null))
                {
                    if (GUILayout.Button("Place Start & End", GUILayout.Height(30f)))
                    {
                        BeginPlacement();
                    }
                }
            }
            else
            {
                string nextPoint = isCreatingGuard ? "ENEMY POSITION" : hasStartPoint ? "END" : "START";
                EditorGUILayout.HelpBox(
                    "Click the " + nextPoint + " point in the Scene view. Press Esc to cancel.",
                    MessageType.Warning);

                if (GUILayout.Button("Cancel"))
                {
                    CancelPlacement();
                }
            }
        }

        private void BeginPlacement()
        {
            if (selectedGuard == null)
            {
                return;
            }

            isPlacing = true;
            isCreatingGuard = false;
            hasStartPoint = false;
            hasHoverPoint = false;
            SceneView.RepaintAll();
            Repaint();
        }

        private void BeginEnemyPlacement()
        {
            if (!IsGuardPrefabValid())
            {
                return;
            }

            selectedGuard = null;
            isPlacing = true;
            isCreatingGuard = true;
            hasStartPoint = false;
            hasHoverPoint = false;
            SceneView.RepaintAll();
            Repaint();
        }

        private void CancelPlacement()
        {
            isPlacing = false;
            isCreatingGuard = false;
            hasStartPoint = false;
            hasHoverPoint = false;
            SceneView.RepaintAll();
            Repaint();
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (!isPlacing || (!isCreatingGuard && selectedGuard == null))
            {
                return;
            }

            Event currentEvent = Event.current;
            if (currentEvent.type == EventType.KeyDown && currentEvent.keyCode == KeyCode.Escape)
            {
                CancelPlacement();
                currentEvent.Use();
                return;
            }

            hasHoverPoint = TryGetPlacementPoint(currentEvent.mousePosition, out hoverPoint);
            DrawPlacementPreview();

            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            if (currentEvent.type != EventType.MouseDown
                || currentEvent.button != 0
                || currentEvent.alt
                || !hasHoverPoint)
            {
                sceneView.Repaint();
                return;
            }

            if (isCreatingGuard)
            {
                CreateGuard(hoverPoint);
                isCreatingGuard = false;
            }
            else if (!hasStartPoint)
            {
                startPoint = hoverPoint;
                hasStartPoint = true;
            }
            else
            {
                CreateAndAssignPath(startPoint, hoverPoint);
                isPlacing = false;
                hasStartPoint = false;
                hasHoverPoint = false;
            }

            currentEvent.Use();
            SceneView.RepaintAll();
            Repaint();
        }

        private bool TryGetPlacementPoint(Vector2 mousePosition, out Vector3 point)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
            RaycastHit[] hits = Physics.RaycastAll(
                ray,
                Mathf.Infinity,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);

            System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            foreach (RaycastHit hit in hits)
            {
                if (selectedGuard != null
                    && (hit.transform == selectedGuard.transform || hit.transform.IsChildOf(selectedGuard.transform)))
                {
                    continue;
                }

                point = hit.point;
                return true;
            }

            float groundHeight = selectedGuard != null ? selectedGuard.transform.position.y : 0f;
            Plane guardGroundPlane = new Plane(Vector3.up, Vector3.up * groundHeight);
            if (guardGroundPlane.Raycast(ray, out float enter))
            {
                point = ray.GetPoint(enter);
                return true;
            }

            point = default;
            return false;
        }

        private void DrawPlacementPreview()
        {
            if (hasStartPoint)
            {
                DrawPoint(startPoint, "START", new Color(0.15f, 1f, 0.35f));
            }

            if (!hasHoverPoint)
            {
                return;
            }

            Color hoverColor = isCreatingGuard
                ? new Color(1f, 0.25f, 0.2f)
                : hasStartPoint
                    ? new Color(1f, 0.75f, 0.1f)
                    : new Color(0.15f, 1f, 0.35f);
            string hoverLabel = isCreatingGuard ? "ENEMY" : hasStartPoint ? "END" : "START";
            DrawPoint(hoverPoint, hoverLabel, hoverColor);

            if (hasStartPoint)
            {
                Handles.color = new Color(0.1f, 0.9f, 1f);
                Handles.DrawAAPolyLine(4f, startPoint, hoverPoint);
            }
        }

        private static void DrawPoint(Vector3 position, string label, Color color)
        {
            float size = HandleUtility.GetHandleSize(position) * 0.12f;
            Handles.color = color;
            Handles.DrawSolidDisc(position, Vector3.up, size);
            Handles.Label(position + Vector3.up * size, label, EditorStyles.boldLabel);
        }

        private void CreateGuard(Vector3 position)
        {
            GameObject guardObject = (GameObject)PrefabUtility.InstantiatePrefab(guardPrefab);
            Undo.RegisterCreatedObjectUndo(guardObject, "Create Enemy Guard");
            guardObject.transform.position = position;
            selectedGuard = guardObject.GetComponent<LDEnemyGuard>();
            Selection.activeGameObject = guardObject;
        }

        private void CreateAndAssignPath(Vector3 start, Vector3 end)
        {
            GameObject pathObject = new GameObject("PatrolPath");
            Undo.RegisterCreatedObjectUndo(pathObject, "Create Guard Patrol Path");
            Undo.SetTransformParent(
                pathObject.transform,
                selectedGuard.transform,
                "Parent Guard Patrol Path");

            LDWaypointPath path = Undo.AddComponent<LDWaypointPath>(pathObject);
            CreateWaypoint(pathObject.transform, "Waypoint_01_Start", start);
            CreateWaypoint(pathObject.transform, "Waypoint_02_End", end);

            Undo.RecordObject(selectedGuard, "Assign Guard Patrol Path");
            SerializedObject serializedGuard = new SerializedObject(selectedGuard);
            serializedGuard.FindProperty("patrolPath").objectReferenceValue = path;
            serializedGuard.ApplyModifiedProperties();
            EditorUtility.SetDirty(selectedGuard);

            Selection.activeGameObject = selectedGuard.gameObject;
            EditorGUIUtility.PingObject(pathObject);
        }

        private static void CreateWaypoint(Transform parent, string name, Vector3 position)
        {
            GameObject waypoint = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(waypoint, "Create Patrol Waypoint");
            Undo.SetTransformParent(waypoint.transform, parent, "Parent Patrol Waypoint");
            waypoint.transform.position = position;
        }

        private void UseGuardFromSelection()
        {
            GameObject activeObject = Selection.activeGameObject;
            if (activeObject == null)
            {
                return;
            }

            LDEnemyGuard guard = activeObject.GetComponentInParent<LDEnemyGuard>();
            if (guard != null)
            {
                selectedGuard = guard;
            }
        }

        private bool IsGuardPrefabValid()
        {
            return guardPrefab != null && guardPrefab.GetComponent<LDEnemyGuard>() != null;
        }

        private void FindDefaultGuardPrefab()
        {
            if (IsGuardPrefabValid())
            {
                return;
            }

            string[] prefabGuids = AssetDatabase.FindAssets("LD_Guard t:Prefab");
            foreach (string prefabGuid in prefabGuids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(prefabGuid);
                GameObject candidate = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (candidate != null && candidate.GetComponent<LDEnemyGuard>() != null)
                {
                    guardPrefab = candidate;
                    return;
                }
            }
        }
    }
}
