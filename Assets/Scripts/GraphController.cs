using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class GraphController : Graphic
{
    [Header("References")]
    public CarScript carScript;

    [Header("Graph Settings")]
    [Tooltip("The maximum speed (km/h) represented at the top of the graph")]
    public float maxSpeed = 100f;
    [Tooltip("Number of points drawn across the graph")]
    public int resolution = 50;
    [Tooltip("How many seconds of history are visible across the full width of the graph")]
    public float timeWindow = 5f;
    public float lineThickness = 3f;

    private List<float> dataHistory = new List<float>();
    private float sampleTimer = 0f;

    // How often (in seconds) a new sample is pushed into the buffer.
    // e.g. timeWindow = 5s, resolution = 50 -> a sample every 0.1s
    private float SampleInterval => timeWindow / Mathf.Max(1, resolution);

    protected override void Awake()
    {
        base.Awake();
        dataHistory.Clear();
        for (int i = 0; i < resolution; i++)
        {
            dataHistory.Add(0f);
        }
    }

    void Update()
    {
        if (carScript == null) return;

        // Accumulate real time and only sample on a fixed interval,
        // so the graph represents an actual time window instead of
        // "however many frames happened to render".
        sampleTimer += Time.deltaTime;
        if (sampleTimer < SampleInterval) return;
        sampleTimer -= SampleInterval;

        // Get car speed
        float currentSpeed = Mathf.Abs(carScript.CurrentSpeedKmh);

        // Shift history and add new point
        dataHistory.RemoveAt(0);
        dataHistory.Add(currentSpeed);

        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (dataHistory.Count < 2) return;

        float w = rectTransform.rect.width;
        float h = rectTransform.rect.height;

        // Find bottom-left corner offset based on pivot
        float xOffset = rectTransform.pivot.x * w;
        float yOffset = rectTransform.pivot.y * h;

        float xSpacing = w / (dataHistory.Count - 1);

        for (int i = 0; i < dataHistory.Count - 1; i++)
        {
            float normalizedY0 = Mathf.Clamp01(dataHistory[i] / maxSpeed);
            float normalizedY1 = Mathf.Clamp01(dataHistory[i + 1] / maxSpeed);

            // Calculate coordinates starting from the bottom of the container box (-yOffset)
            float x0 = (i * xSpacing) - xOffset;
            float y0 = (normalizedY0 * h) - yOffset;

            float x1 = ((i + 1) * xSpacing) - xOffset;
            float y1 = (normalizedY1 * h) - yOffset;

            Vector2 p0 = new Vector2(x0, y0);
            Vector2 p1 = new Vector2(x1, y1);

            Vector2 dir = (p1 - p0).normalized;
            Vector2 normal = new Vector2(-dir.y, dir.x) * (lineThickness / 2f);

            AddQuad(vh, p0 - normal, p0 + normal, p1 + normal, p1 - normal);
        }
    }

    private void AddQuad(VertexHelper vh, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
    {
        UIVertex v0 = UIVertex.simpleVert; v0.color = color; v0.position = p0;
        UIVertex v1 = UIVertex.simpleVert; v1.color = color; v1.position = p1;
        UIVertex v2 = UIVertex.simpleVert; v2.color = color; v2.position = p2;
        UIVertex v3 = UIVertex.simpleVert; v3.color = color; v3.position = p3;

        int i = vh.currentVertCount;
        vh.AddVert(v0); vh.AddVert(v1); vh.AddVert(v2); vh.AddVert(v3);
        vh.AddTriangle(i, i + 1, i + 2);
        vh.AddTriangle(i + 2, i + 3, i);
    }
}