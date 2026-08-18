using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public static class HookupExitAction
{
    [MenuItem("Tools/Hookup ExitActionListener to InputActionAsset")]
    public static void Hookup()
    {
        string path = "Assets/InputSystem_Actions.inputactions";
        var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
        if (asset == null)
        {
            EditorUtility.DisplayDialog("Hookup ExitActionListener", $"Could not find InputActionAsset at {path}.", "OK");
            return;
        }

        int assignedCount = 0;

        // Prefabs
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        for (int i = 0; i < prefabGuids.Length; i++)
        {
            string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
            var prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            var listeners = prefabRoot.GetComponentsInChildren<ExitActionListener>(true);
            bool changed = false;
            foreach (var l in listeners)
            {
                if (l.inputActions != asset)
                {
                    var so = new SerializedObject(l);
                    var prop = so.FindProperty("inputActions");
                    if (prop != null)
                    {
                        prop.objectReferenceValue = asset;
                        so.ApplyModifiedProperties();
                        assignedCount++;
                        changed = true;
                    }
                }
            }
            if (changed) PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        // Scenes
        string[] sceneGuids = AssetDatabase.FindAssets("t:Scene");
        string activeScenePath = SceneManager.GetActiveScene().path;
        for (int i = 0; i < sceneGuids.Length; i++)
        {
            string scenePath = AssetDatabase.GUIDToAssetPath(sceneGuids[i]);
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            if (!scene.isLoaded) continue;
            bool sceneChanged = false;
            var roots = scene.GetRootGameObjects();
            foreach (var root in roots)
            {
                var listeners = root.GetComponentsInChildren<ExitActionListener>(true);
                foreach (var l in listeners)
                {
                    if (l.inputActions != asset)
                    {
                        var so = new SerializedObject(l);
                        var prop = so.FindProperty("inputActions");
                        if (prop != null)
                        {
                            prop.objectReferenceValue = asset;
                            so.ApplyModifiedProperties();
                            assignedCount++;
                            sceneChanged = true;
                        }
                    }
                }
            }
            if (sceneChanged)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }
        // Restore active scene
        if (!string.IsNullOrEmpty(activeScenePath)) EditorSceneManager.OpenScene(activeScenePath, OpenSceneMode.Single);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Hookup ExitActionListener", $"Assigned InputActionAsset to {assignedCount} ExitActionListener components.", "OK");
        Debug.Log($"HookupExitAction: Assigned InputActionAsset to {assignedCount} ExitActionListener components.");
    }
}
