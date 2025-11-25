using UnityEngine;

/// <summary>
/// Manages a picture-in-picture camera that can be attached to a missile or threat.
/// The camera follows the target and renders to a RenderTexture for display in the HUD.
/// </summary>
[AddComponentMenu("Simulation/UI/PiP Camera Controller")]
public sealed class PiPCameraController : MonoBehaviour
{
    [Header("Camera Settings")]
    [Tooltip("The camera component that will render the PiP view")]
    public Camera pipCamera;

    [Tooltip("RenderTexture to output the camera view")]
    public RenderTexture renderTexture;

    [Header("Mount Settings")]
    [Tooltip("Offset from the target's position in local space")]
    public Vector3 localOffset = new Vector3(0f, 0.5f, -2f);

    [Tooltip("Look at offset from target (to aim camera slightly ahead)")]
    public Vector3 lookAtOffset = new Vector3(0f, 0f, 5f);

    [Tooltip("Smoothing factor for camera follow (0 = no smoothing, 1 = very smooth)")]
    [Range(0f, 1f)]
    public float smoothing = 0.7f;

    private Transform targetTransform;
    private bool isActive = false;

    void Awake()
    {
        if (pipCamera == null)
            pipCamera = GetComponent<Camera>();

        // Initially disable the camera
        if (pipCamera != null)
            pipCamera.enabled = false;
    }

    /// <summary>
    /// Attach this PiP camera to a target missile/threat
    /// </summary>
    public void AttachToTarget(GameObject target)
    {
        if (target == null)
        {
            Debug.LogWarning("PiPCameraController: Cannot attach to null target");
            Detach();
            return;
        }

        targetTransform = target.transform;
        isActive = true;

        if (pipCamera != null)
        {
            pipCamera.enabled = true;
            pipCamera.targetTexture = renderTexture;
        }

        Debug.Log($"PiP Camera attached to {target.name}");
    }

    /// <summary>
    /// Detach from current target and disable the camera
    /// </summary>
    public void Detach()
    {
        targetTransform = null;
        isActive = false;

        if (pipCamera != null)
            pipCamera.enabled = false;
    }

    void LateUpdate()
    {
        if (!isActive || targetTransform == null)
            return;

        // Calculate desired position in world space
        Vector3 desiredPosition = targetTransform.TransformPoint(localOffset);

        // Smooth follow
        if (smoothing > 0f)
        {
            transform.position = Vector3.Lerp(transform.position, desiredPosition, 1f - smoothing);
        }
        else
        {
            transform.position = desiredPosition;
        }

        // Look at target with offset
        Vector3 lookAtPoint = targetTransform.TransformPoint(lookAtOffset);
        transform.LookAt(lookAtPoint);
    }

    /// <summary>
    /// Check if this camera is currently active
    /// </summary>
    public bool IsActive => isActive;

    /// <summary>
    /// Get the RenderTexture for this camera
    /// </summary>
    public RenderTexture GetRenderTexture() => renderTexture;
}
