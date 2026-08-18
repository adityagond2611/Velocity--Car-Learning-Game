using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class CarScript : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI velocityUI; // displays Speed
    [SerializeField] private TextMeshProUGUI trueVelocityUI; // Displays Vector3 and Forward Velocity
    [SerializeField] private TextMeshProUGUI accelerationUI;
    [SerializeField] private TextMeshProUGUI distanceUI;
    [SerializeField] private TextMeshProUGUI timeUI;

    [Header("Wheel Colliders")]
    [SerializeField] private WheelCollider frontLeftCollider;
    [SerializeField] private WheelCollider frontRightCollider;
    [SerializeField] private WheelCollider rearLeftCollider;
    [SerializeField] private WheelCollider rearRightCollider;

    [Header("Wheel Transforms")]
    [SerializeField] private Transform frontLeftTransform;
    [SerializeField] private Transform frontRightTransform;
    [SerializeField] private Transform rearLeftTransform;
    [SerializeField] private Transform rearRightTransform;

    [Header("Audio")]
    [Tooltip("A single looping engine clip; pitch is shifted by RPM so no se    parate low/high RPM clips are needed")]
    [SerializeField] private AudioClip engineSound;

    // ---------------------------------------------------------------
    // Hardcoded vehicle specification.
    // Loosely modeled on a ~320 hp RWD sports coupe (Supra/Cayman-class):
    // ~1360 kg, 0.32 drag coefficient, 6-speed manual-style gear spread.
    // ---------------------------------------------------------------

    // Mass & aerodynamics
    private const float carMass = 1360f;
    private const float dragCoefficient = 0.32f;
    private const float frontalArea = 2.1f;
    private const float airDensity = 1.225f;
    private const float downforceCoefficient = 4.5f;
    private const float linearDamping = 0.12f;
    private const float angularDamping = 0.4f;
    private static readonly Vector3 centerOfMassOffset = new Vector3(0f, -0.5f, -0.1f);

    // Drivetrain: engine torque curve + gearbox
    private const float idleRPM = 950f;
    private const float peakTorqueRPM = 4600f;
    private const float redlineRPM = 7200f;
    private const float idleTorqueNm = 150f;
    private const float peakTorqueNm = 420f;
    private const float redlineTorqueNm = 210f;

    private readonly float[] gearRatios = { 3.82f, 2.26f, 1.64f, 1.29f, 1.00f, 0.84f };
    private const float reverseGearRatio = 3.28f;
    private const float finalDriveRatio = 3.73f;
    private const float upshiftRPM = 6840f;
    private const float downshiftRPM = 2530f;

    // Brakes & steering
    private const float brakeTorque = 4200f;
    private const float frontBrakeBias = 0.62f;
    private const float maxSteerAngleLowSpeed = 35f;
    private const float minSteerAngleHighSpeed = 8f;
    private const float topSpeedReferenceMs = 69f;

    // Tire grip
    private const float forwardTireStiffness = 2.4f;
    private const float sidewaysTireStiffness = 2.0f;

    // Traction control
    private const float tractionControlSlipThreshold = 0.15f;
    private const float tractionControlGain = 6f;

    // Outward body roll. Real cars lean AWAY from the turn (the outside
    // suspension compresses as the chassis is pushed outward by the
    // apparent centrifugal force). The default WheelCollider setup with a
    // low COM doesn't always produce a visible roll, and any small
    // asymmetry can flip the direction. This applies a small torque
    // around the forward (local Z) axis proportional to steer + speed so
    // the chassis visibly tips outward the way a real car does.
    // Sign convention: SteerInput < 0 (left) -> torque_z < 0 -> right
    // side dips -> outward lean. SteerInput > 0 (right) -> opposite.
    private const float bodyRollCoefficient = 0.005f;
    private const float minRollSpeedMs = 1.5f; // ignore below walking pace so the car doesn't twitch while parked

    // Telemetry cleanup
    private const float speedDeadzone = 0.05f;

    // Engine audio
    private const float minEnginePitch = 0.6f;
    private const float maxEnginePitch = 2.2f;
    private const float minEngineVolume = 0.4f;
    private const float maxEngineVolume = 1.0f;

    // Telemetry UI only needs ~10 Hz; rebuilding TextMeshPro strings every
    // frame was the single most expensive thing in Update.
    private const float uiRefreshInterval = 0.1f;

    public float CurrentSpeedKmh { get; private set; }
    public float CurrentSpeedMs { get; private set; }
    public Vector3 CurrentVelocity { get; private set; }
    public float LocalForwardVelocity { get; private set; }
    public float CurrentAcceleration { get; private set; }
    public float TotalDistanceMeters { get; private set; }
    public float ElapsedTimeSeconds { get; private set; }
    public float MotorInput { get; private set; }
    public float SteerInput { get; private set; }
    public bool IsBraking { get; private set; }
    public float EngineRPM { get; private set; }
    public int CurrentGear { get; private set; }
    public bool IsReversing { get; private set; }

    private Rigidbody rb;
    private AudioSource audioSource;
    private Vector3 previousPosition;
    private float previousSpeedMs;
    private int gearIndex;

    // Cached so ApplyTireFriction() doesn't allocate a new[] every call.
    private WheelCollider[] wheelColliders;
    // Skip the first FixedUpdate's acceleration delta; the rigidbody was
    // just configured in Awake, so v - 0 / dt produces a meaningless spike
    // on frame 1.
    private bool firstPhysicsStep = true;
    private float uiRefreshTimer;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass = centerOfMassOffset;
        rb.mass = carMass;
        rb.linearDamping = linearDamping;
        rb.angularDamping = angularDamping;

        // Interpolate so the visual position is smoothed between
        // FixedUpdate physics steps - without this, the car visibly snaps
        // to each physics tick (the classic "jittery" Unity car).
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        // Continuous Speculative CCD prevents the wheels/chassis from
        // momentarily tunneling on sub-frame collisions, which produces
        // micro-bounces that read as jitter.
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        wheelColliders = new[] { frontLeftCollider, frontRightCollider, rearLeftCollider, rearRightCollider };
        ApplyTireFriction();
        ConfigureWheelSuspension();

        // Bump the WheelCollider solver substeps so the contact model can
        // converge on a 1360 kg RWD car. Default (1) is far too low and
        // produces visible step-stutter under load.
        for (int i = 0; i < wheelColliders.Length; i++)
        {
            wheelColliders[i].ConfigureVehicleSubsteps(1f, 12, 12);
        }

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.clip = engineSound;
        audioSource.loop = true;
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f;

        previousPosition = transform.position;
        previousSpeedMs = 0f;
    }

    private void Start()
    {
        // Only start audio here so it doesn't overlap with the first
        // physics step that's still settling.
        if (engineSound != null)
        {
            audioSource.Play();
        }
    }

    private void Update()
    {
        GetInput();
        UpdateWheelVisuals();

        uiRefreshTimer += Time.deltaTime;
        if (uiRefreshTimer >= uiRefreshInterval)
        {
            uiRefreshTimer = 0f;
            UpdateUI();
        }

        UpdateEngineAudio();
    }

    private void FixedUpdate()
    {
        CalculatePhysicsTelemetry();
        HandleMotor();
        HandleSteering();
        ApplyAerodynamics();
        ApplyBodyRoll();
    }

    private void GetInput()
    {
        // Read vertical input (W/UpArrow = 1, S/DownArrow = -1)
        float vertical = 0f;
        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
                vertical = 1f;
            else if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
                vertical = -1f;
        }
        MotorInput = vertical;

        // Read horizontal input (D/RightArrow = 1, A/LeftArrow = -1)
        float horizontal = 0f;
        if (Keyboard.current != null)
        {
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
                horizontal = 1f;
            else if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
                horizontal = -1f;
        }
        SteerInput = horizontal;

        // Read brake input (Space key)
        IsBraking = Keyboard.current != null && Keyboard.current.spaceKey.isPressed;
    }

    private void ApplyTireFriction()
    {
        WheelFrictionCurve forward = frontLeftCollider.forwardFriction;
        forward.stiffness = forwardTireStiffness;

        WheelFrictionCurve sideways = frontLeftCollider.sidewaysFriction;
        sideways.stiffness = sidewaysTireStiffness;

        for (int i = 0; i < wheelColliders.Length; i++)
        {
            wheelColliders[i].forwardFriction = forward;
            wheelColliders[i].sidewaysFriction = sideways;
        }
    }

    private void ConfigureWheelSuspension()
    {
        // Configure realistic suspension spring and damper for all wheels.
        // Spring: realistic for ~1360 kg car with ~170 mm suspension travel
        // Damper: typical shock absorber for road car
        for (int i = 0; i < wheelColliders.Length; i++)
        {
            JointSpring suspensionSpring = wheelColliders[i].suspensionSpring;
            suspensionSpring.spring = 35000f;   // Spring constant (N/m) - much stiffer than default
            suspensionSpring.damper = 2500f;    // Damping coefficient (N·s/m) - realistic shock absorber
            wheelColliders[i].suspensionSpring = suspensionSpring;
        }
    }

    private void HandleMotor()
    {
        IsReversing = MotorInput < -0.01f;

        float drivenWheelRpm = (Mathf.Abs(rearLeftCollider.rpm) + Mathf.Abs(rearRightCollider.rpm)) * 0.5f;
        float ratioMagnitude = IsReversing ? reverseGearRatio : gearRatios[gearIndex];
        EngineRPM = Mathf.Max(idleRPM, drivenWheelRpm * ratioMagnitude * finalDriveRatio);

        if (IsReversing)
        {
            gearIndex = 0;
        }
        else
        {
            UpdateGear();
        }
        CurrentGear = gearIndex + 1;

        float engineTorque = EvaluateTorqueCurve(EngineRPM);
        float wheelTorque = engineTorque * ratioMagnitude * finalDriveRatio * MotorInput;

        float perWheelTorque = wheelTorque * 0.5f;
        float leftTractionScale = CalculateTractionScale(rearLeftCollider);
        float rightTractionScale = CalculateTractionScale(rearRightCollider);
        rearLeftCollider.motorTorque = perWheelTorque * leftTractionScale;
        rearRightCollider.motorTorque = perWheelTorque * rightTractionScale;

        float totalBrake = IsBraking ? brakeTorque : 0f;
        float frontBrake = totalBrake * frontBrakeBias;
        float rearBrake = totalBrake * (1f - frontBrakeBias);
        frontLeftCollider.brakeTorque = frontBrake;
        frontRightCollider.brakeTorque = frontBrake;
        rearLeftCollider.brakeTorque = rearBrake;
        rearRightCollider.brakeTorque = rearBrake;
    }

    private float CalculateTractionScale(WheelCollider wheel)
    {
        float wheelSurfaceSpeed = Mathf.Abs(wheel.rpm) * 2f * Mathf.PI / 60f * wheel.radius;
        float referenceSpeed = Mathf.Max(CurrentSpeedMs, 0.5f);
        float slip = (wheelSurfaceSpeed - referenceSpeed) / referenceSpeed;

        if (slip <= tractionControlSlipThreshold) return 1f;

        float excessSlip = slip - tractionControlSlipThreshold;
        return Mathf.Clamp01(1f - excessSlip * tractionControlGain);
    }

    private float EvaluateTorqueCurve(float rpm)
    {
        rpm = Mathf.Clamp(rpm, idleRPM, redlineRPM);

        if (rpm <= peakTorqueRPM)
        {
            float t = Mathf.InverseLerp(idleRPM, peakTorqueRPM, rpm);
            return Mathf.Lerp(idleTorqueNm, peakTorqueNm, t);
        }
        else
        {
            float t = Mathf.InverseLerp(peakTorqueRPM, redlineRPM, rpm);
            return Mathf.Lerp(peakTorqueNm, redlineTorqueNm, t);
        }
    }

    private void UpdateGear()
    {
        if (gearIndex < gearRatios.Length - 1 && EngineRPM > upshiftRPM)
        {
            gearIndex++;
        }
        else if (gearIndex > 0 && EngineRPM < downshiftRPM)
        {
            gearIndex--;
        }
    }

    private void HandleSteering()
    {
        float speedT = Mathf.InverseLerp(0f, topSpeedReferenceMs, CurrentSpeedMs);
        float maxAngleAtSpeed = Mathf.Lerp(maxSteerAngleLowSpeed, minSteerAngleHighSpeed, speedT);
        float steerAngle = SteerInput * maxAngleAtSpeed;

        frontLeftCollider.steerAngle = steerAngle;
        frontRightCollider.steerAngle = steerAngle;
    }

    private void ApplyBodyRoll()
    {
        if (CurrentSpeedMs < minRollSpeedMs) return;
        if (Mathf.Abs(SteerInput) < 0.01f) return;

        // ForceMode.Acceleration: torque is treated as angular acceleration
        // (rad/s^2) regardless of mass, so the coefficient is tunable
        // independently of carMass.
        float rollTorque = SteerInput * CurrentSpeedMs * bodyRollCoefficient;
        rb.AddRelativeTorque(0f, 0f, rollTorque, ForceMode.Acceleration);
    }

    private void UpdateWheelVisuals()
    {
        UpdateSingleWheel(frontLeftCollider, frontLeftTransform);
        UpdateSingleWheel(frontRightCollider, frontRightTransform);
        UpdateSingleWheel(rearLeftCollider, rearLeftTransform);
        UpdateSingleWheel(rearRightCollider, rearRightTransform);
    }

    private void UpdateSingleWheel(WheelCollider collider, Transform wheelTransform)
    {
        if (wheelTransform == null) return;

        collider.GetWorldPose(out Vector3 position, out Quaternion rotation);
        wheelTransform.position = position;
        wheelTransform.rotation = rotation;
    }

    private void CalculatePhysicsTelemetry()
    {
        ElapsedTimeSeconds += Time.fixedDeltaTime;

        CurrentVelocity = rb.linearVelocity;
        CurrentSpeedMs = CurrentVelocity.magnitude;
        CurrentSpeedKmh = CurrentSpeedMs * 3.6f;

        LocalForwardVelocity = transform.InverseTransformDirection(CurrentVelocity).z;

        if (Mathf.Abs(CurrentSpeedMs) < speedDeadzone)
        {
            CurrentSpeedMs = 0f;
            CurrentSpeedKmh = 0f;
            LocalForwardVelocity = 0f;
        }

        // First physics step: previousSpeedMs is 0 by construction, so
        // (v - 0) / dt would be a meaningless spike that bleeds into the
        // UI for several frames. Skip the delta and seed previousSpeedMs
        // with the real value instead.
        if (firstPhysicsStep)
        {
            previousSpeedMs = CurrentSpeedMs;
            firstPhysicsStep = false;
        }
        else if (Time.fixedDeltaTime > 0.0001f)
        {
            float rawAcceleration = (CurrentSpeedMs - previousSpeedMs) / Time.fixedDeltaTime;
            CurrentAcceleration = Mathf.Lerp(CurrentAcceleration, rawAcceleration, Time.fixedDeltaTime * 10f);
            previousSpeedMs = CurrentSpeedMs;
        }

        TotalDistanceMeters += Vector3.Distance(transform.position, previousPosition);
        previousPosition = transform.position;
    }

    private void ApplyAerodynamics()
    {
        float speed = rb.linearVelocity.magnitude;
        if (speed < 0.1f) return;

        float dragForceMagnitude = 0.5f * airDensity * dragCoefficient * frontalArea * speed * speed;
        rb.AddForce(-rb.linearVelocity.normalized * dragForceMagnitude);

        float downforceMagnitude = downforceCoefficient * speed * speed;
        rb.AddForce(-transform.up * downforceMagnitude);
    }

    private void UpdateEngineAudio()
    {
        if (audioSource == null || engineSound == null) return;

        float rpmT = Mathf.InverseLerp(idleRPM, redlineRPM, EngineRPM);
        audioSource.pitch = Mathf.Lerp(minEnginePitch, maxEnginePitch, rpmT);

        float throttleT = Mathf.Abs(MotorInput);
        audioSource.volume = Mathf.Lerp(minEngineVolume, maxEngineVolume, throttleT);
    }

    private void UpdateUI()
    {
        if (velocityUI != null)
        {
            velocityUI.text = $"{CurrentSpeedKmh:F1} km/h";
        }

        if (trueVelocityUI != null)
        {
            trueVelocityUI.text = $"{LocalForwardVelocity:F2} m/s";
        }

        if (accelerationUI != null)
        {
            accelerationUI.text = $"{CurrentAcceleration:F2} m/s²";
        }

        if (distanceUI != null)
        {
            if (TotalDistanceMeters >= 1000f)
                distanceUI.text = $"{(TotalDistanceMeters / 1000f):F2} km";
            else
                distanceUI.text = $"{TotalDistanceMeters:F1} m";
        }

        if (timeUI != null)
        {
            int minutes = Mathf.FloorToInt(ElapsedTimeSeconds / 60f);
            int seconds = Mathf.FloorToInt(ElapsedTimeSeconds % 60f);
            int milliseconds = Mathf.FloorToInt((ElapsedTimeSeconds * 100f) % 100f);

            timeUI.text = $"{minutes:00}:{seconds:00}.{milliseconds:00}";
        }
    }
}
