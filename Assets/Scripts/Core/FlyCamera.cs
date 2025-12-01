using UnityEngine;
using UnityEngine.InputSystem;

[AddComponentMenu("Camera Control/Fly Camera (Input System)")]
public class FlyCamera : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed   = 10f;
    public float climbSpeed  = 4f;

    [Header("Look")]
    public float lookSpeed   = 0.1f;   // degrees per pixel
    public bool  invertY     = false;

    private float rotationX = 0f;
    private float rotationY = 0f;

    void Start()
    {
        // Initialize rotation from current transform to avoid snapping
        Vector3 euler = transform.eulerAngles;
        rotationX = euler.x > 180 ? euler.x - 360 : euler.x;
        rotationY = euler.y;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
    }

    void Update()
    {
        // Skip input when replay menu is open
        var replayMenu = FindObjectOfType<ReplayMenuController>();
        if (replayMenu != null && replayMenu.IsVisible) return;

        var kb   = Keyboard.current;
        var ms   = Mouse.current;
        Vector3 dir = Vector3.zero;

        // — Move on WASD (use unscaledDeltaTime for god mode) —
        if (kb.wKey.isPressed) dir += transform.forward;
        if (kb.sKey.isPressed) dir -= transform.forward;
        if (kb.aKey.isPressed) dir -= transform.right;
        if (kb.dKey.isPressed) dir += transform.right;

        // — Climb/Descend on E/Q —
        if (kb.eKey.isPressed) dir += transform.up * climbSpeed;
        if (kb.qKey.isPressed) dir -= transform.up * climbSpeed;

        transform.position += dir * moveSpeed * Time.unscaledDeltaTime;

        // — Mouse scroll to adjust speed (like Unity Scene view) —
        float scroll = ms.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            float multiplier = scroll > 0 ? 1.1f : 0.9f;
            moveSpeed *= multiplier;
            moveSpeed = Mathf.Clamp(moveSpeed, 0.1f, 1000f);
        }

        // — Mouse look (use unscaledDeltaTime for god mode) —
        Vector2 delta = ms.delta.ReadValue();
        float yaw   = delta.x * lookSpeed * Time.unscaledDeltaTime * 60f; // Scale for frame rate independence
        float pitch = delta.y * lookSpeed * (invertY ? 1 : -1) * Time.unscaledDeltaTime * 60f;

        rotationY += yaw;
        rotationX -= pitch;
        rotationX = Mathf.Clamp(rotationX, -90f, 90f);

        transform.rotation = Quaternion.Euler(rotationX, rotationY, 0f);

        // — Unlock cursor —
        if (kb.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible   = true;
        }
    }
}
