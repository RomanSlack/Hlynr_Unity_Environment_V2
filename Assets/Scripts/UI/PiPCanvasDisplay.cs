using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Creates PiP camera views using viewport rects (no RenderTextures - more reliable).
/// The cameras render directly to screen corners with labels via OnGUI.
/// </summary>
[AddComponentMenu("Simulation/UI/PiP Canvas Display")]
public class PiPCanvasDisplay : MonoBehaviour
{
    [Header("PiP Cameras")]
    [Tooltip("Camera for interceptor view")]
    public Camera interceptorCamera;
    [Tooltip("Camera for threat view")]
    public Camera threatCamera;

    [Header("Display Settings")]
    [Range(0.05f, 0.4f)]
    public float pipWidth = 0.15f;  // As fraction of screen width
    [Range(0.05f, 0.4f)]
    public float pipHeight = 0.12f; // As fraction of screen height
    public float padding = 8f;      // Pixels from edge

    // Tracking targets
    private Transform interceptorTarget;
    private Transform threatTarget;
    private Vector3 cameraOffset = new Vector3(0f, 2f, -6f);

    private bool interceptorActive = false;
    private bool threatActive = false;

    void Start()
    {
        Debug.Log("[PiPCanvasDisplay] Start() - Using viewport rect approach");

        // Setup interceptor camera
        if (interceptorCamera != null)
        {
            interceptorCamera.targetTexture = null; // Render to screen, not texture
            interceptorCamera.depth = 10; // Render on top of main camera
            interceptorCamera.clearFlags = CameraClearFlags.Skybox;
            // Viewport: bottom-right, upper position
            UpdateInterceptorViewport();
            // Enable immediately for testing - will show skybox until target attached
            interceptorCamera.enabled = true;
            interceptorActive = true;
            Debug.Log($"[PiPCanvasDisplay] Interceptor camera setup. Rect: {interceptorCamera.rect}");
        }
        else
        {
            Debug.LogError("[PiPCanvasDisplay] Interceptor Camera not assigned!");
        }

        // Setup threat camera
        if (threatCamera != null)
        {
            threatCamera.targetTexture = null; // Render to screen, not texture
            threatCamera.depth = 11; // Render on top
            threatCamera.clearFlags = CameraClearFlags.Skybox;
            // Viewport: bottom-right, lower position
            UpdateThreatViewport();
            // Enable immediately for testing - will show skybox until target attached
            threatCamera.enabled = true;
            threatActive = true;
            Debug.Log($"[PiPCanvasDisplay] Threat camera setup. Rect: {threatCamera.rect}");
        }
        else
        {
            Debug.LogError("[PiPCanvasDisplay] Threat Camera not assigned!");
        }
    }

    void UpdateInterceptorViewport()
    {
        if (interceptorCamera == null) return;
        // Position: bottom-right, above threat camera
        float x = 1f - pipWidth - (padding / Screen.width);
        float y = pipHeight + (padding * 2 / Screen.height);
        interceptorCamera.rect = new Rect(x, y, pipWidth, pipHeight);
    }

    void UpdateThreatViewport()
    {
        if (threatCamera == null) return;
        // Position: bottom-right corner
        float x = 1f - pipWidth - (padding / Screen.width);
        float y = padding / Screen.height;
        threatCamera.rect = new Rect(x, y, pipWidth, pipHeight);
    }

    public void AttachInterceptor(GameObject target)
    {
        if (target == null) return;
        interceptorTarget = target.transform;
        interceptorActive = true;
        if (interceptorCamera != null)
            interceptorCamera.enabled = true;
        Debug.Log($"[PiPCanvasDisplay] Attached interceptor camera to {target.name}");
    }

    public void AttachThreat(GameObject target)
    {
        if (target == null) return;
        threatTarget = target.transform;
        threatActive = true;
        if (threatCamera != null)
            threatCamera.enabled = true;
        Debug.Log($"[PiPCanvasDisplay] Attached threat camera to {target.name}");
    }

    void LateUpdate()
    {
        // Update viewports in case screen size changed
        UpdateInterceptorViewport();
        UpdateThreatViewport();

        // Follow interceptor
        if (interceptorActive && interceptorTarget != null && interceptorCamera != null)
        {
            Vector3 desiredPos = interceptorTarget.TransformPoint(cameraOffset);
            interceptorCamera.transform.position = Vector3.Lerp(
                interceptorCamera.transform.position, desiredPos, Time.unscaledDeltaTime * 8f);
            interceptorCamera.transform.LookAt(interceptorTarget.position + interceptorTarget.forward * 10f);
        }

        // Follow threat
        if (threatActive && threatTarget != null && threatCamera != null)
        {
            Vector3 desiredPos = threatTarget.TransformPoint(cameraOffset);
            threatCamera.transform.position = Vector3.Lerp(
                threatCamera.transform.position, desiredPos, Time.unscaledDeltaTime * 8f);
            threatCamera.transform.LookAt(threatTarget.position + threatTarget.forward * 10f);
        }
    }

    void OnGUI()
    {
        if (!interceptorActive && !threatActive) return;

        // Calculate screen positions for labels
        float screenW = Screen.width;
        float screenH = Screen.height;
        float pxWidth = pipWidth * screenW;
        float pxHeight = pipHeight * screenH;
        float pxPadding = padding;

        // Style for labels
        var labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 10,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };

        float labelHeight = 16f;

        // Interceptor label (above its viewport)
        if (interceptorActive && interceptorCamera != null && interceptorCamera.enabled)
        {
            float x = screenW - pxWidth - pxPadding;
            float y = screenH - (pxHeight * 2) - (pxPadding * 2) - labelHeight - 2;

            // Background box
            GUI.backgroundColor = new Color(0.1f, 0.2f, 0.4f, 0.9f);
            GUI.Box(new Rect(x, y, pxWidth, labelHeight), "");

            // Label
            labelStyle.normal.textColor = new Color(0.4f, 0.7f, 1f);
            GUI.Label(new Rect(x, y, pxWidth, labelHeight), "INTERCEPTOR", labelStyle);

            // Draw border around viewport
            DrawViewportBorder(interceptorCamera.rect, new Color(0.3f, 0.6f, 1f), 2);
        }

        // Threat label (above its viewport)
        if (threatActive && threatCamera != null && threatCamera.enabled)
        {
            float x = screenW - pxWidth - pxPadding;
            float y = screenH - pxHeight - pxPadding - labelHeight - 2;

            // Background box
            GUI.backgroundColor = new Color(0.4f, 0.1f, 0.1f, 0.9f);
            GUI.Box(new Rect(x, y, pxWidth, labelHeight), "");

            // Label
            labelStyle.normal.textColor = new Color(1f, 0.4f, 0.4f);
            GUI.Label(new Rect(x, y, pxWidth, labelHeight), "THREAT", labelStyle);

            // Draw border around viewport
            DrawViewportBorder(threatCamera.rect, new Color(1f, 0.3f, 0.3f), 2);
        }
    }

    void DrawViewportBorder(Rect viewportRect, Color color, int thickness)
    {
        // Convert viewport rect (0-1) to screen pixels
        // Note: viewport Y is from bottom, GUI Y is from top
        float x = viewportRect.x * Screen.width;
        float y = (1f - viewportRect.y - viewportRect.height) * Screen.height; // Flip Y
        float w = viewportRect.width * Screen.width;
        float h = viewportRect.height * Screen.height;

        // Create border texture
        Texture2D tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, color);
        tex.Apply();

        // Draw borders
        GUI.DrawTexture(new Rect(x - thickness, y - thickness, w + thickness * 2, thickness), tex); // Top
        GUI.DrawTexture(new Rect(x - thickness, y + h, w + thickness * 2, thickness), tex); // Bottom
        GUI.DrawTexture(new Rect(x - thickness, y, thickness, h), tex); // Left
        GUI.DrawTexture(new Rect(x + w, y, thickness, h), tex); // Right

        Destroy(tex);
    }
}
