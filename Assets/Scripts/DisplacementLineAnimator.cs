using System.Collections;
using UnityEngine;
using TMPro;

[RequireComponent(typeof(LineRenderer))]
public class DisplacementLineAnimator : MonoBehaviour
{
    public LineRenderer lineRenderer { get; private set; }
    public bool IsAnimating { get; private set; } = false;

    [Header("Line Appearance")]
    public float lineWidth = 0.06f;
    // Increased default multiplier to make the line visibly bolder (current request: 5x thicker than previous 10x -> 50x)
    public float widthMultiplier = 50f; // default multiplier
    public Color lineColor = Color.red; // default to red as requested
    // Keep the line aligned with the track level. This value restores the original position on the ground.
    public float lineVerticalOffset = 0f;
    // Additional vertical offset for the floating label above the raised line.
    public float labelHeight = 0.5f;

    private TextMeshPro displacementLabel;

    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer == null)
            lineRenderer = gameObject.AddComponent<LineRenderer>();

        // Basic setup for LineRenderer so it's visible with default materials
        lineRenderer.positionCount = 2;
        lineRenderer.useWorldSpace = true;
        lineRenderer.startWidth = lineWidth * widthMultiplier;
        lineRenderer.endWidth = lineWidth * widthMultiplier;

        var overlayShader = Shader.Find("Custom/OrangeLineOverlay");
        if (overlayShader != null)
        {
            lineRenderer.material = new Material(overlayShader);
            lineRenderer.material.SetColor("_Color", lineColor);
        }
        else
        {
            lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        }

        lineRenderer.startColor = lineColor;
        lineRenderer.endColor = lineColor;
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
        lineRenderer.enabled = false;
    }

    // Animate a line drawing from pointA to pointB over duration seconds.
    public IEnumerator AnimateLineRoutine(Transform pointA, Transform pointB, float duration, float displayDistanceOverride = -1f)
    {
        if (pointA == null || pointB == null || duration <= 0f)
            yield break;

        IsAnimating = true;
        // Ensure widths are set according to current multiplier in case values were changed at runtime
        lineRenderer.startWidth = lineWidth * widthMultiplier;
        lineRenderer.endWidth = lineWidth * widthMultiplier;
        lineRenderer.enabled = true;

        Vector3 aPos = pointA.position + Vector3.up * lineVerticalOffset;
        Vector3 bPos = pointB.position + Vector3.up * lineVerticalOffset;

        // Create a floating displacement label in world-space at the midpoint, raised above the line.
        CreateDisplacementLabel(((aPos + bPos) * 0.5f) + Vector3.up * labelHeight);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            // linear draw - could be eased here if desired
            Vector3 currentEnd = Vector3.Lerp(aPos, bPos, t);
            lineRenderer.SetPosition(0, aPos);
            lineRenderer.SetPosition(1, currentEnd);

            // Update label: if an override distance is provided, show that constant value (the 'Distance' from UI)
            if (displacementLabel != null)
            {
                if (displayDistanceOverride >= 0f)
                {
                    displacementLabel.text = $"{displayDistanceOverride:F1} m";
                    displacementLabel.transform.position = (aPos + currentEnd) * 0.5f + Vector3.up * labelHeight;
                }
                else
                {
                    float currentLen = Vector3.Distance(aPos, currentEnd);
                    displacementLabel.text = $"{currentLen:F1} m";
                    // keep the label positioned at the midpoint between A and current end
                    displacementLabel.transform.position = (aPos + currentEnd) * 0.5f + Vector3.up * labelHeight;
                }

                // face the main camera if available
                if (Camera.main != null)
                    displacementLabel.transform.rotation = Quaternion.LookRotation(displacementLabel.transform.position - Camera.main.transform.position);
            }

            yield return null;
        }

        // Ensure final position and final label value
        lineRenderer.SetPosition(0, aPos);
        lineRenderer.SetPosition(1, bPos);
        if (displacementLabel != null)
        {
            if (displayDistanceOverride >= 0f)
            {
                displacementLabel.text = $"{displayDistanceOverride:F1} m";
            }
            else
            {
                float finalLen = Vector3.Distance(aPos, bPos);
                displacementLabel.text = $"{finalLen:F1} m";
            }
            displacementLabel.transform.position = (aPos + bPos) * 0.5f + Vector3.up * labelHeight;
            if (Camera.main != null)
                displacementLabel.transform.rotation = Quaternion.LookRotation(displacementLabel.transform.position - Camera.main.transform.position);
        }

        // Short pause so the label is visible at final length, then remove it
        yield return new WaitForSeconds(0.05f);

        if (displacementLabel != null)
            Destroy(displacementLabel.gameObject);

        IsAnimating = false;
        yield break;
    }

    private void CreateDisplacementLabel(Vector3 worldPos)
    {
        // Clean up any existing
        if (displacementLabel != null)
            Destroy(displacementLabel.gameObject);

        var go = new GameObject("DisplacementLabel");
        go.transform.SetParent(transform, true);
        go.transform.position = worldPos;

        displacementLabel = go.AddComponent<TextMeshPro>();
        displacementLabel.text = "0.0 m";
        displacementLabel.fontSize = 4f;
        displacementLabel.alignment = TextAlignmentOptions.Center;
        displacementLabel.color = lineColor;
        displacementLabel.enableCulling = false;
        // Extra visual clarity
        displacementLabel.fontStyle = FontStyles.Bold;
    }

    public void ClearLine()
    {
        if (lineRenderer != null)
            lineRenderer.enabled = false;

        if (displacementLabel != null)
        {
            Destroy(displacementLabel.gameObject);
            displacementLabel = null;
        }
    }
}
