using UnityEditor;
using UnityEngine;
#if UNITY_EDITOR
using UnityEngine.InputSystem;
#endif

public static class AddExitAction
{
    [MenuItem("Tools/Add Exit Action to Input Actions")]
    public static void Add()
    {
#if !UNITY_EDITOR
        Debug.LogError("AddExitAction must be run in the Editor.");
        return;
#endif
        string path = "Assets/InputSystem_Actions.inputactions";
        var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
        if (asset == null)
        {
            // Try to auto-create a Scriptable InputActionAsset if possible
            if (EditorUtility.DisplayDialog("Add Exit Action",
                $"Could not find InputActionAsset at {path}. Create a new InputActionAsset now? (Recommended)",
                "Yes", "No"))
            {
                // Call the replacer utility to create a real InputActionAsset ScriptableObject
                ReplaceInputActionsAsset.Replace();
                asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
                if (asset == null)
                {
                    EditorUtility.DisplayDialog("Add Exit Action", $"Could not create InputActionAsset at {path}.", "OK");
                    return;
                }
            }
            else
            {
                EditorUtility.DisplayDialog("Add Exit Action", $"Could not find InputActionAsset at {path}.", "OK");
                return;
            }
        }

        var map = asset.FindActionMap("Player", throwIfNotFound: false);
        if (map == null)
        {
            map = new InputActionMap("Player");
            asset.AddActionMap(map);
        }

        var existing = map.FindAction("Exit", throwIfNotFound: false);
        if (existing != null)
        {
            EditorUtility.DisplayDialog("Add Exit Action", "Exit action already exists in Player map.", "OK");
            return;
        }

        var exit = map.AddAction("Exit", InputActionType.Button);
        // Add a common binding: Escape key and gamepad start/buttonSouth
        exit.AddBinding("<Keyboard>/escape");
        exit.AddBinding("<Gamepad>/start");
        exit.AddBinding("<Gamepad>/buttonSouth");

        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Add Exit Action", "Added Exit action with bindings (Escape, Gamepad Start, A).", "OK");
        Debug.Log("AddExitAction: Exit action added to InputSystem_Actions.inputactions");
    }
}
