using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIStyleManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI velocityUI;
    [SerializeField] private TextMeshProUGUI trueVelocityUI;
    [SerializeField] private TextMeshProUGUI accelerationUI;
    [SerializeField] private TextMeshProUGUI distanceUI;
    [SerializeField] private TextMeshProUGUI timeUI;

    [Header("Font References")]
    [SerializeField] private TMP_FontAsset headerFont;
    [SerializeField] private TMP_FontAsset valueFont;

    [Header("Style Settings")]
    [SerializeField] private int headerFontSize = 26;
    [SerializeField] private int valueFontSize = 40;
    [SerializeField] private float backgroundAlpha = 0.65f;

    private void Start()
    {
        ApplyUIStyles();
    }

    private void ApplyUIStyles()
    {
        // Style with black transparent background and color-coded text
        ApplyStyleWithBackground(velocityUI, valueFontSize, new Color(0.2f, 0.6f, 1f, 1f), valueFont); // Blue text

        ApplyStyleWithBackground(trueVelocityUI, headerFontSize, new Color(0.4f, 0.8f, 1f, 1f), headerFont); // Light Blue text

        ApplyStyleWithBackground(accelerationUI, headerFontSize, new Color(1f, 0.6f, 0.2f, 1f), headerFont); // Orange text

        ApplyStyleWithBackground(distanceUI, headerFontSize, new Color(0.2f, 0.9f, 0.4f, 1f), headerFont); // Green text

        ApplyStyleWithBackground(timeUI, headerFontSize, new Color(0.8f, 0.4f, 1f, 1f), headerFont); // Purple text
    }

    private void ApplyStyleWithBackground(TextMeshProUGUI textElement, int fontSize, Color textColor, TMP_FontAsset font)
    {
        if (textElement == null) return;

        // Text styling
        textElement.fontSize = fontSize;
        textElement.color = textColor;
        textElement.fontStyle = FontStyles.Bold;
        textElement.alignment = TextAlignmentOptions.Center;

        // Apply custom font if assigned
        if (font != null)
        {
            textElement.font = font;
        }

        // Outline for better visibility
        textElement.outlineWidth = 0.3f;
        textElement.outlineColor = new Color(0, 0, 0, 1f);

        // Character spacing for elegance
        textElement.characterSpacing = 2;
        textElement.lineSpacing = 100;

        // Add BLACK TRANSPARENT background panel
        AddBackgroundPanel(textElement.gameObject);

        // Add layout element for consistent sizing
        LayoutElement layout = textElement.gameObject.GetComponent<LayoutElement>();
        if (layout == null)
        {
            layout = textElement.gameObject.AddComponent<LayoutElement>();
        }
        layout.preferredWidth = 220;
        layout.preferredHeight = 75;
    }

    private void AddBackgroundPanel(GameObject uiElement)
    {
        // Check if Image component already exists
        Image bgImage = uiElement.GetComponent<Image>();
        if (bgImage != null)
        {
            // Update to BLACK TRANSPARENT background
            bgImage.color = new Color(0f, 0f, 0f, backgroundAlpha);
            return;
        }

        // Create new background image with BLACK TRANSPARENT color
        bgImage = uiElement.AddComponent<Image>();
        bgImage.color = new Color(0f, 0f, 0f, backgroundAlpha);
        bgImage.raycastTarget = false;

        // Add subtle dark outline
        Outline outline = uiElement.GetComponent<Outline>();
        if (outline == null)
        {
            outline = uiElement.AddComponent<Outline>();
        }
        outline.effectColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        outline.effectDistance = new Vector2(2, 2);

        // Add shadow for depth
        Shadow shadow = uiElement.GetComponent<Shadow>();
        if (shadow == null)
        {
            shadow = uiElement.AddComponent<Shadow>();
        }
        shadow.effectColor = new Color(0, 0, 0, 0.8f);
        shadow.effectDistance = new Vector2(2, -2);
    }
}
