using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Small editor utility to force Unity to reserialize Image components in prefabs and scenes.
// Run via the Unity Editor menu: Tools -> Reserialize UI Images
// IMPORTANT: Make a backup or commit your project before running this.

public static class ReserializeImages
{
    [MenuItem("Tools/Reserialize UI Images (Prefabs + Scenes)")]
    public static void ReserializeAll()
    {
        if (!EditorUtility.DisplayDialog("Reserialize UI Images",
            "This will open scenes and re-save prefabs to force reserialization of all UnityEngine.UI.Image components. Make sure you have a backup or commit before proceeding. Continue?",
            "Yes", "No"))
        {
            return;
        }

        try
        {
            ReserializePrefabs();
            ReserializeScenes();
            EditorUtility.DisplayDialog("Reserialize UI Images", "Finished reserializing prefabs and scenes.", "OK");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"ReserializeImages: exception: {ex}");
            EditorUtility.DisplayDialog("Reserialize UI Images", "An error occurred. See console for details.", "OK");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }

    private static void ReserializePrefabs()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab");
        int total = guids.Length;
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            EditorUtility.DisplayProgressBar("Reserialize UI Images", $"Processing prefab {i+1}/{total}: {path}", (float)i/total);

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            bool changed = false;
            var images = prefab.GetComponentsInChildren<Image>(true);
            foreach (var img in images)
            {
                // Force a tiny write to the serialized data to make Unity reserialize this component.
                // Assigning the same value back will mark the prefab as dirty when done via PrefabUtility.
                var s = img.sprite;
                img.sprite = s;
                // Forcing other properties that might be serialized with name changes
                img.type = img.type;
                // No direct SetDirty on components on prefab asset; mark the prefab root dirty if any image found
                if (images.Length > 0) changed = true;
            }

            if (changed)
            {
                PrefabUtility.SavePrefabAsset(prefab);
            }
        }
    }

    private static void ReserializeScenes()
    {
        string[] guids = AssetDatabase.FindAssets("t:Scene");
        int total = guids.Length;
        // Keep track of the currently active scene to restore later
        string activeScenePath = SceneManager.GetActiveScene().path;

        for (int i = 0; i < guids.Length; i++)
        {
            string scenePath = AssetDatabase.GUIDToAssetPath(guids[i]);
            EditorUtility.DisplayProgressBar("Reserialize UI Images", $"Processing scene {i+1}/{total}: {scenePath}", (float)i/total);

            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            if (!scene.isLoaded) continue;

            bool sceneChanged = false;
            var rootObjects = scene.GetRootGameObjects();
            foreach (var root in rootObjects)
            {
                var images = root.GetComponentsInChildren<Image>(true);
                foreach (var img in images)
                {
                    var s = img.sprite;
                    img.sprite = s;
                    img.type = img.type;
                    sceneChanged = true;
                }
            }

            if (sceneChanged)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }

        // Restore previously active scene if possible
        if (!string.IsNullOrEmpty(activeScenePath))
        {
            EditorSceneManager.OpenScene(activeScenePath, OpenSceneMode.Single);
        }
    }
}
