using UnityEngine;

public enum CheckpointType { Start, Finish }

/// <summary>
/// Attach to a checkpoint GameObject with a trigger collider.
/// Forwards OnTriggerEnter events to the VelocityLessonManager.
/// </summary>
public class CheckpointTrigger : MonoBehaviour
{
    private VelocityLessonManager manager;
    private CheckpointType checkpointType;

    public void Initialize(VelocityLessonManager mgr, CheckpointType type)
    {
        manager = mgr;
        checkpointType = type;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (manager != null)
            manager.OnCheckpointEntered(checkpointType, other);
    }
}
