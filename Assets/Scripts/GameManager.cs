using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class GameManager : MonoBehaviour
{
    private const string DefaultSceneName = "Velocity_C1";

    private void Awake()
    {
        AutoBindContinueButton();
    }

    private void AutoBindContinueButton()
    {
        Button targetButton = null;

        var velocityButtonObject = GameObject.Find("Canvas/Main Panels/Story/Content/Chapters/Content/Chapter Area/List/VELOCITY");
        if (velocityButtonObject != null)
            targetButton = velocityButtonObject.GetComponent<Button>();

        if (targetButton == null)
        {
            foreach (var button in Object.FindObjectsOfType<Button>(true))
            {
                if (button == null)
                    continue;

                bool nameMatches = button.name.Contains("VELOCITY", System.StringComparison.OrdinalIgnoreCase)
                    || button.name.Contains("Continue", System.StringComparison.OrdinalIgnoreCase);
                var textComponent = button.GetComponentInChildren<TextMeshProUGUI>();
                bool textMatches = textComponent != null && !string.IsNullOrWhiteSpace(textComponent.text)
                    && (textComponent.text.Contains("VELOCITY", System.StringComparison.OrdinalIgnoreCase)
                        || textComponent.text.Contains("Continue", System.StringComparison.OrdinalIgnoreCase));

                if (nameMatches || textMatches)
                {
                    targetButton = button;
                    break;
                }
            }
        }

        if (targetButton == null)
            return;

        targetButton.onClick.AddListener(() => SwitchScene(DefaultSceneName));
    }

    public void SwitchScene(string scene)
    {
        if (string.IsNullOrWhiteSpace(scene))
            scene = DefaultSceneName;

        scene = scene.Trim();
        if (scene.Equals("Velocity C1", System.StringComparison.OrdinalIgnoreCase))
            scene = "Velocity_C1";

        if (!scene.EndsWith(".unity", System.StringComparison.OrdinalIgnoreCase))
        {
            string underscoreVersion = scene.Replace(' ', '_');
            if (Application.CanStreamedLevelBeLoaded(underscoreVersion) || Application.CanStreamedLevelBeLoaded(scene))
            {
                SceneManager.LoadScene(underscoreVersion);
                return;
            }
        }

        SceneManager.LoadScene(scene);
    }
}
