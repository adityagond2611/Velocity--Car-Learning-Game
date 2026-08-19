using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using Unity.Cinemachine;

/// <summary>
/// Handles camera angle and profile toggles from UI buttons or hotkeys.
/// Integrates with CameraController presets while maintaining Cinemachine compatibility.
/// </summary>
public class CameraAngleToggle : MonoBehaviour
{
    [SerializeField] private CinemachineCamera cameraAngleA;
    [SerializeField] private CinemachineCamera cameraAngleB;
    [SerializeField] private CinemachineCamera mouseLookCamera;
    [SerializeField] private CinemachineInputAxisController mouseLookInputController;
    [SerializeField] private CameraController cameraController;

    private int currentAngleIndex = 0;
    private CinemachineCamera[] cameraAngles;

    private void Start()
    {
        if (cameraController == null)
            cameraController = FindAnyObjectByType<CameraController>();

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
    /// Toggle between camera profiles/angles. Call this from a UI Button's OnClick event.
    /// </summary>
    public void ToggleCameraAngle()
    {
        if (cameraController != null)
        {
            cameraController.CycleCameraProfile();
        }
        else
        {
            currentAngleIndex = (currentAngleIndex + 1) % (cameraAngles != null && cameraAngles.Length > 0 ? cameraAngles.Length : 1);
            ApplyCameraState();
        }
    }

    /// <summary>
    /// Switch to a specific profile or camera angle index.
    /// </summary>
    public void SetCameraAngle(int angleIndex)
    {
        if (cameraController != null)
        {
            cameraController.SetCameraProfile(angleIndex);
        }
        else
        {
            currentAngleIndex = angleIndex;
            ApplyCameraState();
        }
    }

    private void ApplyCameraState()
    {
        if (cameraAngles == null) return;

        for (int i = 0; i < cameraAngles.Length; i++)
        {
            if (cameraAngles[i] != null)
            {
                cameraAngles[i].Priority = (i == currentAngleIndex) ? 10 : 0;
            }
        }

        bool isMouseLookActive = IsMouseLookActive;
        if (mouseLookInputController != null)
        {
            mouseLookInputController.enabled = isMouseLookActive;
        }

        if (isMouseLookActive)
        {
            EventSystem.current?.SetSelectedGameObject(null);
            CaptureCursor();
            StartCoroutine(EnsureCursorLockedNextFrame());
        }
        else
        {
            ReleaseCursor();
        }

        Debug.Log($"[CameraAngleToggle] Camera state updated to {GetCameraAngleName()}.");
    }

    private System.Collections.IEnumerator EnsureCursorLockedNextFrame()
    {
        yield return null;
        CaptureCursor();
    }

    private bool IsMouseLookActive =>
        cameraAngles != null
        && currentAngleIndex >= 0
        && currentAngleIndex < cameraAngles.Length
        && cameraAngles[currentAngleIndex] == mouseLookCamera
        && mouseLookCamera != null;

    private string GetCameraAngleName()
    {
        return currentAngleIndex switch
        {
            0 => "Angle A",
            1 => "Angle B",
            _ => "Mouse Look"
        };
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


