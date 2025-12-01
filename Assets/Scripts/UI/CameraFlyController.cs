using UnityEngine;
using UnityEngine.InputSystem;

public class CameraFlyController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 10f;
    public float fastMoveSpeed = 20f;
    public float mouseSensitivity = 2f;
    public float smoothTime = 0.1f;


    private Vector3 velocity;
    private Vector3 targetVelocity;
    private float rotationX = 0f;
    private float rotationY = 0f;
    private bool cursorLocked = true;

    void Start()
    {
        // Initialize rotation from current transform to avoid snapping
        Vector3 euler = transform.eulerAngles;
        // Normalize angles to -180 to 180 range
        rotationX = euler.x > 180 ? euler.x - 360 : euler.x;
        rotationY = euler.y;

        LockCursor();
    }

    void Update()
    {
        // Skip input when replay menu is open
        var replayMenu = FindObjectOfType<ReplayMenuController>();
        if (replayMenu != null && replayMenu.IsVisible) return;

        // Use unscaledDeltaTime for true god mode that works even when game is paused
        HandleMouseLook();
        HandleMovement();
        HandleSpeedAdjustment();
        HandleCursorToggle();
    }

    void HandleSpeedAdjustment()
    {
        var mouse = Mouse.current;
        if (mouse != null)
        {
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                // Scale speed by 10% per scroll notch (like Unity Scene view)
                float multiplier = scroll > 0 ? 1.1f : 0.9f;
                moveSpeed *= multiplier;
                fastMoveSpeed *= multiplier;

                // Clamp to reasonable range
                moveSpeed = Mathf.Clamp(moveSpeed, 0.1f, 1000f);
                fastMoveSpeed = Mathf.Clamp(fastMoveSpeed, moveSpeed, 2000f);
            }
        }
    }

    void HandleMouseLook()
    {
        if (cursorLocked)
        {
            var mouse = Mouse.current;
            if (mouse != null)
            {
                Vector2 mouseDelta = mouse.delta.ReadValue();
                // Use unscaledDeltaTime to work during pause
                float mouseX = mouseDelta.x * mouseSensitivity * Time.unscaledDeltaTime;
                float mouseY = mouseDelta.y * mouseSensitivity * Time.unscaledDeltaTime;

                rotationY += mouseX;
                rotationX -= mouseY;
                rotationX = Mathf.Clamp(rotationX, -90f, 90f);

                transform.rotation = Quaternion.Euler(rotationX, rotationY, 0f);
            }
        }
    }

    void HandleMovement()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        Vector3 inputVector = Vector3.zero;

        if (keyboard.wKey.isPressed) inputVector += Vector3.forward;
        if (keyboard.sKey.isPressed) inputVector += Vector3.back;
        if (keyboard.aKey.isPressed) inputVector += Vector3.left;
        if (keyboard.dKey.isPressed) inputVector += Vector3.right;
        if (keyboard.eKey.isPressed) inputVector += Vector3.up;
        if (keyboard.qKey.isPressed) inputVector += Vector3.down;

        inputVector = transform.TransformDirection(inputVector);
        inputVector.Normalize();

        float currentSpeed = keyboard.leftShiftKey.isPressed ? fastMoveSpeed : moveSpeed;
        targetVelocity = inputVector * currentSpeed;

        // Use unscaledDeltaTime to work during pause
        velocity = Vector3.Lerp(velocity, targetVelocity, smoothTime * Time.unscaledDeltaTime * 10f);
        transform.position += velocity * Time.unscaledDeltaTime;
    }

    void HandleCursorToggle()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            ToggleCursor();
        }
    }

    void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        cursorLocked = true;
    }

    void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        cursorLocked = false;
    }

    void ToggleCursor()
    {
        if (cursorLocked)
            UnlockCursor();
        else
            LockCursor();
    }

    void OnValidate()
    {
        moveSpeed = Mathf.Max(0.1f, moveSpeed);
        fastMoveSpeed = Mathf.Max(moveSpeed, fastMoveSpeed);
        mouseSensitivity = Mathf.Max(0.1f, mouseSensitivity);
        smoothTime = Mathf.Max(0.01f, smoothTime);
    }
}