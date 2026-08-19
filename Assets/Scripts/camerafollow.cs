using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("Drag your Car Transform here")]
    [SerializeField] private Transform target;

    [Header("Position Offset")]
    [Tooltip("X = Left/Right, Y = Height, Z = Distance Behind (negative number)")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 3.5f, -7.0f);

    [Header("Rotation Offset (Pitch / Tilt)")]
    [Tooltip("Tilt angle looking down at the car (e.g. 10 to 15 degrees)")]
    [SerializeField] private float pitchAngle = 12f;

    private void LateUpdate()
    {
        if (target == null) return;

        // 100% Rigid locked position relative to car rotation (ZERO distance lag)
        transform.position = target.position + target.rotation * offset;

        // 100% Rigid locked rotation with downward pitch
        transform.rotation = target.rotation * Quaternion.Euler(pitchAngle, 0f, 0f);
    }
}