using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if UNITY_EDITOR
// Note: This script references the Input System types. Ensure the Input System package is installed before running.
using UnityEngine.InputSystem;
#endif

// Auto-fix utility: assigns the project's InputActionAsset to CinemachineLookActionBinder components,
// reserializes UI Images, invokes the binder's internal BindLookAction method via reflection (editor-time),
// and saves prefabs and scenes.

public static class AutoFixProject
{
    [MenuItem("Tools/Auto Fix Project (Assign InputActions + Reserialize Images + Bind Cinemachine)")]
    public static void RunAutoFix()
    {
#if !UNITY_EDITOR
        Debug.LogError("AutoFixProject can only run in the Unity Editor.");
        return;
#endif
        if (!EditorUtility.DisplayDialog("Auto Fix Project",
            "This tool will assign the InputActionAsset (Assets/InputSystem_Actions.inputactions) to all CinemachineLookActionBinder components, reserialize UI Images in prefabs and scenes, and attempt to invoke binder wiring. Please backup/commit your project before continuing.",
            "Proceed", "Cancel"))
        {
            return;
        }

        try
        {
            string inputAssetPath = "Assets/InputSystem_Actions.inputactions";
            InputActionAsset inputAsset = null;

            // Load or create InputActionAsset
            try
            {
                inputAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(inputAssetPath);
            }
            catch (Exception)
            {
                inputAsset = null;
            }

            if (inputAsset == null)
            {
                // Try to create a small asset with Player/Look to satisfy binders
                try
                {
                    inputAsset = ScriptableObject.CreateInstance<InputActionAsset>();
                    var map = new InputActionMap("Player");
                    var look = map.AddAction("Look", InputActionType.Value);
                    look.expectedControlType = "Vector2";
                    inputAsset.AddActionMap(map);

                    AssetDatabase.CreateAsset(inputAsset, inputAssetPath);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();

                    Debug.Log($"AutoFixProject: Created new InputActionAsset at {inputAssetPath} with Player/Look action.");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"AutoFixProject: Unable to load or create InputActionAsset at {inputAssetPath}: {ex.Message}");
                    inputAsset = null;
                }
            }

            int prefabCount = 0, prefabAssignCount = 0, prefabBindInvoked = 0;
            int sceneCount = 0, sceneAssignCount = 0, sceneBindInvoked = 0;
            int imagesTouched = 0;

            // Helper to invoke private BindLookAction on a component instance via reflection
            Action<UnityEngine.Object> TryInvokeBind = (comp) =>
            {
                if (comp == null) return;
                try
                {
                    var type = comp.GetType();
                    var method = type.GetMethod("BindLookAction", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (method != null)
                    {
                        method.Invoke(comp, null);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"AutoFixProject: Failed to invoke BindLookAction on {comp.GetType().Name}: {e.Message}");
                }
            };

            // Process prefabs
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            prefabCount = prefabGuids.Length;
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                EditorUtility.DisplayProgressBar("Auto Fix Project", $"Processing prefab {i+1}/{prefabGuids.Length}: {prefabPath}", (float)i / prefabGuids.Length);

                var prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
                bool changed = false;

                // Assign InputActionAsset to CinemachineLookActionBinder components
                var monoBehaviours = prefabRoot.GetComponentsInChildren<MonoBehaviour>(true);
                foreach (var mb in monoBehaviours)
                {
                    if (mb == null) continue;
                    var typeName = mb.GetType().Name;
                    if (typeName == "CinemachineLookActionBinder")
                    {
                        if (inputAsset != null)
                        {
                            var so = new SerializedObject(mb);
                            var prop = so.FindProperty("inputActions");
                            if (prop != null && prop.objectReferenceValue != inputAsset)
                            {
                                prop.objectReferenceValue = inputAsset;
                                so.ApplyModifiedProperties();
                                changed = true;
                                prefabAssignCount++;
                            }
                        }

                        // Attempt to bind immediately (editor-time) so prefab is wired
                        TryInvokeBind(mb);
                        prefabBindInvoked++;
                    }
                }

                // Re-serialize Image components in this prefab
                var images = prefabRoot.GetComponentsInChildren<Image>(true);
                foreach (var img in images)
                {
                    if (img == null) continue;
                    // Touch sprite property to force reserialization
                    var s = img.sprite;
                    img.sprite = s;
                    // touch another property
                    img.type = img.type;
                    imagesTouched++;
                    changed = true;
                }

                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                }

                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }

            // Process scenes
            string[] sceneGuids = AssetDatabase.FindAssets("t:Scene");
            sceneCount = sceneGuids.Length;
            string activeScenePath = SceneManager.GetActiveScene().path;

            for (int i = 0; i < sceneGuids.Length; i++)
            {
                string scenePath = AssetDatabase.GUIDToAssetPath(sceneGuids[i]);
                EditorUtility.DisplayProgressBar("Auto Fix Project", $"Processing scene {i+1}/{sceneGuids.Length}: {scenePath}", (float)i / sceneGuids.Length);

                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                if (!scene.isLoaded) continue;

                bool sceneChanged = false;
                var roots = scene.GetRootGameObjects();
                foreach (var root in roots)
                {
                    var monoBehaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
                    foreach (var mb in monoBehaviours)
                    {
                        if (mb == null) continue;
                        var typeName = mb.GetType().Name;
                        if (typeName == "CinemachineLookActionBinder")
                        {
                            if (inputAsset != null)
                            {
                                var so = new SerializedObject(mb);
                                var prop = so.FindProperty("inputActions");
                                if (prop != null && prop.objectReferenceValue != inputAsset)
                                {
                                    prop.objectReferenceValue = inputAsset;
                                    so.ApplyModifiedProperties();
                                    sceneChanged = true;
                                    sceneAssignCount++;
                                }
                            }

                            TryInvokeBind(mb);
                            sceneBindInvoked++;
                        }
                    }

                    var images = root.GetComponentsInChildren<Image>(true);
                    foreach (var img in images)
                    {
                        if (img == null) continue;
                        var s = img.sprite;
                        img.sprite = s;
                        img.type = img.type;
                        imagesTouched++;
                        sceneChanged = true;
                    }
                }

                if (sceneChanged)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
            }

            // Restore active scene
            if (!string.IsNullOrEmpty(activeScenePath))
            {
                EditorSceneManager.OpenScene(activeScenePath, OpenSceneMode.Single);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.ClearProgressBar();

            Debug.Log($"AutoFixProject: Prefabs scanned: {prefabCount}. Prefab InputAction assignments: {prefabAssignCount}. Prefab bind invocations: {prefabBindInvoked}.");
            Debug.Log($"AutoFixProject: Scenes scanned: {sceneCount}. Scene InputAction assignments: {sceneAssignCount}. Scene bind invocations: {sceneBindInvoked}.");
            Debug.Log($"AutoFixProject: Images touched (reserialized): {imagesTouched}.");

            EditorUtility.DisplayDialog("Auto Fix Project", $"Completed. Prefabs scanned: {prefabCount}. Scenes scanned: {sceneCount}.\nAssignments (prefabs/scenes): {prefabAssignCount}/{sceneAssignCount}.\nBind calls invoked (prefabs/scenes): {prefabBindInvoked}/{sceneBindInvoked}.\nImages reserialized: {imagesTouched}.", "OK");
        }
        catch (Exception ex)
        {
            EditorUtility.ClearProgressBar();
            Debug.LogError($"AutoFixProject: Exception during run: {ex}");
            EditorUtility.DisplayDialog("Auto Fix Project", $"Error: {ex.Message}. See Console for full stack.", "OK");
        }
    }
}
