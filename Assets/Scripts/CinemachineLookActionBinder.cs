using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Supplies the project's existing Player/Look action to the Pan Tilt input controller.
/// The controller remains responsible for gain and input smoothing.
/// </summary>
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
[RequireComponent(typeof(CinemachineInputAxisController))]
public sealed class CinemachineLookActionBinder : MonoBehaviour
{
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private string lookActionPath = "Player/Look";

    private InputActionReference lookActionReference;

    private void Awake()
    {
        BindLookAction();
    }

    private void BindLookAction()
    {
        if (inputActions == null)
        {
            // InputActions asset missing - do not log repeatedly or disable the component to avoid noisy errors.
            return;
        }

        InputAction lookAction = inputActions.FindAction(lookActionPath, throwIfNotFound: false);
        if (lookAction == null)
        {
            // Look action not found in the asset; silently skip binding to avoid interfering with runtime.
            return;
        }

        lookActionReference = InputActionReference.Create(lookAction);

        CinemachineInputAxisController inputController = GetComponent<CinemachineInputAxisController>();
        if (inputController == null) return;
        inputController.SynchronizeControllers();

        foreach (CinemachineInputAxisController.Controller controller in inputController.Controllers)
        {
            if (controller == null) continue;
            if (controller.Name == "Look X (Pan)" || controller.Name == "Look Y (Tilt)")
            {
                controller.Input.InputAction = lookActionReference;
            }
        }
    }
}
