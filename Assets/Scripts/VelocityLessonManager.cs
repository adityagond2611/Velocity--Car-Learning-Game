using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Unity.Cinemachine;

/// <summary>
/// Manages the Velocity Lesson checkpoint system with ultra-smooth cinematic transitions:
/// - Detects car crossing Checkpoint A (start) and Checkpoint B (finish)
/// - Smooth Cinemachine camera blend (2.0s) to high overhead top-down view
/// - Smooth progressive car braking and engine audio decel
/// - Smooth CanvasGroup fade-in and scale pop for the scoreboard
/// - Smooth reset sequence on "Continue"
/// </summary>
public class VelocityLessonManager : MonoBehaviour
{
    // ───────────────────────── Inspector References ─────────────────────────
    [Header("Checkpoints")]
    [SerializeField] private Transform checkpointA;
    [SerializeField] private Transform checkpointB;

    [Header("Car")]
    [SerializeField] private Transform carTransform;
    [SerializeField] private Rigidbody carRigidbody;

    [Header("Cinemachine Cutscene Camera")]
    [SerializeField] private CinemachineCamera cutsceneCamera;

    [Header("UI - Live Timer")]
    [SerializeField] private TextMeshProUGUI liveTimerText;

    [Header("UI - Scoreboard Panel")]
    [SerializeField] private GameObject scoreboardPanel;
    [SerializeField] private CanvasGroup scoreboardCanvasGroup;
    private DisplacementLineAnimator displacementLineAnimator;

    [Header("UI - Scoreboard Content")]
    [SerializeField] private TextMeshProUGUI formulaText;
    [SerializeField] private TextMeshProUGUI timeCardText;
    [SerializeField] private TextMeshProUGUI velocityCardText;
    [SerializeField] private TextMeshProUGUI displacementCardText;
    [SerializeField] private TextMeshProUGUI feedbackCardText;
    [SerializeField] private Button continueButton;

    // Optional overlay indicator image created by designer (red curved line). If present, use it during the 5s sequence instead of the runtime LineRenderer.
    private UnityEngine.UI.Image indicatorImage;
    // Source UI text (top HUD) that displays the "Distance" value (e.g. "784.8 m"). Prefer this value when available.
    private TextMeshProUGUI distanceSourceText;
    // Runtime-created UI label shown during the 5s that mirrors the Distance value (placed in scoreboardPanel)
    private TextMeshProUGUI indicatorDisplayText;

    // ───────────────────────── State ─────────────────────────
    private enum LessonState { WaitingForStart, Timing, Braking, ShowingResults, Resetting }
    private LessonState state = LessonState.WaitingForStart;

    private Vector3 startPosition;
    private float startTime;
    private float endTime;
    private Vector3 carResetPosition;
    private Quaternion carResetRotation;
    private Coroutine activeTransitionCoroutine;

    // Cache for temporarily hiding scoreboard children during the line-draw animation
    private List<GameObject> scoreboardChildren = new List<GameObject>();
    private List<bool> scoreboardChildrenPrevActive = new List<bool>();

    // HUD elements to hide when the run finishes (Time, Acceleration, Distance, Velocity, Speedometer, Map, Graph)
    private List<GameObject> hudElementsToHide = new List<GameObject>();
    private List<bool> hudElementsPrevActive = new List<bool>();

    // ───────────────────────── Lifecycle ─────────────────────────
    private void Start()
    {
        // Auto-find references if not set in inspector
        if (carTransform == null)
        {
            var car = GameObject.Find("gaddi go brrrr");
            if (car != null)
            {
                carTransform = car.transform;
                carRigidbody = car.GetComponent<Rigidbody>();
            }
        }
        if (carRigidbody == null && carTransform != null)
            carRigidbody = carTransform.GetComponent<Rigidbody>();

        if (checkpointA == null)
        {
            var cpA = GameObject.Find("Checkpoint_A");
            if (cpA != null) checkpointA = cpA.transform;
        }
        if (checkpointB == null)
        {
            var cpB = GameObject.Find("Checkpoint_B");
            if (cpB != null) checkpointB = cpB.transform;
        }
        if (cutsceneCamera == null)
        {
            var camGO = GameObject.Find("CinemachineCamera_LessonCutscene");
            if (camGO != null) cutsceneCamera = camGO.GetComponent<CinemachineCamera>();
        }

        AutoFindUI();

        // Auto-find any DisplacementLineAnimator so inspector assignments aren't required
        if (displacementLineAnimator == null)
            displacementLineAnimator = FindAnyObjectByType<DisplacementLineAnimator>();

        // Store car reset position & rotation
        if (carTransform != null)
        {
            carResetPosition = carTransform.position;
            carResetRotation = carTransform.rotation;
        }

        // Initialize cutscene camera priority to inactive
        if (cutsceneCamera != null)
        {
            cutsceneCamera.Priority = -10;
        }

        // Set up Exit button
        if (continueButton != null)
        {
            continueButton.onClick.RemoveAllListeners();
            continueButton.onClick.AddListener(OnExitPressed);
        }

        // Ensure scoreboard CanvasGroup is set up and hidden initially
        if (scoreboardPanel != null)
        {
            if (scoreboardCanvasGroup == null)
                scoreboardCanvasGroup = scoreboardPanel.GetComponent<CanvasGroup>() ?? scoreboardPanel.AddComponent<CanvasGroup>();

            scoreboardCanvasGroup.alpha = 0f;
            scoreboardCanvasGroup.interactable = false;
            scoreboardCanvasGroup.blocksRaycasts = false;
            scoreboardPanel.SetActive(false);
        }

        // Initialize live timer text
        if (liveTimerText != null)
        {
            liveTimerText.gameObject.SetActive(true);
            liveTimerText.text = "Drive through the Start line!";
        }

        // Configure pixel-perfect scoreboard layout matching design reference
        ConfigureScoreboardLayout();

        // Setup triggers on checkpoints
        SetupCheckpointTriggers();
    }

    private void ConfigureScoreboardLayout()
    {
        if (scoreboardPanel == null) return;

        // 1. Background image of Scoreboard: subtle transparent overlay, non-blocking
        var bgImage = scoreboardPanel.GetComponent<UnityEngine.UI.Image>();
        if (bgImage != null)
        {
            bgImage.color = new Color(0f, 0f, 0f, 0.20f);
            bgImage.raycastTarget = false;
        }

        // 2. Top Formula Box
        if (formulaText != null && formulaText.transform.parent != null)
        {
            var formulaBox = formulaText.transform.parent.GetComponent<RectTransform>();
            if (formulaBox != null)
            {
                formulaBox.anchorMin = new Vector2(0.5f, 1f);
                formulaBox.anchorMax = new Vector2(0.5f, 1f);
                formulaBox.pivot = new Vector2(0.5f, 1f);
                formulaBox.anchoredPosition = new Vector2(0f, -15f);
                formulaBox.sizeDelta = new Vector2(560f, 92f);

                var boxImg = formulaBox.GetComponent<UnityEngine.UI.Image>();
                if (boxImg != null)
                {
                    boxImg.color = new Color(0.06f, 0.09f, 0.16f, 0.90f);
                    boxImg.raycastTarget = false;
                }
            }

            var fTextRt = formulaText.GetComponent<RectTransform>();
            if (fTextRt != null)
            {
                fTextRt.anchorMin = Vector2.zero;
                fTextRt.anchorMax = Vector2.one;
                fTextRt.pivot = new Vector2(0.5f, 0.5f);
                fTextRt.anchoredPosition = Vector2.zero;
                fTextRt.sizeDelta = new Vector2(-16f, -8f);
            }
            formulaText.fontSize = 12.5f;
            formulaText.alignment = TextAlignmentOptions.Center;
            formulaText.raycastTarget = false;
        }

        // 3. Bottom Cards (Time, Velocity, Feedback)
        ConfigureCard(timeCardText, new Vector2(-225f, 58f), new Vector2(215f, 62f));
        ConfigureCard(velocityCardText, new Vector2(0f, 58f), new Vector2(215f, 62f));
        ConfigureCard(feedbackCardText, new Vector2(225f, 58f), new Vector2(215f, 62f));

        // 4. Hide redundant displacement card if separate from formula/feedback
        if (displacementCardText != null && displacementCardText.transform.parent != null)
        {
            var p = displacementCardText.transform.parent.gameObject;
            if (p != null && p != (timeCardText != null ? timeCardText.transform.parent.gameObject : null) &&
                p != (velocityCardText != null ? velocityCardText.transform.parent.gameObject : null) &&
                p != (feedbackCardText != null ? feedbackCardText.transform.parent.gameObject : null))
            {
                p.SetActive(false);
            }
        }

        // 5. Continue Button
        if (continueButton != null)
        {
            var btnRt = continueButton.GetComponent<RectTransform>();
            if (btnRt != null)
            {
                btnRt.anchorMin = new Vector2(0.5f, 0f);
                btnRt.anchorMax = new Vector2(0.5f, 0f);
                btnRt.pivot = new Vector2(0.5f, 0f);
                btnRt.anchoredPosition = new Vector2(0f, 16f);
                btnRt.sizeDelta = new Vector2(170f, 32f);
            }
            continueButton.transform.SetAsLastSibling(); // Ensure button is topmost in hierarchy
            continueButton.interactable = true;

            var btnImg = continueButton.GetComponent<UnityEngine.UI.Image>();
            if (btnImg != null)
            {
                btnImg.color = new Color(0.06f, 0.42f, 0.85f, 1.0f);
                btnImg.raycastTarget = true;
            }

            var btnText = continueButton.GetComponentInChildren<TextMeshProUGUI>();
            if (btnText != null)
            {
                btnText.text = "<b>EXIT</b>";
                btnText.color = Color.white;
                btnText.fontSize = 13f;
                btnText.alignment = TextAlignmentOptions.Center;
                btnText.raycastTarget = false;
            }
        }
    }

    private void ConfigureCard(TextMeshProUGUI tmp, Vector2 pos, Vector2 size)
    {
        if (tmp == null || tmp.transform.parent == null) return;
        var parentRt = tmp.transform.parent.GetComponent<RectTransform>();
        if (parentRt != null)
        {
            parentRt.anchorMin = new Vector2(0.5f, 0f);
            parentRt.anchorMax = new Vector2(0.5f, 0f);
            parentRt.pivot = new Vector2(0.5f, 0f);
            parentRt.anchoredPosition = pos;
            parentRt.sizeDelta = size;

            var img = parentRt.GetComponent<UnityEngine.UI.Image>();
            if (img != null)
            {
                img.color = new Color(0.08f, 0.12f, 0.20f, 0.90f);
                img.raycastTarget = false;
            }
        }

        var tmpRt = tmp.GetComponent<RectTransform>();
        if (tmpRt != null)
        {
            tmpRt.anchorMin = Vector2.zero;
            tmpRt.anchorMax = Vector2.one;
            tmpRt.pivot = new Vector2(0.5f, 0.5f);
            tmpRt.anchoredPosition = Vector2.zero;
            tmpRt.sizeDelta = new Vector2(-16f, -10f);
        }
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
    }

    private void AutoFindUI()
    {
        var canvas = GameObject.Find("Canvas");
        if (canvas == null) return;

        for (int i = 0; i < canvas.transform.childCount; i++)
        {
            var child = canvas.transform.GetChild(i);
            if (child.name == "LessonLiveTimer_Text" && liveTimerText == null)
                liveTimerText = child.GetComponent<TextMeshProUGUI>();
            if (child.name == "LessonScoreboard_Panel" && scoreboardPanel == null)
            {
                scoreboardPanel = child.gameObject;
                scoreboardCanvasGroup = scoreboardPanel.GetComponent<CanvasGroup>() ?? scoreboardPanel.AddComponent<CanvasGroup>();
            }
        }

        if (scoreboardPanel != null)
        {
            for (int i = 0; i < scoreboardPanel.transform.childCount; i++)
            {
                var child = scoreboardPanel.transform.GetChild(i);
                string childName = child.name.ToLowerInvariant();

                if (childName.Contains("formula") || childName.Contains("top"))
                    formulaText = child.GetComponentInChildren<TextMeshProUGUI>();
                else if (childName.Contains("time"))
                    timeCardText = child.GetComponentInChildren<TextMeshProUGUI>();
                else if (childName.Contains("velocity") || childName.Contains("speed"))
                    velocityCardText = child.GetComponentInChildren<TextMeshProUGUI>();
                else if (childName.Contains("displacement") || childName.Contains("dist"))
                    displacementCardText = child.GetComponentInChildren<TextMeshProUGUI>();
                else if (childName.Contains("feedback") || childName.Contains("result"))
                    feedbackCardText = child.GetComponentInChildren<TextMeshProUGUI>();
                else if (childName.Contains("continue") || childName.Contains("button"))
                    continueButton = child.GetComponent<Button>();
            }
        }

        // Find designer-supplied indicator Image anywhere under Canvas (case-insensitive name match)
        var allImages = canvas.GetComponentsInChildren<UnityEngine.UI.Image>(true);
        foreach (var img in allImages)
        {
            var n = img.gameObject.name.ToLowerInvariant();
            if (n.Contains("indicator") || n.Contains("indicatorline") || n.Contains("displacementline") || n.Contains("redline") || n.Contains("indicator_line"))
            {
                indicatorImage = img;
                break;
            }
        }

        // Find a top-level "Distance" text on the HUD (not the bottom displacement card). Prefer names containing "distance"
        var allTMPs = canvas.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (var t in allTMPs)
        {
            if (t == displacementCardText) continue;
            var n = t.gameObject.name.ToLowerInvariant();
            if (n.Contains("distance") || n.Contains("dist"))
            {
                distanceSourceText = t;
                break;
            }
        }

        // Collect HUD elements to hide after finishing the run. Search broadly for common keywords.
        var allChildren = canvas.GetComponentsInChildren<Transform>(true);
        foreach (var tr in allChildren)
        {
            if (tr == null || tr.gameObject == null) continue;
            var go = tr.gameObject;
            if (scoreboardPanel != null && (go == scoreboardPanel || go.transform.IsChildOf(scoreboardPanel.transform)))
                continue; // don't hide scoreboard contents here

            var lname = go.name.ToLowerInvariant();
            if (lname.Contains("time") || lname.Contains("accel") || lname.Contains("acceleration") || lname.Contains("distance") || lname.Contains("velocity") || lname.Contains("speed") || lname.Contains("speedometer") || lname.Contains("speedo") || lname.Contains("map") || lname.Contains("graph") || lname.Contains("velgraph") || lname.Contains("velocitygraph"))
            {
                // Avoid adding duplicates
                if (!hudElementsToHide.Contains(go))
                    hudElementsToHide.Add(go);
            }
        }
    }

    private void SetupCheckpointTriggers()
    {
        if (checkpointA != null)
        {
            var trigger = checkpointA.gameObject.GetComponent<CheckpointTrigger>() ?? checkpointA.gameObject.AddComponent<CheckpointTrigger>();
            trigger.Initialize(this, CheckpointType.Start);
            var col = checkpointA.GetComponent<BoxCollider>();
            if (col != null) col.isTrigger = true;
        }

        if (checkpointB != null)
        {
            var trigger = checkpointB.gameObject.GetComponent<CheckpointTrigger>() ?? checkpointB.gameObject.AddComponent<CheckpointTrigger>();
            trigger.Initialize(this, CheckpointType.Finish);
            var col = checkpointB.GetComponent<BoxCollider>();
            if (col != null) col.isTrigger = true;
        }
    }

    private void Update()
    {
        if (state == LessonState.Timing && liveTimerText != null)
        {
            float elapsed = Time.time - startTime;
            if (elapsed < 5.0f)
            {
                liveTimerText.text = $"{elapsed:F2} s  |  Reach the end (Reach the red dot on the map)";
            }
            else
            {
                liveTimerText.text = $"{elapsed:F2} s";
            }
        }

        // Maintain cursor freedom during scoreboard results screen
        if (state == LessonState.ShowingResults)
        {
            if (Cursor.lockState != CursorLockMode.None || !Cursor.visible)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            CameraController.IsUIModeActive = true;
        }
    }

    // ───────────────────────── Checkpoint Callbacks ─────────────────────────
    public void OnCheckpointEntered(CheckpointType type, Collider other)
    {
        if (other.attachedRigidbody == null) return;
        if (carRigidbody != null && other.attachedRigidbody != carRigidbody) return;

        if (type == CheckpointType.Start && state == LessonState.WaitingForStart)
        {
            OnStartLineCrossed();
        }
        else if (type == CheckpointType.Finish && state == LessonState.Timing)
        {
            OnFinishLineCrossed();
        }
    }

    private void OnStartLineCrossed()
    {
        state = LessonState.Timing;
        startTime = Time.time;
        startPosition = carTransform.position;

        if (liveTimerText != null)
        {
            liveTimerText.gameObject.SetActive(true);
            liveTimerText.text = "0.00 s  |  Reach the end (Reach the red dot on the map)";
        }

        Debug.Log("[VelocityLesson] START line crossed! Timer started.");
    }

    private void OnFinishLineCrossed()
    {
        state = LessonState.Braking;
        endTime = Time.time;

        // Disable player car inputs immediately
        var carScript = carTransform != null ? carTransform.GetComponent<CarScript>() : null;
        if (carScript != null) carScript.enabled = false;

        // Activate Cinemachine Cutscene Camera with high priority to begin smooth 2.0s blend
        if (cutsceneCamera != null)
            cutsceneCamera.Priority = 100;

        // Start smooth deceleration and scoreboard presentation sequence
        if (activeTransitionCoroutine != null) StopCoroutine(activeTransitionCoroutine);
        activeTransitionCoroutine = StartCoroutine(SmoothFinishSequence());

        Debug.Log("[VelocityLesson] FINISH line crossed! Smooth cutscene sequence initiated.");
    }

    // ───────────────────────── Smooth Finish Sequence ─────────────────────────
    private IEnumerator SmoothFinishSequence()
    {
        // Enable UI Mode for camera and unlock cursor so player can click UI buttons
        CameraController.IsUIModeActive = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Immediately hide HUD elements (Time, Acceleration, Distance, Velocity, Speedometer, Map, Graph)
        hudElementsPrevActive.Clear();
        if (hudElementsToHide == null || hudElementsToHide.Count == 0)
            AutoFindUI(); // ensure list populated
        foreach (var go in hudElementsToHide)
        {
            if (go == null) continue;
            hudElementsPrevActive.Add(go.activeSelf);
            go.SetActive(false);
        }

        // Smoothly brake the car over 1.5 - 2.0 seconds while camera is flying up
        float brakingDuration = 2.0f;
        float elapsedBraking = 0f;

        var wheels = carTransform != null ? carTransform.GetComponentsInChildren<WheelCollider>() : new WheelCollider[0];

        while (elapsedBraking < brakingDuration && carRigidbody != null && carRigidbody.linearVelocity.magnitude > 0.1f)
        {
            elapsedBraking += Time.deltaTime;
            float brakeStrength = Mathf.SmoothStep(1000f, 6000f, elapsedBraking / brakingDuration);

            // Progressive braking force
            Vector3 opposingForce = -carRigidbody.linearVelocity.normalized * brakeStrength;
            carRigidbody.AddForce(opposingForce, ForceMode.Force);

            foreach (var w in wheels)
            {
                w.motorTorque = 0f;
                w.brakeTorque = brakeStrength;
            }

            yield return null;
        }

        // Completely bring car physics to rest
        if (carRigidbody != null)
        {
            carRigidbody.linearVelocity = Vector3.zero;
            carRigidbody.angularVelocity = Vector3.zero;
        }

        // Brief pleasant pause while overhead camera settles
        yield return new WaitForSeconds(0.4f);

        // Populate scoreboard values
        PopulateScoreboardValues();

        // Smoothly fade in and scale pop the Scoreboard UI
        if (scoreboardPanel != null)
        {
            // Ensure panel is active for the upcoming line animation to be visible in the scene
            // Also force the cursor to be visible/unlocked while the scoreboard is showing so clicking doesn't hide it.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            scoreboardPanel.SetActive(true);
            if (scoreboardCanvasGroup == null)
                scoreboardCanvasGroup = scoreboardPanel.GetComponent<CanvasGroup>() ?? scoreboardPanel.AddComponent<CanvasGroup>();

            // Temporarily hide ALL child UI elements under the scoreboard panel until the line animation completes.
            // Cache each child's previous active state so it can be restored exactly as before.
            scoreboardChildren.Clear();
            scoreboardChildrenPrevActive.Clear();
            for (int i = 0; i < scoreboardPanel.transform.childCount; i++)
            {
                var childGo = scoreboardPanel.transform.GetChild(i).gameObject;
                scoreboardChildren.Add(childGo);
                scoreboardChildrenPrevActive.Add(childGo.activeSelf);
                childGo.SetActive(false);
            }

            // Ensure we have a DisplacementLineAnimator in the scene (auto-create if needed)
            if (displacementLineAnimator == null)
            {
                var existing = FindAnyObjectByType<DisplacementLineAnimator>();
                if (existing != null) displacementLineAnimator = existing;
                else
                {
                    var go = new GameObject("DisplacementLineAnimator");
                    displacementLineAnimator = go.AddComponent<DisplacementLineAnimator>();
                }
            }

            // Determine a distance value to display: prefer the car's total track distance (TotalDistanceMeters) if available.
            float displayDistance = -1f;
            string displayDistanceText = null;
            if (carTransform != null)
            {
                var carScript = carTransform.GetComponent<CarScript>();
                if (carScript != null)
                {
                    displayDistance = carScript.TotalDistanceMeters;
                    displayDistanceText = $"{displayDistance:F1} m";
                }
            }

            // If there's a designer-supplied indicator image, use it (do not run the world-space LineRenderer)
            if (indicatorImage != null)
            {
                indicatorImage.gameObject.SetActive(true);
                indicatorImage.color = Color.red;

                // Create a small UI label in the scoreboard panel to show the distance text during the 5s
                if (indicatorDisplayText == null && scoreboardPanel != null)
                {
                    var go = new GameObject("IndicatorDisplayText", typeof(RectTransform));
                    go.transform.SetParent(scoreboardPanel.transform, false);
                    var rt = go.GetComponent<RectTransform>();
                    rt.anchorMin = new Vector2(0.5f, 0.07f);
                    rt.anchorMax = new Vector2(0.5f, 0.07f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.sizeDelta = new Vector2(300f, 50f);

                    indicatorDisplayText = go.AddComponent<TextMeshProUGUI>();
                    indicatorDisplayText.alignment = TextAlignmentOptions.Center;
                    indicatorDisplayText.fontSize = 24f;
                    indicatorDisplayText.color = Color.red;
                    indicatorDisplayText.text = displayDistanceText ?? "";
                }

                // Wait exactly 5 seconds while indicator image and label are visible
                yield return new WaitForSeconds(5f);

                // Hide indicator and remove label
                indicatorImage.gameObject.SetActive(false);
                if (indicatorDisplayText != null)
                {
                    Destroy(indicatorDisplayText.gameObject);
                    indicatorDisplayText = null;
                }
            }
            else
            {
                // Start the line draw animation for exactly 5 seconds (runs concurrently with UI fade-in)
                float numericDisplay = displayDistance; // pass numeric (track distance) if available, else -1
                StartCoroutine(displacementLineAnimator.AnimateLineRoutine(checkpointA, checkpointB, 5f, numericDisplay));
                // Wait until the animator signals it's done (AnimateLineRoutine sets IsAnimating false when finished)
                yield return new WaitUntil(() => displacementLineAnimator == null || displacementLineAnimator.IsAnimating == false);
            }

            float fadeDuration = 0.7f;
            float elapsedFade = 0f;
            var panelRect = scoreboardPanel.GetComponent<RectTransform>();

            while (elapsedFade < fadeDuration)
            {
                elapsedFade += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedFade / fadeDuration);
                float ease = Mathf.SmoothStep(0f, 1f, t);

                scoreboardCanvasGroup.alpha = ease;
                if (panelRect != null)
                {
                    float scale = Mathf.Lerp(0.94f, 1.0f, ease);
                    panelRect.localScale = new Vector3(scale, scale, 1f);
                }

                yield return null;
            }

            // Ensure UI is visible (but content remains hidden until line finished)
            scoreboardCanvasGroup.alpha = 1f;
            if (panelRect != null) panelRect.localScale = Vector3.one;

            // Wait until the line animation completes (5 seconds). If animator is missing, proceed immediately.
            yield return new WaitUntil(() => displacementLineAnimator == null || displacementLineAnimator.IsAnimating == false);

            // Restore previously cached child active states so the scoreboard content returns to the same layout
            for (int i = 0; i < scoreboardChildren.Count; i++)
            {
                var go = scoreboardChildren[i];
                bool prevActive = scoreboardChildrenPrevActive[i];
                if (go != null)
                    go.SetActive(prevActive);
            }
            scoreboardChildren.Clear();
            scoreboardChildrenPrevActive.Clear();

            // Re-enable interactions now that the visual sequence is complete
            scoreboardCanvasGroup.interactable = true;
            scoreboardCanvasGroup.blocksRaycasts = true;
        }

        state = LessonState.ShowingResults;
    }

    private void PopulateScoreboardValues()
    {
        float deltaTime = Mathf.Max(0.01f, endTime - startTime);
        Vector3 displacementVec = (checkpointB != null && checkpointA != null)
            ? (checkpointB.position - checkpointA.position)
            : Vector3.forward * 160f;

        float displacement = displacementVec.magnitude;
        float avgVelocity = displacement / deltaTime;

    // Prefer the car's recorded total distance (track distance) if available
    float totalDistance = displacement;
    if (carTransform != null)
    {
        var carScript = carTransform.GetComponent<CarScript>();
        if (carScript != null)
            totalDistance = carScript.TotalDistanceMeters;
    }

        ConfigureScoreboardLayout();

        if (liveTimerText != null)
            liveTimerText.text = $"{deltaTime:F2} s (Finished!)";

        if (formulaText != null)
        {
            formulaText.lineSpacing = 4f;
            formulaText.text =
                $"<size=125%><b>VELOCITY CALCULATED!</b></size>\n\n" +
                $"<size=105%><color=#94A3B8>VELOCITY</color>  =  <color=#CBD5E1><u>  DISPLACEMENT  </u></color>  =  <color=#CBD5E1><u>    {displacement:F0} m    </u></color>  =  <color=#46E059><b>{avgVelocity:F0} m/s</b></color></size>\n" +
                $"<size=105%><color=#94A3B8>                </color>        <color=#CBD5E1>     TIME     </color>         <color=#CBD5E1>     {deltaTime:F0} s     </color></size>";
        }

        if (timeCardText != null)
            timeCardText.text = $"<color=#94A3B8><size=75%>TIME TAKEN</size></color>\n<size=135%><b>{deltaTime:F0} s</b></size>";

        if (velocityCardText != null)
            velocityCardText.text = $"<color=#94A3B8><size=75%>AVERAGE VELOCITY</size></color>\n<size=135%><b>{avgVelocity:F0} m/s</b></size>";

        if (feedbackCardText != null)
        {
            string header = "WELL DONE!";
            string desc = "You grasped the concept of velocity!";
            if (avgVelocity > 35f) { header = "EXCELLENT!"; desc = "Blazing speed! Velocity mastered!"; }
            else if (avgVelocity < 12f) { header = "GOOD EFFORT!"; desc = "You completed the run! Try faster next time!"; }
            feedbackCardText.text = $"<size=95%><b>{header}</b></size>\n<size=75%><color=#CBD5E1>{desc}</color></size>";
        }
    }

    // ───────────────────────── Exit on Button Click ─────────────────────────
    private void OnExitPressed()
    {
        Debug.Log("[VelocityLesson] Exit button pressed. Quitting application.");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void OnContinuePressed()
    {
        OnExitPressed();
    }

    private IEnumerator SmoothResetSequence()
    {
        // 1. Smoothly fade out the scoreboard UI
        if (scoreboardCanvasGroup != null)
        {
            scoreboardCanvasGroup.interactable = false;
            scoreboardCanvasGroup.blocksRaycasts = false;

            float fadeDuration = 0.4f;
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                scoreboardCanvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
                yield return null;
            }
            scoreboardCanvasGroup.alpha = 0f;
        }

        if (scoreboardPanel != null) scoreboardPanel.SetActive(false);

        // Clear any displacement line animation when hiding the scoreboard
        if (displacementLineAnimator != null) displacementLineAnimator.ClearLine();

        // Restore HUD elements that were hidden when the run finished
        if (hudElementsToHide != null && hudElementsToHide.Count > 0 && hudElementsPrevActive != null)
        {
            for (int i = 0; i < hudElementsToHide.Count && i < hudElementsPrevActive.Count; i++)
            {
                var go = hudElementsToHide[i];
                if (go == null) continue;
                bool wasActive = hudElementsPrevActive[i];
                go.SetActive(wasActive);
            }
            hudElementsPrevActive.Clear();
        }

        // Ensure cursor is visible/unlocked when returning from the scoreboard so it doesn't stay hidden
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // 2. Start blending Cinemachine camera back to player view
        if (cutsceneCamera != null)
            cutsceneCamera.Priority = -10;

        // 3. Smoothly reset car physics and position
        if (carRigidbody != null)
        {
            carRigidbody.linearVelocity = Vector3.zero;
            carRigidbody.angularVelocity = Vector3.zero;
        }
        if (carTransform != null)
        {
            carTransform.position = carResetPosition;
            carTransform.rotation = carResetRotation;
            FindAnyObjectByType<CameraController>()?.SnapToTarget();
        }

        var wheels = carTransform != null ? carTransform.GetComponentsInChildren<WheelCollider>() : new WheelCollider[0];
        foreach (var w in wheels)
        {
            w.brakeTorque = 0f;
            w.motorTorque = 0f;
        }

        // Wait for camera to finish smooth return blend
        yield return new WaitForSeconds(1.0f);

        // Re-enable player car controls and camera mouse lock
        var carScript = carTransform != null ? carTransform.GetComponent<CarScript>() : null;
        if (carScript != null) carScript.enabled = true;

        CameraController.IsUIModeActive = false;

        if (liveTimerText != null)
            liveTimerText.text = "Drive through the Start line!";

        state = LessonState.WaitingForStart;
        Debug.Log("[VelocityLesson] Smooth Reset Complete! Ready for next run.");
    }
}

