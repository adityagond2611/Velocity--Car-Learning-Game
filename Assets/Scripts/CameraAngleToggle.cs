using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using Unity.Cinemachine;

public class CameraAngleToggle : MonoBehaviour
{
    [SerializeField] private CinemachineVirtualCamera cameraAngleA;
        [SerializeField] private CinemachineVirtualCamera cameraAngleB;
        [SerializeField] private CinemachineVirtualCamera mouseLookCamera;
    [SerializeField] private CinemachineInputAxisController mouseLookInputController;

    private int currentAngleIndex = 0;
        private CinemachineVirtualCamera[] cameraAngles;

    private bool controlEnabled = false;

    private void Start()
    {
        // CameraAngleToggle disabled: this component will not change camera state to avoid interfering with gameplay.
        controlEnabled = false;
        Debug.Log("[CameraAngleToggle] Camera control disabled by maintainer. Toggle and camera-control actions are no-ops.");

        // Keep serialized references intact but do not perform any automatic camera changes.
        cameraAngles = new[] { cameraAngleA, cameraAngleB, mouseLookCamera };
    }

    private void Update()
    {
        if (IsMouseLookActive && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            ReleaseCursor();
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            ReleaseCursor();
        }
        else if (IsMouseLookActive)
        {
            CaptureCursor();
        }
    }

    private void OnDisable()
    {
        ReleaseCursor();
    }

    /// <summary>
    /// Toggle between camera angles. Call this from a UI Button's OnClick event.
    /// </summary>
    public void ToggleCameraAngle()
    {
        // Camera control disabled: do nothing to avoid interfering with gameplay.
        Debug.Log("[CameraAngleToggle] Toggle requested but camera control is disabled.");
        return;
    }

    /// <summary>
    /// Optional: Switch to a specific angle by index (0 = Angle A, 1 = Angle B).
    /// Useful for future expansion to 3+ angles.
    /// </summary>
    public void SetCameraAngle(int angleIndex)
    {
        // Camera control disabled: ignore requests to set camera angle.
        Debug.Log("[CameraAngleToggle] SetCameraAngle called but camera control is disabled.");
        return;
    }

    private void ApplyCameraState()
    {
        for (int i = 0; i < cameraAngles.Length; i++)
        {
            cameraAngles[i].Priority = (i == currentAngleIndex) ? 10 : 0;
        }

        bool isMouseLookActive = IsMouseLookActive;
        if (mouseLookInputController != null)
        {
            mouseLookInputController.enabled = isMouseLookActive;
        }

        if (isMouseLookActive)
        {
            // Ensure UI does not keep focus and lock the cursor for mouse-look controls.
            // Clear any selected UI element (prevents button from keeping focus) and then lock.
            UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);
            CaptureCursor();

            // Some platforms or UI interactions may briefly release the cursor; enforce lock again next frame.
            // Use a single-frame delayed action.
            StartCoroutine(EnsureCursorLockedNextFrame());
        }
        else
        {
            ReleaseCursor();
        }

        Debug.Log($"[CameraAngleToggle] Set to {GetCameraAngleName()}. Camera priority updated.");
    }

    private System.Collections.IEnumerator EnsureCursorLockedNextFrame()
    {
        yield return null; // wait one frame
        CaptureCursor();
    }

    private bool IsMouseLookActive =>
        cameraAngles != null
        && currentAngleIndex >= 0
        && currentAngleIndex < cameraAngles.Length
        && cameraAngles[currentAngleIndex] == mouseLookCamera;

    private string GetCameraAngleName()
    {
        return currentAngleIndex switch
        {
            0 => "Angle A",
            1 => "Angle B",
            _ => "Mouse Look"
        };
    }

    private void TryCenterCinemachineCamera(CinemachineVirtualCamera cam)
    {
        if (cam == null) return;

        try
        {
            // Use the non-generic API to get the Aim component (CinemachineComponentBase)
            var compBase = cam.GetCinemachineComponent(CinemachineCore.Stage.Aim);
            if (compBase != null)
            {
                var t = compBase.GetType();
                // Try common field names used by Composer and FramingTransposer across Cinemachine versions.
                SetFloatFieldIfExists(compBase, t, "m_ScreenX", 0.5f);
                SetFloatFieldIfExists(compBase, t, "m_ScreenY", 0.5f);
                SetFloatFieldIfExists(compBase, t, "m_DeadZoneWidth", 0f);
                SetFloatFieldIfExists(compBase, t, "m_DeadZoneHeight", 0f);

                // Some versions expose properties instead of fields; try those too.
                SetFloatPropertyIfExists(compBase, t, "m_ScreenX", 0.5f);
                SetFloatPropertyIfExists(compBase, t, "m_ScreenY", 0.5f);
                SetFloatPropertyIfExists(compBase, t, "m_DeadZoneWidth", 0f);
                SetFloatPropertyIfExists(compBase, t, "m_DeadZoneHeight", 0f);
            }
        }
        catch (Exception)
        {
            // Ignore any reflection exceptions - this is best-effort to support multiple Cinemachine versions.
        }
    }

    private void SetFloatFieldIfExists(object target, Type type, string fieldName, float value)
    {
        var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field != null && field.FieldType == typeof(float))
        {
            field.SetValue(target, value);
        }
    }

    private void SetFloatPropertyIfExists(object target, Type type, string propName, float value)
    {
        var prop = type.GetProperty(propName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (prop != null && prop.PropertyType == typeof(float) && prop.CanWrite)
        {
            prop.SetValue(target, value);
        }
    }

    private static void CaptureCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private static void ReleaseCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}

