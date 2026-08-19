using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// Animates a dynamic educational Displacement Vector on the track between START and FINISH checkpoints.
/// Features:
/// - Crisp neon-green vector line with perpendicular end-caps.
/// - 7x scaled Text labels in all dimensions for high-visibility from overhead cameras.
/// - Displacement text placed to the RIGHT of the line so the full line remains unobstructed.
/// - Large bold "START", "FINISH", and "DISPLACEMENT \n {X} m" badges.
/// - Automatic billboard camera alignment so text always faces the cutscene camera.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class DisplacementLineAnimator : MonoBehaviour
{
    [Header("Vector Line Appearance")]
    [Tooltip("Base thickness of the displacement vector shaft.")]
    [SerializeField] private float lineWidth = 3.5f;

    [Tooltip("Color of the displacement line (Matches reference neon-green).")]
    [SerializeField] private Color vectorColor = new Color(0.27f, 0.88f, 0.35f, 1.0f); // Vibrant Neon Green #46E059

    [Tooltip("Vertical height offset above ground to stay clearly above track and fences.")]
    [SerializeField] private float verticalOffset = 4.0f;

    [Header("End-Cap / Tick Settings")]
    [Tooltip("Length of perpendicular tick marks at Start and Finish.")]
    [SerializeField] private float tickLength = 12.0f;

    [Header("Label & Badge Settings (7x Scaled)")]
    [Tooltip("Global scale multiplier for all labels in 3D space (7x scaled).")]
    [SerializeField] private float textScale = 7.0f;

    [Tooltip("Lateral offset to position the displacement text to the RIGHT of the line.")]
    [SerializeField] private float lateralOffsetRight = 24.0f;

    [Tooltip("Vertical height of floating labels above the line.")]
    [SerializeField] private float labelHeightOffset = 4.5f;

    [Tooltip("Base font size for START and FINISH labels.")]
    [SerializeField] private float checkpointFontSize = 10.0f;

    [Tooltip("Base font size for the DISPLACEMENT measurement.")]
    [SerializeField] private float displacementFontSize = 9.0f;

    // Component References
    public LineRenderer lineRenderer { get; private set; }
    public bool IsAnimating { get; private set; } = false;

    private LineRenderer startTickRenderer;
    private LineRenderer endTickRenderer;
    private GameObject startLabelObject;
    private GameObject endLabelObject;
    private GameObject displacementLabelObject;
    private TextMeshPro displacementText;
    private TextMeshPro startText;
    private TextMeshPro endText;

    private void Awake()
    {
        SetupMainLineRenderer();
        SetupTickRenderers();
    }

    private void SetupMainLineRenderer()
    {
        lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer == null)
            lineRenderer = gameObject.AddComponent<LineRenderer>();

        lineRenderer.positionCount = 2;
        lineRenderer.useWorldSpace = true;
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;

        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
        lineRenderer.material = new Material(shader);
        lineRenderer.material.color = vectorColor;
        lineRenderer.startColor = vectorColor;
        lineRenderer.endColor = vectorColor;
        lineRenderer.sortingOrder = 500;
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
        lineRenderer.enabled = false;
    }

    private void SetupTickRenderers()
    {
        var startTickGO = new GameObject("StartTickRenderer");
        startTickGO.transform.SetParent(transform, false);
        startTickRenderer = startTickGO.AddComponent<LineRenderer>();
        ConfigureTickRenderer(startTickRenderer);

        var endTickGO = new GameObject("EndTickRenderer");
        endTickGO.transform.SetParent(transform, false);
        endTickRenderer = endTickGO.AddComponent<LineRenderer>();
        ConfigureTickRenderer(endTickRenderer);
    }

    private void ConfigureTickRenderer(LineRenderer lr)
    {
        lr.positionCount = 2;
        lr.useWorldSpace = true;
        lr.startWidth = lineWidth * 1.15f;
        lr.endWidth = lineWidth * 1.15f;

        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
        lr.material = new Material(shader);
        lr.material.color = vectorColor;
        lr.startColor = vectorColor;
        lr.endColor = vectorColor;
        lr.sortingOrder = 500;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.enabled = false;
    }

    /// <summary>
    /// Animates drawing the displacement vector from Point A (START) to Point B (FINISH).
    /// </summary>
    public IEnumerator AnimateLineRoutine(Transform pointA, Transform pointB, float duration, float displayDistanceOverride = -1f)
    {
        if (pointA == null || pointB == null || duration <= 0f)
            yield break;

        IsAnimating = true;

        Vector3 aPos = pointA.position + Vector3.up * verticalOffset;
        Vector3 bPos = pointB.position + Vector3.up * verticalOffset;
        Vector3 direction = (bPos - aPos).normalized;
        // Right-hand perpendicular vector relative to direction pointing up
        Vector3 rightVector = Vector3.Cross(Vector3.up, direction).normalized;
        float totalDisplacement = Vector3.Distance(aPos, bPos);

        // Prepare Renderers
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.material.color = vectorColor;
        lineRenderer.startColor = vectorColor;
        lineRenderer.endColor = vectorColor;
        lineRenderer.enabled = true;

        startTickRenderer.enabled = true;
        UpdateTick(startTickRenderer, aPos, rightVector, tickLength);

        endTickRenderer.enabled = true;

        // Create START, FINISH, and DISPLACEMENT Labels (positioned on the right side of the line)
        CreateStartLabel(aPos - direction * 8f + rightVector * (lateralOffsetRight * 0.5f));
        CreateEndLabel(bPos + direction * 8f + rightVector * (lateralOffsetRight * 0.5f));
        CreateDisplacementLabel((aPos + bPos) * 0.5f + rightVector * lateralOffsetRight + Vector3.up * labelHeightOffset);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // Smooth cubic ease out
            float easeT = 1f - Mathf.Pow(1f - t, 3f);

            Vector3 currentTip = Vector3.Lerp(aPos, bPos, easeT);
            float currentDist = totalDisplacement * easeT;

            // Update main line
            lineRenderer.SetPosition(0, aPos);
            lineRenderer.SetPosition(1, currentTip);

            // Update end tick at current tip
            UpdateTick(endTickRenderer, currentTip, rightVector, tickLength);

            // Update displacement numeric label position on the right of the line
            if (displacementText != null && displacementLabelObject != null)
            {
                displacementText.text = $"<color=#{ColorUtility.ToHtmlStringRGB(vectorColor)}><b>DISPLACEMENT</b>\n{currentDist:F0} m</color>";
                displacementLabelObject.transform.position = (aPos + currentTip) * 0.5f + rightVector * lateralOffsetRight + Vector3.up * labelHeightOffset;
            }

            UpdateBillboards();

            yield return null;
        }

        // Lock final state
        lineRenderer.SetPosition(0, aPos);
        lineRenderer.SetPosition(1, bPos);
        UpdateTick(endTickRenderer, bPos, rightVector, tickLength);

        if (displacementText != null && displacementLabelObject != null)
        {
            displacementText.text = $"<color=#{ColorUtility.ToHtmlStringRGB(vectorColor)}><b>DISPLACEMENT</b>\n{totalDisplacement:F0} m</color>";
            displacementLabelObject.transform.position = (aPos + bPos) * 0.5f + rightVector * lateralOffsetRight + Vector3.up * labelHeightOffset;
        }

        UpdateBillboards();

        yield return new WaitForSeconds(0.1f);
        IsAnimating = false;
    }

    private void UpdateTick(LineRenderer lr, Vector3 center, Vector3 normal, float length)
    {
        if (lr == null) return;
        Vector3 halfOffset = normal * (length * 0.5f);
        lr.SetPosition(0, center - halfOffset);
        lr.SetPosition(1, center + halfOffset);
    }

    private void CreateStartLabel(Vector3 pos)
    {
        if (startLabelObject != null) Destroy(startLabelObject);

        startLabelObject = new GameObject("StartLabel");
        startLabelObject.transform.SetParent(transform, true);
        startLabelObject.transform.position = pos + Vector3.up * labelHeightOffset;
        startLabelObject.transform.localScale = Vector3.one * textScale;

        startText = startLabelObject.AddComponent<TextMeshPro>();
        startText.text = "<b>START</b>";
        startText.fontSize = checkpointFontSize;
        startText.alignment = TextAlignmentOptions.Center;
        startText.color = Color.white;
        startText.fontStyle = FontStyles.Bold;
        startText.textWrappingMode = TextWrappingModes.NoWrap;
        startText.outlineWidth = 0.4f;
        startText.outlineColor = new Color32(0, 0, 0, 255);

        var mr = startLabelObject.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            mr.sortingOrder = 500;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    private void CreateEndLabel(Vector3 pos)
    {
        if (endLabelObject != null) Destroy(endLabelObject);

        endLabelObject = new GameObject("EndLabel");
        endLabelObject.transform.SetParent(transform, true);
        endLabelObject.transform.position = pos + Vector3.up * labelHeightOffset;
        endLabelObject.transform.localScale = Vector3.one * textScale;

        endText = endLabelObject.AddComponent<TextMeshPro>();
        endText.text = "<b>FINISH</b>";
        endText.fontSize = checkpointFontSize;
        endText.alignment = TextAlignmentOptions.Center;
        endText.color = Color.white;
        endText.fontStyle = FontStyles.Bold;
        endText.textWrappingMode = TextWrappingModes.NoWrap;
        endText.outlineWidth = 0.4f;
        endText.outlineColor = new Color32(0, 0, 0, 255);

        var mr = endLabelObject.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            mr.sortingOrder = 500;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    private void CreateDisplacementLabel(Vector3 pos)
    {
        if (displacementLabelObject != null) Destroy(displacementLabelObject);

        displacementLabelObject = new GameObject("DisplacementLabel");
        displacementLabelObject.transform.SetParent(transform, true);
        displacementLabelObject.transform.position = pos;
        displacementLabelObject.transform.localScale = Vector3.one * textScale;

        displacementText = displacementLabelObject.AddComponent<TextMeshPro>();
        displacementText.text = $"<color=#{ColorUtility.ToHtmlStringRGB(vectorColor)}><b>DISPLACEMENT</b>\n0 m</color>";
        displacementText.fontSize = displacementFontSize;
        displacementText.alignment = TextAlignmentOptions.Center;
        displacementText.color = vectorColor;
        displacementText.fontStyle = FontStyles.Bold;
        displacementText.textWrappingMode = TextWrappingModes.NoWrap;
        displacementText.outlineWidth = 0.4f;
        displacementText.outlineColor = new Color32(5, 10, 15, 255);

        var mr = displacementLabelObject.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            mr.sortingOrder = 500;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    private void UpdateBillboards()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Quaternion camRot = cam.transform.rotation;

        if (startLabelObject != null)
        {
            startLabelObject.transform.rotation = camRot;
            startLabelObject.transform.localScale = Vector3.one * textScale;
        }

        if (endLabelObject != null)
        {
            endLabelObject.transform.rotation = camRot;
            endLabelObject.transform.localScale = Vector3.one * textScale;
        }

        if (displacementLabelObject != null)
        {
            displacementLabelObject.transform.rotation = camRot;
            displacementLabelObject.transform.localScale = Vector3.one * textScale;
        }
    }

    private void LateUpdate()
    {
        if (lineRenderer != null && lineRenderer.enabled)
        {
            UpdateBillboards();
        }
    }

    public void ClearLine()
    {
        if (lineRenderer != null) lineRenderer.enabled = false;
        if (startTickRenderer != null) startTickRenderer.enabled = false;
        if (endTickRenderer != null) endTickRenderer.enabled = false;

        if (startLabelObject != null)
        {
            Destroy(startLabelObject);
            startLabelObject = null;
        }

        if (endLabelObject != null)
        {
            Destroy(endLabelObject);
            endLabelObject = null;
        }

        if (displacementLabelObject != null)
        {
            Destroy(displacementLabelObject);
            displacementLabelObject = null;
        }

        IsAnimating = false;
    }
}
