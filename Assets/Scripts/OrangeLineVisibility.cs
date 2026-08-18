using UnityEngine;

[ExecuteAlways]
public class OrangeLineVisibility : MonoBehaviour
{
    [Header("Orange Line Visibility")]
    [SerializeField] private Color lineColor = new Color(1f, 0.5f, 0f, 1f);
    [SerializeField] private bool forceAlwaysVisible = true;

    private const string ShaderName = "Custom/OrangeLineOverlay";
    private Material overlayMaterial;

    private void Reset()
    {
        ApplyAlwaysVisibleMaterial();
    }

    private void OnEnable()
    {
        ApplyAlwaysVisibleMaterial();
    }

    private void Update()
    {
        if (!forceAlwaysVisible)
            return;

        ApplyAlwaysVisibleMaterial();
    }

    private void ApplyAlwaysVisibleMaterial()
    {
        if (overlayMaterial == null)
        {
            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError("[OrangeLineVisibility] Shader 'Custom/OrangeLineOverlay' was not found. Make sure the shader file is in Assets/Shaders.");
                return;
            }

            overlayMaterial = new Material(shader);
            overlayMaterial.name = "OrangeLineOverlayMaterial";
        }

        overlayMaterial.SetColor("_Color", lineColor);

        if (TryGetComponent(out Renderer renderer))
        {
            renderer.sharedMaterial = overlayMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        if (TryGetComponent(out LineRenderer lineRenderer))
        {
            lineRenderer.material = overlayMaterial;
            lineRenderer.startColor = lineColor;
            lineRenderer.endColor = lineColor;
            lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lineRenderer.receiveShadows = false;
        }
    }
}
