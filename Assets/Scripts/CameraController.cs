using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Camera Follow")]
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 followOffset = new Vector3(0, 2f, -5f);

    [Header("Acceleration Spike Detection")]
    [Tooltip("Minimum m/s² to trigger a camera kick. Tune by playtesting — watch Console for acceleration values.")]
    [SerializeField] private float accelSpikeThreshold = 8f;

    [Header("Camera Kick Effect")]
    [Tooltip("How far back (in world units) the camera snaps when acceleration spikes.")]
    [SerializeField] private float kickDistance = 1.5f;
    [Tooltip("How long (in seconds) for the camera to ease back to its resting position.")]
    [SerializeField] private float returnTime = 0.6f;
    [Tooltip("Minimum seconds between kicks to prevent jitter/spam.")]
    [SerializeField] private float kickCooldown = 0.3f;

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;

    private Rigidbody carRigidbody;
    private CarScript carScript;

    private float previousSpeed = 0f;
    private float currentAccel = 0f;
    private float baseDistance;
    private float currentDistance;
    private float distanceVelocity = 0f; // for SmoothDamp
    private float timeSinceLastKick = float.MaxValue;
    private bool isKicking = false;

    private void Start()
    {
        // Find the car if not assigned
        if (target == null)
        {
            GameObject carGO = GameObject.Find("Car") ?? GameObject.FindGameObjectWithTag("Player");
            if (carGO != null)
            {
                target = carGO.transform;
                carRigidbody = carGO.GetComponent<Rigidbody>();
                carScript = carGO.GetComponent<CarScript>();
            }
            else
            {
                Debug.LogError("[CameraController] No target car found. Assign it in the Inspector.");
                enabled = false;
                return;
            }
        }
        else
        {
            carRigidbody = target.GetComponent<Rigidbody>();
            carScript = target.GetComponent<CarScript>();
        }

        baseDistance = followOffset.z;
        currentDistance = baseDistance;
        timeSinceLastKick = float.MaxValue;
    }

    private void Update()
    {
        if (target == null || carScript == null) return;

        // Calculate instantaneous acceleration
        float currentSpeed = carScript.CurrentSpeedMs;
        currentAccel = (currentSpeed - previousSpeed) / Time.deltaTime;
        previousSpeed = currentSpeed;

        // Update cooldown timer
        if (timeSinceLastKick < kickCooldown)
        {
            timeSinceLastKick += Time.deltaTime;
        }

        // Detect acceleration spike
        if (currentAccel > accelSpikeThreshold && timeSinceLastKick >= kickCooldown)
        {
            TriggerKick();
        }

        // Ease camera distance back toward target
        float targetDistance = isKicking ? baseDistance + kickDistance : baseDistance;
        currentDistance = Mathf.SmoothDamp(currentDistance, targetDistance, ref distanceVelocity, returnTime);

        // Check if return is complete
        if (isKicking && Mathf.Abs(currentDistance - baseDistance) < 0.01f)
        {
            isKicking = false;
            currentDistance = baseDistance;
        }

        // Debug readout
        if (showDebugInfo && Time.frameCount % 10 == 0)
        {
            Debug.Log($"[CameraController] Accel: {currentAccel:F2} m/s² | Threshold: {accelSpikeThreshold} | Kicking: {isKicking} | Distance: {currentDistance:F2}");
        }
    }

    private void LateUpdate()
    {
        if (target == null) return;

        // Update camera position with current distance offset
        Vector3 offset = new Vector3(followOffset.x, followOffset.y, -currentDistance);
        transform.position = target.position + offset;
        transform.LookAt(target.position + Vector3.up * 1f);
    }

    private void TriggerKick()
    {
        isKicking = true;
        timeSinceLastKick = 0f;

        if (showDebugInfo)
        {
            Debug.Log($"[CameraController] **KICK TRIGGERED** at {currentAccel:F2} m/s²");
        }
    }
}




