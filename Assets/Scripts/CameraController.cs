using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

/// <summary>
/// Professional vehicle camera controller designed for racing/driving games.
/// Seamlessly integrates with Unity Cinemachine 3.x and standalone Camera setups:
/// - 100% Unity New Input System (Keyboard.current / Mouse.current) compatible.
/// - Full interactive Mouse Controls: 360° orbit look, vertical pitch tilt, scroll wheel zoom, and auto-recenter.
/// - Works directly on CinemachineCamera virtual cameras or standalone Camera GameObjects.
/// - Allows CinemachineBrain cutscenes (e.g. lesson finish / replay cameras) to blend smoothly without conflicts.
/// - Dynamic speed-dependent Field of View (FOV) and follow distance.
/// - Velocity-aligned heading interpolation for cinematic drift and slide framing.
/// - Acceleration kickback and braking dive G-force effects.
/// - Smart reverse gear detection and rear-view auto-orientation.
/// - Obstacle collision avoidance (SphereCast spring-arm) to prevent clipping through track geometry.
/// - Subtle high-speed micro-turbulence/chassis rumble.
/// - Instant snap-to-target on vehicle reset or teleport.
/// </summary>
[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
public class CameraController : MonoBehaviour
{
    // ───────────────────────── Enums & Profiles ─────────────────────────
    public enum MouseActivationMode
    {
        AlwaysActive,
        HoldRightClick,
        HoldLeftClick,
        ClickAndDrag
    }

    [System.Serializable]
    public struct CameraProfile
    {
        public string profileName;
        [Tooltip("Follow distance behind the car.")]
        public float distance;
        [Tooltip("Follow height above the car.")]
        public float height;
        [Tooltip("Target look-at height on the car.")]
        public float lookAtHeight;
        [Tooltip("Look ahead distance in front of the car.")]
        public float lookAhead;
        [Tooltip("Base field of view.")]
        public float baseFOV;
        [Tooltip("Max field of view at top speed.")]
        public float maxFOV;

        public CameraProfile(string name, float dist, float h, float lookH, float lookA, float minFov, float maxFov)
        {
            profileName = name;
            distance = dist;
            height = h;
            lookAtHeight = lookH;
            lookAhead = lookA;
            baseFOV = minFov;
            maxFOV = maxFov;
        }
    }

    // ───────────────────────── Inspector Fields ─────────────────────────
    [Header("Target Vehicle")]
    [Tooltip("Target car Transform. Auto-finds 'gaddi go brrrr', 'Car', or Player tag if empty.")]
    [SerializeField] private Transform target;

    [Tooltip("Target car Rigidbody for physics telemetry.")]
    [SerializeField] private Rigidbody carRigidbody;

    [Tooltip("Target CarScript for gear, speed, and acceleration data.")]
    [SerializeField] private CarScript carScript;

    [Header("--- Mouse Controls (Orbit Look & Zoom) ---")]
    [Tooltip("Enable mouse look / orbit around the vehicle.")]
    [SerializeField] private bool enableMouseLook = true;

    [Tooltip("Activation mode for mouse look: HoldRightClick (recommended), AlwaysActive, HoldLeftClick, or ClickAndDrag.")]
    [SerializeField] private MouseActivationMode mouseActivation = MouseActivationMode.HoldRightClick;

    [Tooltip("Horizontal mouse look sensitivity (Yaw).")]
    [SerializeField] private float mouseSensitivityX = 2.5f;

    [Tooltip("Vertical mouse look sensitivity (Pitch).")]
    [SerializeField] private float mouseSensitivityY = 2.0f;

    [Tooltip("Invert vertical mouse look.")]
    [SerializeField] private bool invertMouseY = false;

    [Tooltip("Minimum pitch angle looking down from above (degrees).")]
    [SerializeField] private float minPitchAngle = -15f;

    [Tooltip("Maximum pitch angle looking up from below (degrees).")]
    [SerializeField] private float maxPitchAngle = 75f;

    [Tooltip("Enable mouse scroll wheel to zoom in and out.")]
    [SerializeField] private bool enableScrollZoom = true;

    [Tooltip("Scroll zoom sensitivity.")]
    [SerializeField] private float scrollZoomSensitivity = 1.2f;

    [Tooltip("Minimum follow distance when zoomed in.")]
    [SerializeField] private float minZoomDistance = 2.5f;

    [Tooltip("Maximum follow distance when zoomed out.")]
    [SerializeField] private float maxZoomDistance = 14.0f;

    [Tooltip("Seconds of no mouse input before camera smoothly re-centers behind the car (set 0 to disable).")]
    [SerializeField] private float autoRecenterDelay = 1.5f;

    [Tooltip("Smooth time when auto-recentering behind the car.")]
    [SerializeField] private float autoRecenterSmoothTime = 0.30f;

    [Tooltip("Automatically trigger re-center when player starts accelerating/driving forward.")]
    [SerializeField] private bool autoRecenterOnDrive = true;

    [Header("Camera Profiles & Views")]
    [Tooltip("List of switchable camera view profiles (press C to cycle).")]
    [SerializeField] private CameraProfile[] cameraProfiles = new CameraProfile[]
    {
        new CameraProfile("Chase (Default)", 5.2f, 2.1f, 1.0f, 2.5f, 62f, 76f),
        new CameraProfile("Close Chase", 4.0f, 1.6f, 0.9f, 2.0f, 65f, 80f),
        new CameraProfile("Far Chase", 6.8f, 2.8f, 1.1f, 3.0f, 58f, 72f),
        new CameraProfile("Hood / Bumper", 1.8f, 1.1f, 0.8f, 5.0f, 70f, 85f)
    };
    [SerializeField] private int activeProfileIndex = 0;

    [Header("Smooth Damping & Follow Dynamics")]
    [Tooltip("Position smoothing time (seconds). Lower = snappier, higher = smoother.")]
    [Range(0.01f, 0.5f)]
    [SerializeField] private float positionSmoothTime = 0.10f;

    [Tooltip("Horizontal rotation (yaw) smoothing time.")]
    [Range(0.01f, 0.5f)]
    [SerializeField] private float yawSmoothTime = 0.08f;

    [Tooltip("Vertical rotation (pitch) smoothing time.")]
    [Range(0.01f, 0.5f)]
    [SerializeField] private float pitchSmoothTime = 0.12f;

    [Tooltip("Weight of velocity direction vs chassis forward during drifts/slides (0 = chassis only, 1 = velocity only).")]
    [Range(0f, 1f)]
    [SerializeField] private float velocityHeadingWeight = 0.50f;

    [Tooltip("Minimum speed (m/s) before velocity heading blending kicks in.")]
    [SerializeField] private float minSpeedForVelocityHeading = 2.0f;

    [Header("Dynamic Speed FOV & Distance")]
    [Tooltip("Dynamically widen FOV and extend distance as speed increases.")]
    [SerializeField] private bool enableDynamicFOV = true;

    [Tooltip("Speed in km/h at which maximum dynamic FOV/distance is reached.")]
    [SerializeField] private float topSpeedReferenceKmh = 140f;

    [Tooltip("Maximum additional distance pulled back at top speed.")]
    [SerializeField] private float maxSpeedDistanceOffset = 1.0f;

    [Tooltip("FOV smoothing time.")]
    [SerializeField] private float fovSmoothTime = 0.20f;

    [Header("Acceleration Kickback & Braking Dive")]
    [Tooltip("Minimum m/s² acceleration spike to trigger kickback effect.")]
    [SerializeField] private float accelSpikeThreshold = 7.0f;

    [Tooltip("How far back (in meters) the camera kicks on hard acceleration.")]
    [SerializeField] private float kickDistance = 1.2f;

    [Tooltip("Downward pitch angle kick (degrees) on hard acceleration.")]
    [SerializeField] private float kickPitchAngle = 1.5f;

    [Tooltip("Time (in seconds) for camera kick to ease back to rest position.")]
    [SerializeField] private float returnTime = 0.5f;

    [Tooltip("Minimum seconds between consecutive kicks.")]
    [SerializeField] private float kickCooldown = 0.35f;

    [Tooltip("How far forward (in meters) the camera dives on heavy braking.")]
    [SerializeField] private float brakeDiveDistance = 0.5f;

    [Header("Smart Reverse View")]
    [Tooltip("Automatically orient camera backward when car is reversing.")]
    [SerializeField] private bool enableSmartReverse = true;

    [Tooltip("Speed threshold (m/s) to trigger reverse camera orientation.")]
    [SerializeField] private float reverseSpeedThreshold = 1.5f;

    [Tooltip("Smooth time when transitioning into/out of reverse view.")]
    [SerializeField] private float reverseTransitionTime = 0.30f;

    [Header("Obstacle Collision Avoidance (Spring Arm)")]
    [Tooltip("Prevent camera from clipping into walls, barriers, or terrain.")]
    [SerializeField] private bool enableCollisionAvoidance = true;

    [Tooltip("Radius of collision check sphere.")]
    [SerializeField] private float collisionRadius = 0.22f;

    [Tooltip("Layers to check for collisions.")]
    [SerializeField] private LayerMask collisionLayers = ~0;

    [Tooltip("Minimum distance camera can compress to target.")]
    [SerializeField] private float minCollisionDistance = 0.8f;

    [Tooltip("Smooth time when extending back out after a collision.")]
    [SerializeField] private float collisionReturnSmoothTime = 0.15f;

    [Header("High-Speed Micro Shake")]
    [Tooltip("Enable subtle vibration at high speeds.")]
    [SerializeField] private bool enableHighSpeedShake = true;

    [Tooltip("Speed (km/h) above which camera shake begins.")]
    [SerializeField] private float shakeMinSpeedKmh = 65f;

    [Tooltip("Maximum shake intensity at top speed.")]
    [SerializeField] private float maxShakeIntensity = 0.035f;

    [Tooltip("Frequency of vibration noise.")]
    [SerializeField] private float shakeFrequency = 22f;

    [Header("Cinemachine & Cutscene Handling")]
    [Tooltip("Optional reference to CinemachineCamera. Auto-detected if present on this GameObject.")]
    [SerializeField] private CinemachineCamera cinemachineCamera;

    [Tooltip("Optional reference to CinemachineBrain on MainCamera to detect active cutscene blends.")]
    [SerializeField] private CinemachineBrain cinemachineBrain;

    [Tooltip("Auto-snap camera if car teleports more than this distance in a single frame.")]
    [SerializeField] private float teleportThreshold = 8.0f;

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;

    // ───────────────────────── Runtime State ─────────────────────────
    private Camera unityCamera;
    private Vector3 currentCameraPosition;
    private Vector3 positionVelocity;
    private float currentYaw;
    private float yawVelocity;
    private float currentPitch;
    private float pitchVelocity;
    private float currentFOV;
    private float fovVelocity;

    // Mouse Orbit State
    private float mouseOrbitYaw = 0f;
    private float mouseOrbitYawVelocity = 0f;
    private float mouseOrbitPitch = 0f;
    private float mouseOrbitPitchVelocity = 0f;
    private float userZoomOffset = 0f;
    private float lastMouseInputTime = -100f;
    private bool isMouseOrbiting = false;

    // Acceleration Kick State
    private float currentKickOffset = 0f;
    private float kickVelocity = 0f;
    private float timeSinceLastKick = float.MaxValue;
    private bool isKicking = false;

    private float previousSpeedMs = 0f;
    private float currentAccel = 0f;
    private Vector3 lastCarPosition;
    private bool isInitialized = false;

    private float currentCollisionMultiplier = 1.0f;
    private float collisionMultiplierVelocity = 0f;

    // ───────────────────────── Properties ─────────────────────────
    public Transform Target
    {
        get => target;
        set { target = value; InitializeTarget(); }
    }

    public int ActiveProfileIndex
    {
        get => activeProfileIndex;
        set
        {
            if (cameraProfiles != null && cameraProfiles.Length > 0)
            {
                activeProfileIndex = Mathf.Clamp(value, 0, cameraProfiles.Length - 1);
            }
        }
    }

    public bool EnableMouseLook
    {
        get => enableMouseLook;
        set => enableMouseLook = value;
    }

    // ───────────────────────── Initialization ─────────────────────────
    private void Awake()
    {
        if (cinemachineCamera == null)
            cinemachineCamera = GetComponent<CinemachineCamera>();

        if (unityCamera == null)
            unityCamera = GetComponent<Camera>() ?? Camera.main;

        if (cinemachineBrain == null)
            cinemachineBrain = FindAnyObjectByType<CinemachineBrain>();

        DisableConflictingCinemachineComponents();
    }

    private void Start()
    {
        InitializeTarget();

        if (cameraProfiles != null && cameraProfiles.Length > 0)
        {
            activeProfileIndex = Mathf.Clamp(activeProfileIndex, 0, cameraProfiles.Length - 1);
            currentFOV = cameraProfiles[activeProfileIndex].baseFOV;
        }
        else
        {
            currentFOV = 62f;
        }

        if (target != null)
        {
            SnapToTarget();
        }

        if (enableMouseLook && mouseActivation == MouseActivationMode.AlwaysActive)
        {
            LockCursor();
        }
    }

    private void OnEnable()
    {
        DisableConflictingCinemachineComponents();
        if (target != null)
        {
            SnapToTarget();
        }
    }

    private void OnDisable()
    {
        UnlockCursor();
    }

    private void DisableConflictingCinemachineComponents()
    {
        var thirdPersonFollow = GetComponent<CinemachineThirdPersonFollow>();
        if (thirdPersonFollow != null && thirdPersonFollow.enabled)
        {
            thirdPersonFollow.enabled = false;
        }

        var rotationComposer = GetComponent<CinemachineRotationComposer>();
        if (rotationComposer != null && rotationComposer.enabled)
        {
            rotationComposer.enabled = false;
        }

        var positionComposer = GetComponent<CinemachinePositionComposer>();
        if (positionComposer != null && positionComposer.enabled)
        {
            positionComposer.enabled = false;
        }

        var panTilt = GetComponent<CinemachinePanTilt>();
        if (panTilt != null && panTilt.enabled)
        {
            panTilt.enabled = false;
        }
    }

    private void InitializeTarget()
    {
        if (target == null)
        {
            var carGO = GameObject.Find("gaddi go brrrr")
                     ?? GameObject.Find("Car")
                     ?? GameObject.FindGameObjectWithTag("Player");

            if (carGO == null)
            {
                var foundCarScript = FindAnyObjectByType<CarScript>();
                if (foundCarScript != null) carGO = foundCarScript.gameObject;
            }

            if (carGO != null)
            {
                target = carGO.transform;
            }
        }

        if (target != null)
        {
            if (carRigidbody == null) carRigidbody = target.GetComponent<Rigidbody>();
            if (carScript == null) carScript = target.GetComponent<CarScript>();
            lastCarPosition = target.position;
        }
    }

    // ───────────────────────── Update Loops ─────────────────────────
    private void Update()
    {
        HandleKeyboardInput();
        HandleMouseInput();
        UpdateAccelerationKick();
    }

    // Static flag allowing UI panels (like Scoreboard) to take exclusive mouse control
    public static bool IsUIModeActive { get; set; } = false;

    private void HandleKeyboardInput()
    {
        if (Keyboard.current == null) return;

        // Toggle camera profile with 'C' key (only in gameplay)
        if (!IsUIModeActive && Keyboard.current.cKey.wasPressedThisFrame)
        {
            CycleCameraProfile();
        }

        // Press 'F' key to immediately re-center camera
        if (!IsUIModeActive && Keyboard.current.fKey.wasPressedThisFrame)
        {
            RecenterCamera();
        }

        // Escape unlocks cursor
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            UnlockCursor();
        }
    }

    private void HandleMouseInput()
    {
        if (!enableMouseLook || Mouse.current == null) return;

        // If UI is active (like scoreboard / cutscenes), keep cursor free and visible
        if (IsUIModeActive)
        {
            if (Cursor.lockState != CursorLockMode.None || !Cursor.visible)
            {
                UnlockCursor();
            }
            return;
        }

        // Check if mouse is hovering over interactive UI element
        if (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        // Middle Mouse click to re-center
        if (Mouse.current.middleButton.wasPressedThisFrame)
        {
            RecenterCamera();
        }

        // Left-click in window to re-lock cursor when AlwaysActive
        if (mouseActivation == MouseActivationMode.AlwaysActive)
        {
            if (Mouse.current.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
            {
                LockCursor();
            }
        }

        bool isMouseActive = IsMouseLookInputActive();

        // Manage cursor lock based on activation mode
        if (mouseActivation == MouseActivationMode.HoldRightClick ||
            mouseActivation == MouseActivationMode.HoldLeftClick ||
            mouseActivation == MouseActivationMode.ClickAndDrag)
        {
            if (isMouseActive && Cursor.lockState != CursorLockMode.Locked)
            {
                LockCursor();
            }
            else if (!isMouseActive && Cursor.lockState == CursorLockMode.Locked)
            {
                UnlockCursor();
            }
        }

        Vector2 mouseDelta = Mouse.current.delta.ReadValue() * 0.12f;

        if (isMouseActive && (Mathf.Abs(mouseDelta.x) > 0.001f || Mathf.Abs(mouseDelta.y) > 0.001f))
        {
            mouseOrbitYaw += mouseDelta.x * mouseSensitivityX;
            mouseOrbitPitch -= mouseDelta.y * mouseSensitivityY * (invertMouseY ? -1f : 1f);
            mouseOrbitPitch = Mathf.Clamp(mouseOrbitPitch, minPitchAngle, maxPitchAngle);

            lastMouseInputTime = Time.time;
            isMouseOrbiting = true;
        }

        // Handle Scroll Wheel Zoom
        if (enableScrollZoom)
        {
            float scroll = Mouse.current.scroll.ReadValue().y * 0.001f;
            if (Mathf.Abs(scroll) > 0.001f)
            {
                CameraProfile profile = GetCurrentProfile();
                float targetDist = profile.distance + userZoomOffset - (scroll * scrollZoomSensitivity * 4f);
                targetDist = Mathf.Clamp(targetDist, minZoomDistance, maxZoomDistance);
                userZoomOffset = targetDist - profile.distance;
            }
        }

        // Auto-recenter logic
        if (isMouseOrbiting && autoRecenterDelay > 0f)
        {
            float timeSinceMouse = Time.time - lastMouseInputTime;
            bool isDriving = autoRecenterOnDrive && carScript != null && Mathf.Abs(carScript.MotorInput) > 0.1f && timeSinceMouse > 0.35f;

            if (timeSinceMouse >= autoRecenterDelay || isDriving)
            {
                mouseOrbitYaw = Mathf.SmoothDampAngle(mouseOrbitYaw, 0f, ref mouseOrbitYawVelocity, autoRecenterSmoothTime);
                mouseOrbitPitch = Mathf.SmoothDampAngle(mouseOrbitPitch, 0f, ref mouseOrbitPitchVelocity, autoRecenterSmoothTime);

                if (Mathf.Abs(mouseOrbitYaw) < 0.1f && Mathf.Abs(mouseOrbitPitch) < 0.1f)
                {
                    mouseOrbitYaw = 0f;
                    mouseOrbitPitch = 0f;
                    isMouseOrbiting = false;
                }
            }
        }
    }

    private bool IsMouseLookInputActive()
    {
        if (Mouse.current == null) return false;
        switch (mouseActivation)
        {
            case MouseActivationMode.AlwaysActive:
                return Cursor.lockState == CursorLockMode.Locked;
            case MouseActivationMode.HoldRightClick:
                return Mouse.current.rightButton.isPressed;
            case MouseActivationMode.HoldLeftClick:
                return Mouse.current.leftButton.isPressed;
            case MouseActivationMode.ClickAndDrag:
                return Mouse.current.leftButton.isPressed || Mouse.current.rightButton.isPressed;
            default:
                return true;
        }
    }

    public void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void RecenterCamera()
    {
        mouseOrbitYaw = 0f;
        mouseOrbitYawVelocity = 0f;
        mouseOrbitPitch = 0f;
        mouseOrbitPitchVelocity = 0f;
        isMouseOrbiting = false;
        lastMouseInputTime = -100f;
    }

    public void CycleCameraProfile()
    {
        if (cameraProfiles == null || cameraProfiles.Length <= 1) return;
        activeProfileIndex = (activeProfileIndex + 1) % cameraProfiles.Length;
        userZoomOffset = 0f;
        if (showDebugInfo)
        {
            Debug.Log($"[CameraController] Switched to profile: {cameraProfiles[activeProfileIndex].profileName}");
        }
    }

    public void SetCameraProfile(int index)
    {
        if (cameraProfiles == null || cameraProfiles.Length == 0) return;
        activeProfileIndex = Mathf.Clamp(index, 0, cameraProfiles.Length - 1);
        userZoomOffset = 0f;
    }

    private void UpdateAccelerationKick()
    {
        if (target == null) return;

        float speedMs = GetCurrentSpeedMs();
        float dt = Time.deltaTime;
        if (dt > 0.0001f)
        {
            if (carScript != null && carScript.enabled)
            {
                currentAccel = carScript.CurrentAcceleration;
            }
            else
            {
                currentAccel = (speedMs - previousSpeedMs) / dt;
            }
        }
        previousSpeedMs = speedMs;

        if (timeSinceLastKick < kickCooldown)
        {
            timeSinceLastKick += dt;
        }

        if (currentAccel > accelSpikeThreshold && timeSinceLastKick >= kickCooldown)
        {
            TriggerKick();
        }

        float targetKickOffset = isKicking ? kickDistance : 0f;
        currentKickOffset = Mathf.SmoothDamp(currentKickOffset, targetKickOffset, ref kickVelocity, returnTime);

        if (isKicking && currentKickOffset < 0.05f && targetKickOffset == 0f)
        {
            isKicking = false;
        }
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            InitializeTarget();
            if (target == null) return;
        }

        float carDelta = Vector3.Distance(target.position, lastCarPosition);
        if (carDelta > teleportThreshold)
        {
            SnapToTarget();
        }
        lastCarPosition = target.position;

        UpdateCameraTransform(Time.deltaTime);
    }

    // ───────────────────────── Camera Math & Tracking ─────────────────────────
    private void UpdateCameraTransform(float deltaTime)
    {
        if (deltaTime <= 0.00001f) deltaTime = Time.deltaTime;
        if (deltaTime <= 0.00001f) return;

        CameraProfile profile = GetCurrentProfile();

        Vector3 carPos = target.position;
        Vector3 carForward = target.forward;
        float speedMs = GetCurrentSpeedMs();
        float speedKmh = speedMs * 3.6f;

        // 1. Determine Reverse State
        bool isReversing = false;
        if (enableSmartReverse && !isMouseOrbiting)
        {
            if (carScript != null && carScript.IsReversing && speedMs > reverseSpeedThreshold)
            {
                isReversing = true;
            }
            else if (carRigidbody != null)
            {
                float forwardDot = Vector3.Dot(carRigidbody.linearVelocity, carForward);
                if (forwardDot < -reverseSpeedThreshold)
                {
                    isReversing = true;
                }
            }
        }

        // 2. Compute Desired Base Heading Direction (with drift velocity blending)
        Vector3 desiredHeading = carForward;
        if (isReversing)
        {
            desiredHeading = -carForward;
        }
        else if (carRigidbody != null && speedMs > minSpeedForVelocityHeading && !isMouseOrbiting)
        {
            Vector3 horizontalVelocity = Vector3.ProjectOnPlane(carRigidbody.linearVelocity, Vector3.up);
            if (horizontalVelocity.sqrMagnitude > 0.5f)
            {
                float blend = Mathf.Clamp01((speedMs - minSpeedForVelocityHeading) / 6f) * velocityHeadingWeight;
                desiredHeading = Vector3.Slerp(carForward, horizontalVelocity.normalized, blend);
            }
        }

        // Calculate Target Base Yaw Angle
        float targetBaseYaw = Mathf.Atan2(desiredHeading.x, desiredHeading.z) * Mathf.Rad2Deg;

        // Smooth Base Yaw
        float yawTime = isReversing ? reverseTransitionTime : yawSmoothTime;
        currentYaw = Mathf.SmoothDampAngle(currentYaw, targetBaseYaw, ref yawVelocity, yawTime, float.MaxValue, deltaTime);

        // Combined Final Yaw with Mouse Orbit
        float totalYaw = currentYaw + mouseOrbitYaw;

        // 3. Compute Dynamic Follow Distance & Height with Zoom Offset
        float speedFactor = Mathf.Clamp01(speedKmh / topSpeedReferenceKmh);
        float baseDistWithZoom = Mathf.Clamp(profile.distance + userZoomOffset, minZoomDistance, maxZoomDistance);
        float dynamicDistance = baseDistWithZoom + (speedFactor * maxSpeedDistanceOffset) + currentKickOffset;

        // Apply braking dive
        if (carScript != null && carScript.IsBraking && speedKmh > 10f)
        {
            dynamicDistance = Mathf.Max(minZoomDistance, dynamicDistance - brakeDiveDistance);
        }

        // 4. Focus / LookAt Target Point
        float lookAhead = isReversing ? -profile.lookAhead * 0.4f : profile.lookAhead;
        Vector3 focusPoint = carPos + Vector3.up * profile.lookAtHeight + desiredHeading * (lookAhead * (0.5f + 0.5f * speedFactor));

        // 5. Calculate Desired Camera Position from Orbit Rotation
        Quaternion orbitRotation = Quaternion.Euler(mouseOrbitPitch, totalYaw, 0f);
        Vector3 backVector = orbitRotation * Vector3.back;
        Vector3 desiredPos = carPos + Vector3.up * profile.height + backVector * dynamicDistance;

        // 6. Obstacle Collision Avoidance (Spring Arm)
        if (enableCollisionAvoidance)
        {
            Vector3 castOrigin = carPos + Vector3.up * profile.lookAtHeight;
            Vector3 castDir = desiredPos - castOrigin;
            float castDist = castDir.magnitude;

            if (castDist > 0.01f)
            {
                float targetMultiplier = 1.0f;
                if (Physics.SphereCast(castOrigin, collisionRadius, castDir.normalized, out RaycastHit hit, castDist, collisionLayers, QueryTriggerInteraction.Ignore))
                {
                    if (hit.transform != target && !hit.transform.IsChildOf(target))
                    {
                        float safeDist = Mathf.Max(minCollisionDistance, hit.distance - 0.05f);
                        targetMultiplier = Mathf.Clamp01(safeDist / castDist);
                    }
                }

                if (targetMultiplier < currentCollisionMultiplier)
                {
                    currentCollisionMultiplier = targetMultiplier;
                    collisionMultiplierVelocity = 0f;
                }
                else
                {
                    currentCollisionMultiplier = Mathf.SmoothDamp(currentCollisionMultiplier, targetMultiplier, ref collisionMultiplierVelocity, collisionReturnSmoothTime, float.MaxValue, deltaTime);
                }

                desiredPos = castOrigin + castDir.normalized * (castDist * currentCollisionMultiplier);
            }
        }
        else
        {
            currentCollisionMultiplier = 1.0f;
        }

        // 7. Smooth Position Interpolation
        if (!isInitialized)
        {
            currentCameraPosition = desiredPos;
            isInitialized = true;
        }
        else
        {
            currentCameraPosition = Vector3.SmoothDamp(currentCameraPosition, desiredPos, ref positionVelocity, positionSmoothTime, float.MaxValue, deltaTime);
        }

        // 8. High-Speed Micro-Turbulence (Camera Shake)
        Vector3 shakeOffset = Vector3.zero;
        if (enableHighSpeedShake && speedKmh > shakeMinSpeedKmh)
        {
            float shakeFactor = Mathf.Clamp01((speedKmh - shakeMinSpeedKmh) / (topSpeedReferenceKmh - shakeMinSpeedKmh));
            float time = Time.time * shakeFrequency;
            float noiseX = (Mathf.PerlinNoise(time, 0f) - 0.5f) * 2f;
            float noiseY = (Mathf.PerlinNoise(0f, time) - 0.5f) * 2f;
            shakeOffset = (transform.right * noiseX + transform.up * noiseY) * (maxShakeIntensity * shakeFactor);
        }

        // 9. Apply Position & Rotation
        transform.position = currentCameraPosition + shakeOffset;

        Vector3 lookDirection = (focusPoint - transform.position).normalized;
        if (lookDirection.sqrMagnitude > 0.001f)
        {
            float targetPitchAngle = -Mathf.Asin(Mathf.Clamp(lookDirection.y, -1f, 1f)) * Mathf.Rad2Deg;
            currentPitch = Mathf.SmoothDampAngle(currentPitch, targetPitchAngle, ref pitchVelocity, pitchSmoothTime, float.MaxValue, deltaTime);

            float lookYaw = Mathf.Atan2(lookDirection.x, lookDirection.z) * Mathf.Rad2Deg;
            Quaternion targetRotation = Quaternion.Euler(currentPitch, lookYaw, 0f);

            if (isKicking && currentKickOffset > 0.01f)
            {
                float pitchKick = kickPitchAngle * (currentKickOffset / kickDistance);
                targetRotation *= Quaternion.Euler(pitchKick, 0f, 0f);
            }

            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 1f - Mathf.Exp(-20f * deltaTime));
        }

        // 10. Dynamic Speed FOV
        if (enableDynamicFOV)
        {
            float targetFOV = Mathf.Lerp(profile.baseFOV, profile.maxFOV, speedFactor);
            currentFOV = Mathf.SmoothDamp(currentFOV, targetFOV, ref fovVelocity, fovSmoothTime, float.MaxValue, deltaTime);

            ApplyFOV(currentFOV);
        }
    }

    private void ApplyFOV(float fov)
    {
        if (cinemachineCamera != null)
        {
            cinemachineCamera.Lens.FieldOfView = fov;
        }
        if (unityCamera != null)
        {
            unityCamera.fieldOfView = fov;
        }
    }

    // ───────────────────────── Public Controls & Helpers ─────────────────────────
    public void SnapToTarget()
    {
        if (target == null) return;

        CameraProfile profile = GetCurrentProfile();
        Vector3 carPos = target.position;
        Vector3 carForward = target.forward;

        currentYaw = Mathf.Atan2(carForward.x, carForward.z) * Mathf.Rad2Deg;
        yawVelocity = 0f;
        pitchVelocity = 0f;
        positionVelocity = Vector3.zero;
        currentKickOffset = 0f;
        kickVelocity = 0f;
        isKicking = false;
        mouseOrbitYaw = 0f;
        mouseOrbitPitch = 0f;
        userZoomOffset = 0f;
        isMouseOrbiting = false;

        Quaternion yawRotation = Quaternion.Euler(0f, currentYaw, 0f);
        Vector3 backVector = yawRotation * Vector3.back;
        currentCameraPosition = carPos + Vector3.up * profile.height + backVector * profile.distance;

        transform.position = currentCameraPosition;
        Vector3 focusPoint = carPos + Vector3.up * profile.lookAtHeight + carForward * profile.lookAhead;
        transform.LookAt(focusPoint);

        currentFOV = profile.baseFOV;
        ApplyFOV(currentFOV);

        lastCarPosition = carPos;
        isInitialized = true;

        if (showDebugInfo)
        {
            Debug.Log("[CameraController] Camera snapped to target.");
        }
    }

    public void TriggerKick()
    {
        isKicking = true;
        timeSinceLastKick = 0f;

        if (showDebugInfo)
        {
            Debug.Log($"[CameraController] Kickback triggered at {currentAccel:F2} m/s²");
        }
    }

    private float GetCurrentSpeedMs()
    {
        if (carScript != null) return carScript.CurrentSpeedMs;
        if (carRigidbody != null) return carRigidbody.linearVelocity.magnitude;
        return 0f;
    }

    private CameraProfile GetCurrentProfile()
    {
        if (cameraProfiles != null && cameraProfiles.Length > 0)
        {
            int idx = Mathf.Clamp(activeProfileIndex, 0, cameraProfiles.Length - 1);
            return cameraProfiles[idx];
        }
        return new CameraProfile("Default", 5.2f, 2.1f, 1.0f, 2.5f, 62f, 76f);
    }

    private void OnDrawGizmosSelected()
    {
        if (target == null) return;
        CameraProfile profile = GetCurrentProfile();
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(target.position + Vector3.up * profile.lookAtHeight, 0.3f);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(target.position + Vector3.up * profile.lookAtHeight + target.forward * profile.lookAhead, 0.4f);
    }
}
