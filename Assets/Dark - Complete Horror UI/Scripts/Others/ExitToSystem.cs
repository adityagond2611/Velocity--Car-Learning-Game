using UnityEngine;

namespace Michsky.UI.Dark
{
    public class ExitToSystem : MonoBehaviour
    {
        public void ExitGame()
        {
            Debug.Log("Exit method is working in builds.");
            Application.Quit();

#if UNITY_EDITOR
            // When running inside the Unity Editor, also stop Play Mode so the button behaves like a real quit.
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }
}