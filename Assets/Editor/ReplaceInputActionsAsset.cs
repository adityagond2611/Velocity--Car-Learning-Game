using UnityEditor;
using UnityEngine;
#if UNITY_EDITOR
using UnityEngine.InputSystem;
#endif

public static class ReplaceInputActionsAsset
{
    [MenuItem("Tools/Replace Input Actions Asset (Create Scriptable InputActionAsset)")]
    public static void Replace()
    {
#if !UNITY_EDITOR
        Debug.LogError("ReplaceInputActionsAsset must be run in the Editor.");
        return;
#endif
        string path = "Assets/InputSystem_Actions.inputactions";
        string backup = path + ".bak2";

        // Backup any existing file at path
        try
        {
            if (System.IO.File.Exists(path))
            {
                // Try to move via AssetDatabase first to preserve meta
                var moveError = AssetDatabase.MoveAsset(path, backup);
                if (!string.IsNullOrEmpty(moveError))
                {
                    try { System.IO.File.Move(path, backup); } catch { }
                    try { AssetDatabase.DeleteAsset(path); } catch { }
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"ReplaceInputActionsAsset: Could not backup existing file: {ex.Message}");
        }

        // Remove any leftover asset entry
        try { AssetDatabase.DeleteAsset(path); } catch { }
        try { if (System.IO.File.Exists(path)) System.IO.File.Delete(path); } catch { }

        // Create a real InputActionAsset ScriptableObject and try to save it to the desired path
        var asset = ScriptableObject.CreateInstance<InputActionAsset>();
        var map = new InputActionMap("Player");
        var move = map.AddAction("Move", InputActionType.Value);
        move.expectedControlType = "Vector2";
        var look = map.AddAction("Look", InputActionType.Value);
        look.expectedControlType = "Vector2";
        var attack = map.AddAction("Attack", InputActionType.Button);
        attack.expectedControlType = "Button";
        asset.AddActionMap(map);

        bool createdAtDesiredPath = false;
        try
        {
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var loaded = AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
            if (loaded != null)
            {
                createdAtDesiredPath = true;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"ReplaceInputActionsAsset: Failed to create asset at {path}: {ex.Message}");
        }

        if (!createdAtDesiredPath)
        {
            // Fallback: create at .asset and then attempt to move
            string altPath = "Assets/InputSystem_Actions.asset";
            try
            {
                var altAsset = ScriptableObject.CreateInstance<InputActionAsset>();
                altAsset.AddActionMap(map);
                AssetDatabase.CreateAsset(altAsset, altPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                // Try move to desired extension
                var moveError = AssetDatabase.MoveAsset(altPath, path);
                if (!string.IsNullOrEmpty(moveError))
                {
                    Debug.LogWarning($"ReplaceInputActionsAsset: MoveAsset returned error: {moveError}. Asset kept at {altPath}.");
                }
                else
                {
                    createdAtDesiredPath = true;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"ReplaceInputActionsAsset: Fallback creation failed: {ex.Message}");
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (createdAtDesiredPath)
        {
            EditorUtility.DisplayDialog("Replace Input Actions Asset", $"Created InputActionAsset at {path}. Backup (if existed) moved to {backup}.", "OK");
            Debug.Log($"ReplaceInputActionsAsset: Created InputActionAsset at {path} and backed up original to {backup}.");
        }
        else
        {
            EditorUtility.DisplayDialog("Replace Input Actions Asset", $"Could not create InputActionAsset at {path}. A fallback asset may exist as Assets/InputSystem_Actions.asset. Please check console.", "OK");
            Debug.LogError($"ReplaceInputActionsAsset: Could not create asset at {path}. Check console for details.");
        }
    }
}
