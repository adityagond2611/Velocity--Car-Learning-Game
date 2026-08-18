using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Editor utility to assign a project InputActionAsset to all CinemachineLookActionBinder components
// in prefabs and scenes. Uses SerializedObject to set the private serialized field.

public static class AssignInputActionsToCinemachine
{
    [MenuItem("Tools/Assign InputActions to CinemachineLookActionBinder")] 
    public static void Assign()
    {
        if (!EditorUtility.DisplayDialog("Assign InputActions",
            "This will assign Assets/InputSystem_Actions.inputactions to every CinemachineLookActionBinder in the project (prefabs + scenes). Make sure you have backed up or committed your project. Continue?",
            "Yes", "No"))
        {
            return;
        }

        // Load the asset
        string assetPath = "Assets/InputSystem_Actions.inputactions";
        var inputAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(assetPath);
        if (inputAsset == null)
        {
            EditorUtility.DisplayDialog("Assign InputActions", $"Could not find InputActionAsset at {assetPath}. Please ensure the asset exists and that the Input System package is installed.", "OK");
            return;
        }

        // Prefabs
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        for (int i = 0; i < prefabGuids.Length; i++)
        {
            string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
            EditorUtility.DisplayProgressBar("Assign InputActions", $"Processing prefab {i+1}/{prefabGuids.Length}: {prefabPath}", (float)i / prefabGuids.Length);

            var prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            var binders = prefabRoot.GetComponentsInChildren(typeof(object), true); // fallback

            var components = prefabRoot.GetComponentsInChildren<MonoBehaviour>(true);
            bool changed = false;
            foreach (var comp in components)
            {
                if (comp == null) continue;
                var typeName = comp.GetType().Name;
                if (typeName == "CinemachineLookActionBinder")
                {
                    var so = new SerializedObject(comp);
                    var prop = so.FindProperty("inputActions");
                    if (prop != null)
                    {
                        if (prop.objectReferenceValue != inputAsset)
                        {
                            prop.objectReferenceValue = inputAsset;
                            so.ApplyModifiedProperties();
                            changed = true;
                        }
                    }
                }
            }

            if (changed)
            {
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            }

            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        // Scenes
        string[] sceneGuids = AssetDatabase.FindAssets("t:Scene");
        for (int i = 0; i < sceneGuids.Length; i++)
        {
            string scenePath = AssetDatabase.GUIDToAssetPath(sceneGuids[i]);
            EditorUtility.DisplayProgressBar("Assign InputActions", $"Processing scene {i+1}/{sceneGuids.Length}: {scenePath}", (float)i / sceneGuids.Length);

            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            if (!scene.isLoaded) continue;

            bool sceneChanged = false;
            var roots = scene.GetRootGameObjects();
            foreach (var root in roots)
            {
                var componentsInScene = root.GetComponentsInChildren<MonoBehaviour>(true);
                foreach (var comp in componentsInScene)
                {
                    if (comp == null) continue;
                    var typeName = comp.GetType().Name;
                    if (typeName == "CinemachineLookActionBinder")
                    {
                        var so = new SerializedObject(comp);
                        var prop = so.FindProperty("inputActions");
                        if (prop != null && prop.objectReferenceValue != inputAsset)
                        {
                            prop.objectReferenceValue = inputAsset;
                            so.ApplyModifiedProperties();
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

        EditorUtility.ClearProgressBar();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Assign InputActions", "Done assigning InputActionAsset to CinemachineLookActionBinder components.", "OK");
    }
}
