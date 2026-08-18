using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders a 2D minimap HUD showing the full track overview with live car position/rotation
/// and static/dynamic markers (e.g. Red Dot at Finish line, Start marker).
/// </summary>
[ExecuteAlways]
public class MinimapController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The car Transform to track (e.g. 'gaddi go brrrr').")]
    [SerializeField] private Transform carTransform;

    [Tooltip("The RectTransform of the track map image.")]
    [SerializeField] private RectTransform trackMapRect;

    [Tooltip("The RectTransform of the car marker icon.")]
    [SerializeField] private RectTransform carMarkerRect;

    [Tooltip("The RectTransform of the finish marker (red dot).")]
    [SerializeField] private RectTransform finishMarkerRect;

    [Tooltip("The finish target Transform in 3D world space (e.g. Checkpoint_B).")]
    [SerializeField] private Transform finishTargetTransform;

    [Header("World Bounds (Auto-calibrated from track meshes)")]
    [Tooltip("Minimum world X coordinate of the track.")]
    [SerializeField] private float worldMinX = -325.64f;

    [Tooltip("Maximum world X coordinate of the track.")]
    [SerializeField] private float worldMaxX = 301.13f;

    [Tooltip("Minimum world Z coordinate of the track.")]
    [SerializeField] private float worldMinZ = -431.02f;

    [Tooltip("Maximum world Z coordinate of the track.")]
    [SerializeField] private float worldMaxZ = 137.61f;

    private float invRangeX;
    private float invRangeZ;

    private void Awake()
    {
        ComputeInverseRanges();
    }

    private void Start()
    {
        ComputeInverseRanges();
        AutoFindReferences();
        UpdateFinishMarkerPosition();
    }

    private void OnValidate()
    {
        ComputeInverseRanges();
    }

    private void AutoFindReferences()
    {
        if (carTransform == null)
        {
            var carGO = GameObject.Find("gaddi go brrrr");
            if (carGO != null) carTransform = carGO.transform;
        }

        if (finishTargetTransform == null)
        {
            var cpB = GameObject.Find("Checkpoint_B");
            if (cpB != null) finishTargetTransform = cpB.transform;
        }

        if (trackMapRect == null)
        {
            trackMapRect = transform.Find("TrackMap_Image") as RectTransform;
        }

        if (carMarkerRect == null && trackMapRect != null)
        {
            carMarkerRect = trackMapRect.Find("Car_Marker") as RectTransform;
        }

        if (finishMarkerRect == null && trackMapRect != null)
        {
            finishMarkerRect = trackMapRect.Find("Finish_Marker") as RectTransform;
        }
    }

    private void ComputeInverseRanges()
    {
        float rangeX = worldMaxX - worldMinX;
        float rangeZ = worldMaxZ - worldMinZ;
        invRangeX = rangeX > 0.0001f ? 1f / rangeX : 1f / 626.77f;
        invRangeZ = rangeZ > 0.0001f ? 1f / rangeZ : 1f / 568.63f;
    }

    private void LateUpdate()
    {
        if (invRangeX == 0f || invRangeZ == 0f)
            ComputeInverseRanges();

        if (carTransform != null && trackMapRect != null && carMarkerRect != null)
        {
            // Update car marker position
            carMarkerRect.anchoredPosition = WorldToMapPosition(carTransform.position);

            // Update car marker rotation
            float yaw = carTransform.eulerAngles.y;
            carMarkerRect.localRotation = Quaternion.Euler(0f, 0f, -yaw);
        }

        // Keep finish marker positioned
        if (finishMarkerRect != null)
        {
            UpdateFinishMarkerPosition();
        }
    }

    public Vector2 WorldToMapPosition(Vector3 worldPos)
    {
        if (trackMapRect == null) return Vector2.zero;
        if (invRangeX == 0f || invRangeZ == 0f) ComputeInverseRanges();

        float normX = Mathf.Clamp01((worldPos.x - worldMinX) * invRangeX);
        float normZ = Mathf.Clamp01((worldPos.z - worldMinZ) * invRangeZ);

        Vector2 mapSize = trackMapRect.rect.size;
        return new Vector2(
            normX * mapSize.x - mapSize.x * 0.5f,
            normZ * mapSize.y - mapSize.y * 0.5f
        );
    }

    public void UpdateFinishMarkerPosition()
    {
        if (finishMarkerRect == null || trackMapRect == null) return;

        Vector3 finishPos = finishTargetTransform != null ? finishTargetTransform.position : new Vector3(135f, 0.45f, -10.5f);
        finishMarkerRect.anchoredPosition = WorldToMapPosition(finishPos);
    }
}
