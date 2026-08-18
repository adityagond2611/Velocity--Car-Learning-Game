using UnityEngine;
using UnityEngine.InputSystem;

public class ExitActionListener : MonoBehaviour
{
    public InputActionAsset inputActions;
    public string actionPath = "Player/Exit";

    private InputAction exitAction;

    private void OnEnable()
    {
        if (inputActions == null)
        {
            Debug.LogWarning("ExitActionListener: inputActions not assigned. Assign the project's InputActionAsset in the Inspector or run the editor hookup.");
            return;
        }

        exitAction = inputActions.FindAction(actionPath, throwIfNotFound: false);
        if (exitAction == null)
        {
            Debug.LogWarning($"ExitActionListener: could not find action '{actionPath}' in the assigned InputActionAsset.");
            return;
        }

        exitAction.performed += OnExitPerformed;
        exitAction.Enable();
    }

    private void OnDisable()
    {
        if (exitAction != null)
        {
            exitAction.performed -= OnExitPerformed;
            exitAction.Disable();
        }
    }

    private void OnExitPerformed(InputAction.CallbackContext ctx)
    {
        Debug.Log("ExitActionListener: Exit action performed. Quitting application (editor will stop play mode).");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
