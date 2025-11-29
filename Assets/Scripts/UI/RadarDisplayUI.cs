using UnityEngine;
using Replay;

/// <summary>
/// Classic radar sweep display with rotating scan line, blips, and detection indicators.
/// Renders using OnGUI for simplicity and compatibility.
/// </summary>
[AddComponentMenu("Simulation/UI/Radar Display")]
public class RadarDisplayUI : MonoBehaviour
{
    [Header("Display Settings")]
    [Tooltip("Size of the radar display in pixels")]
    public float displaySize = 200f;

    [Tooltip("Padding from screen edge")]
    public float padding = 10f;

    [Tooltip("Corner position")]
    public ScreenCorner corner = ScreenCorner.BottomLeft;

    [Header("Radar Settings")]
    [Tooltip("Sweep rotation speed (degrees per second)")]
    public float sweepSpeed = 120f;

    [Tooltip("How long blips remain visible after detection (seconds)")]
    public float blipFadeTime = 2f;

    [Tooltip("Display range in meters (auto-scales to show threat)")]
    public float displayRange = 5000f;

    [Header("Colors")]
    public Color backgroundColor = new Color(0.02f, 0.08f, 0.02f, 0.95f);
    public Color gridColor = new Color(0.1f, 0.4f, 0.1f, 0.6f);
    public Color sweepColor = new Color(0.2f, 1f, 0.2f, 0.8f);
    public Color sweepTrailColor = new Color(0.1f, 0.5f, 0.1f, 0.3f);
    public Color threatBlipColor = new Color(1f, 0.2f, 0.2f, 1f);
    public Color interceptorBlipColor = new Color(0.2f, 0.6f, 1f, 1f);
    public Color groundStationColor = new Color(0.8f, 0.8f, 0.2f, 1f);
    public Color detectionColor = new Color(0.2f, 1f, 0.2f, 1f);
    public Color noDetectionColor = new Color(1f, 0.3f, 0.3f, 0.6f);
    public Color textColor = new Color(0.3f, 1f, 0.3f, 1f);

    public enum ScreenCorner { TopLeft, TopRight, BottomLeft, BottomRight }

    // Current radar state
    private RadarFrame currentFrame;
    private float sweepAngle = 0f;

    // Blip tracking for fade effect
    private float threatBlipTime = -999f;
    private Vector2 threatBlipPos;
    private float groundBlipTime = -999f;

    // Textures for drawing
    private Texture2D pixelTex;
    private Texture2D circleTex;
    private Texture2D blipTex;

    // Detection history for trail effect
    private float[] sweepTrail = new float[360];

    void Awake()
    {
        CreateTextures();
    }

    void CreateTextures()
    {
        // Simple pixel texture
        pixelTex = new Texture2D(1, 1);
        pixelTex.SetPixel(0, 0, Color.white);
        pixelTex.Apply();

        // Circle texture for radar background
        int size = 128;
        circleTex = new Texture2D(size, size);
        float center = size / 2f;
        float radius = center - 1;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                float alpha = dist <= radius ? 1f : 0f;
                circleTex.SetPixel(x, y, new Color(1, 1, 1, alpha));
            }
        }
        circleTex.Apply();

        // Blip texture (small glowing dot)
        int blipSize = 16;
        blipTex = new Texture2D(blipSize, blipSize);
        float blipCenter = blipSize / 2f;
        for (int y = 0; y < blipSize; y++)
        {
            for (int x = 0; x < blipSize; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(blipCenter, blipCenter));
                float alpha = Mathf.Clamp01(1f - (dist / blipCenter));
                alpha = alpha * alpha; // Make it more concentrated
                blipTex.SetPixel(x, y, new Color(1, 1, 1, alpha));
            }
        }
        blipTex.Apply();
    }

    void Update()
    {
        // Rotate sweep line
        sweepAngle += sweepSpeed * Time.unscaledDeltaTime;
        if (sweepAngle >= 360f) sweepAngle -= 360f;

        // Update sweep trail (fade old values)
        for (int i = 0; i < 360; i++)
        {
            sweepTrail[i] = Mathf.Max(0f, sweepTrail[i] - Time.unscaledDeltaTime / blipFadeTime);
        }

        // Mark current sweep position
        int sweepIdx = Mathf.FloorToInt(sweepAngle) % 360;
        sweepTrail[sweepIdx] = 1f;
    }

    /// <summary>
    /// Called by ReplayDirector to update radar state
    /// </summary>
    public void UpdateRadarState(RadarFrame frame)
    {
        currentFrame = frame;

        if (frame == null) return;

        // Update threat blip when detected by either radar
        bool threatDetected = (frame.onboard?.detected ?? false) || (frame.ground?.detected ?? false);
        if (threatDetected)
        {
            threatBlipTime = Time.unscaledTime;

            // Calculate threat position relative to interceptor (for onboard) or ground station
            if (frame.onboard?.detected == true && frame.onboard.position != null)
            {
                // Use onboard radar data - threat is at range in forward direction
                float range = frame.onboard.range_to_target;
                float angle = frame.onboard.beam_angle_to_target_deg;

                // Convert to 2D position (top-down view, forward = up)
                // The angle is from forward vector, so we need the actual direction
                if (frame.onboard.forward_vector != null && frame.onboard.forward_vector.Length >= 3)
                {
                    // Project to 2D (XZ plane in ENU = XY in our radar display)
                    Vector2 fwd2D = new Vector2(frame.onboard.forward_vector[0], frame.onboard.forward_vector[1]).normalized;
                    float fwdAngle = Mathf.Atan2(fwd2D.y, fwd2D.x) * Mathf.Rad2Deg;

                    // Threat is at beam_angle_to_target from forward
                    // For simplicity, assume threat is roughly in forward direction for now
                    float normalizedRange = Mathf.Clamp01(range / displayRange);
                    threatBlipPos = fwd2D * normalizedRange * 0.45f; // 0.45 = radius in normalized coords
                }
            }
            else if (frame.ground?.detected == true)
            {
                // Use ground radar - calculate from elevation and range
                float range = frame.ground.range_to_target;
                float elevation = frame.ground.elevation_deg;

                // Simple: place at distance from center based on range
                float normalizedRange = Mathf.Clamp01(range / displayRange);
                // Use a fixed direction for ground radar detections (coming from top of screen)
                threatBlipPos = new Vector2(0f, normalizedRange * 0.45f);
            }
        }

        // Ground station detection
        if (frame.ground?.detected == true)
        {
            groundBlipTime = Time.unscaledTime;
        }
    }

    void OnGUI()
    {
        if (pixelTex == null) CreateTextures();

        // Calculate position based on corner
        Rect displayRect = GetDisplayRect();
        Vector2 center = new Vector2(displayRect.x + displayRect.width / 2f,
                                      displayRect.y + displayRect.height / 2f);
        float radius = displaySize / 2f - 5f;

        // Draw background circle
        GUI.color = backgroundColor;
        GUI.DrawTexture(displayRect, circleTex);

        // Draw grid circles
        GUI.color = gridColor;
        DrawCircle(center, radius * 0.25f, 1);
        DrawCircle(center, radius * 0.5f, 1);
        DrawCircle(center, radius * 0.75f, 1);
        DrawCircle(center, radius, 2);

        // Draw cross-hairs
        DrawLine(center + Vector2.left * radius, center + Vector2.right * radius, gridColor, 1);
        DrawLine(center + Vector2.up * radius, center + Vector2.down * radius, gridColor, 1);

        // Draw sweep trail (fading arc behind sweep line)
        DrawSweepTrail(center, radius);

        // Draw sweep line
        Vector2 sweepEnd = center + AngleToVector(sweepAngle) * radius;
        DrawLine(center, sweepEnd, sweepColor, 2);

        // Draw ground station marker (at center)
        if (currentFrame?.ground?.enabled == true)
        {
            Color gsColor = groundStationColor;
            if (Time.unscaledTime - groundBlipTime < blipFadeTime)
            {
                // Pulse when detecting
                float pulse = Mathf.Sin(Time.unscaledTime * 10f) * 0.3f + 0.7f;
                gsColor = Color.Lerp(groundStationColor, detectionColor, pulse);
            }
            DrawBlip(center, 8f, gsColor);

            // Small "G" label
            var gsStyle = new GUIStyle(GUI.skin.label) { fontSize = 8, fontStyle = FontStyle.Bold };
            gsStyle.normal.textColor = groundStationColor;
            GUI.Label(new Rect(center.x + 6, center.y - 12, 20, 15), "GS", gsStyle);
        }

        // Draw interceptor blip (always at center, representing "us")
        Color intColor = interceptorBlipColor;
        if (currentFrame?.onboard != null)
        {
            // Show interceptor seeker status
            if (currentFrame.onboard.detected)
            {
                float pulse = Mathf.Sin(Time.unscaledTime * 8f) * 0.3f + 0.7f;
                intColor = Color.Lerp(interceptorBlipColor, detectionColor, pulse);
            }
        }
        DrawBlip(center, 6f, intColor);

        // Draw seeker FOV cone (from interceptor)
        if (currentFrame?.onboard != null)
        {
            DrawSeekerCone(center, radius, currentFrame.onboard);
        }

        // Draw threat blip
        float threatFade = 1f - Mathf.Clamp01((Time.unscaledTime - threatBlipTime) / blipFadeTime);
        if (threatFade > 0f)
        {
            Vector2 threatScreenPos = center + threatBlipPos * displaySize;
            Color tColor = threatBlipColor;
            tColor.a *= threatFade;
            DrawBlip(threatScreenPos, 10f, tColor);

            // Threat label
            if (threatFade > 0.5f)
            {
                var threatStyle = new GUIStyle(GUI.skin.label) { fontSize = 9, fontStyle = FontStyle.Bold };
                threatStyle.normal.textColor = new Color(threatBlipColor.r, threatBlipColor.g, threatBlipColor.b, threatFade);
                GUI.Label(new Rect(threatScreenPos.x + 8, threatScreenPos.y - 6, 50, 15), "TGT", threatStyle);
            }
        }

        // Draw status panel
        DrawStatusPanel(displayRect);

        // Draw title
        var titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 11,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperCenter
        };
        titleStyle.normal.textColor = textColor;
        GUI.Label(new Rect(displayRect.x, displayRect.y - 18, displayRect.width, 20), "RADAR", titleStyle);
    }

    void DrawSeekerCone(Vector2 center, float radius, OnboardRadarState onboard)
    {
        if (onboard.forward_vector == null || onboard.forward_vector.Length < 3) return;

        // Get forward direction in 2D (XY plane = ENU East-North)
        Vector2 fwd2D = new Vector2(onboard.forward_vector[0], onboard.forward_vector[1]);
        if (fwd2D.sqrMagnitude < 0.001f) return;
        fwd2D.Normalize();

        float fwdAngle = Mathf.Atan2(fwd2D.y, fwd2D.x) * Mathf.Rad2Deg;
        float halfBeam = onboard.half_beam_width_deg;

        // Draw cone edges
        Color coneColor = onboard.in_beam ? new Color(0.2f, 0.8f, 0.2f, 0.4f) : new Color(0.5f, 0.5f, 0.5f, 0.2f);

        Vector2 leftEdge = center + AngleToVector(fwdAngle + halfBeam - 90f) * radius * 0.8f;
        Vector2 rightEdge = center + AngleToVector(fwdAngle - halfBeam - 90f) * radius * 0.8f;

        DrawLine(center, leftEdge, coneColor, 1);
        DrawLine(center, rightEdge, coneColor, 1);

        // Draw arc at edge
        int segments = 20;
        for (int i = 0; i < segments; i++)
        {
            float a1 = fwdAngle - halfBeam + (halfBeam * 2f * i / segments) - 90f;
            float a2 = fwdAngle - halfBeam + (halfBeam * 2f * (i + 1) / segments) - 90f;
            Vector2 p1 = center + AngleToVector(a1) * radius * 0.8f;
            Vector2 p2 = center + AngleToVector(a2) * radius * 0.8f;
            DrawLine(p1, p2, coneColor, 1);
        }
    }

    void DrawStatusPanel(Rect displayRect)
    {
        float panelWidth = displaySize;
        float panelHeight = 60f;
        Rect panelRect = new Rect(displayRect.x, displayRect.y + displaySize + 5f, panelWidth, panelHeight);

        // Background
        GUI.color = new Color(0.02f, 0.05f, 0.02f, 0.9f);
        GUI.DrawTexture(panelRect, pixelTex);
        GUI.color = Color.white;

        // Border
        DrawRectBorder(panelRect, gridColor, 1);

        var labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 9 };
        labelStyle.normal.textColor = textColor;

        float y = panelRect.y + 3f;
        float x = panelRect.x + 5f;

        if (currentFrame != null)
        {
            // Onboard radar status
            string onboardStatus = "---";
            Color onboardColor = noDetectionColor;
            if (currentFrame.onboard != null)
            {
                if (currentFrame.onboard.detected)
                {
                    onboardStatus = "LOCK";
                    onboardColor = detectionColor;
                }
                else
                {
                    onboardStatus = currentFrame.onboard.detection_reason ?? "SEARCH";
                    if (onboardStatus == "outside_beam") onboardStatus = "NO FOV";
                    else if (onboardStatus == "poor_signal") onboardStatus = "WEAK";
                }
            }
            labelStyle.normal.textColor = onboardColor;
            GUI.Label(new Rect(x, y, panelWidth - 10, 14), $"SEEKER: {onboardStatus}", labelStyle);

            // Ground radar status
            y += 14f;
            string groundStatus = "---";
            Color groundColor = noDetectionColor;
            if (currentFrame.ground != null && currentFrame.ground.enabled)
            {
                if (currentFrame.ground.detected)
                {
                    groundStatus = $"TRK {currentFrame.ground.range_to_target:0}m";
                    groundColor = detectionColor;
                }
                else
                {
                    groundStatus = "SEARCH";
                }
            }
            labelStyle.normal.textColor = groundColor;
            GUI.Label(new Rect(x, y, panelWidth - 10, 14), $"GROUND: {groundStatus}", labelStyle);

            // Fusion status
            y += 14f;
            string fusionStatus = "---";
            Color fusionColor = textColor;
            if (currentFrame.fusion != null)
            {
                if (currentFrame.fusion.both_detected)
                {
                    fusionStatus = $"DUAL {currentFrame.fusion.fusion_confidence:P0}";
                    fusionColor = detectionColor;
                }
                else if (currentFrame.fusion.any_detected)
                {
                    fusionStatus = $"SINGLE {currentFrame.fusion.fusion_confidence:P0}";
                    fusionColor = new Color(1f, 0.8f, 0.2f, 1f);
                }
                else
                {
                    fusionStatus = "NO TRACK";
                    fusionColor = noDetectionColor;
                }
            }
            labelStyle.normal.textColor = fusionColor;
            GUI.Label(new Rect(x, y, panelWidth - 10, 14), $"FUSION: {fusionStatus}", labelStyle);

            // Range to target
            y += 14f;
            labelStyle.normal.textColor = textColor;
            float range = currentFrame.onboard?.range_to_target ?? currentFrame.ground?.range_to_target ?? 0f;
            GUI.Label(new Rect(x, y, panelWidth - 10, 14), $"RANGE: {range:0}m", labelStyle);
        }
        else
        {
            labelStyle.normal.textColor = noDetectionColor;
            GUI.Label(new Rect(x, y, panelWidth - 10, 14), "NO DATA", labelStyle);
        }
    }

    void DrawSweepTrail(Vector2 center, float radius)
    {
        // Draw fading trail behind sweep
        for (int i = 0; i < 360; i++)
        {
            if (sweepTrail[i] > 0.05f)
            {
                Color trailColor = sweepTrailColor;
                trailColor.a *= sweepTrail[i];
                Vector2 p1 = center + AngleToVector(i) * radius * 0.1f;
                Vector2 p2 = center + AngleToVector(i) * radius;
                DrawLine(p1, p2, trailColor, 1);
            }
        }
    }

    Rect GetDisplayRect()
    {
        float x = 0f, y = 0f;

        switch (corner)
        {
            case ScreenCorner.TopLeft:
                x = padding;
                y = padding;
                break;
            case ScreenCorner.TopRight:
                x = Screen.width - displaySize - padding;
                y = padding;
                break;
            case ScreenCorner.BottomLeft:
                x = padding;
                y = Screen.height - displaySize - padding - 70f; // Extra space for status panel
                break;
            case ScreenCorner.BottomRight:
                x = Screen.width - displaySize - padding;
                y = Screen.height - displaySize - padding - 70f;
                break;
        }

        return new Rect(x, y, displaySize, displaySize);
    }

    Vector2 AngleToVector(float angleDeg)
    {
        float rad = angleDeg * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(rad), -Mathf.Sin(rad)); // Negative Y because GUI Y is inverted
    }

    void DrawBlip(Vector2 center, float size, Color color)
    {
        GUI.color = color;
        GUI.DrawTexture(new Rect(center.x - size/2, center.y - size/2, size, size), blipTex);
        GUI.color = Color.white;
    }

    void DrawCircle(Vector2 center, float radius, int thickness)
    {
        int segments = 64;
        for (int i = 0; i < segments; i++)
        {
            float a1 = (i / (float)segments) * 360f;
            float a2 = ((i + 1) / (float)segments) * 360f;
            Vector2 p1 = center + AngleToVector(a1) * radius;
            Vector2 p2 = center + AngleToVector(a2) * radius;
            DrawLine(p1, p2, gridColor, thickness);
        }
    }

    void DrawLine(Vector2 p1, Vector2 p2, Color color, int thickness)
    {
        GUI.color = color;

        Vector2 delta = p2 - p1;
        float length = delta.magnitude;
        if (length < 0.1f) return;

        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;

        GUIUtility.RotateAroundPivot(angle, p1);
        GUI.DrawTexture(new Rect(p1.x, p1.y - thickness/2f, length, thickness), pixelTex);
        GUIUtility.RotateAroundPivot(-angle, p1);

        GUI.color = Color.white;
    }

    void DrawRectBorder(Rect rect, Color color, int thickness)
    {
        DrawLine(new Vector2(rect.x, rect.y), new Vector2(rect.x + rect.width, rect.y), color, thickness);
        DrawLine(new Vector2(rect.x, rect.y + rect.height), new Vector2(rect.x + rect.width, rect.y + rect.height), color, thickness);
        DrawLine(new Vector2(rect.x, rect.y), new Vector2(rect.x, rect.y + rect.height), color, thickness);
        DrawLine(new Vector2(rect.x + rect.width, rect.y), new Vector2(rect.x + rect.width, rect.y + rect.height), color, thickness);
    }

    void OnDestroy()
    {
        if (pixelTex != null) Destroy(pixelTex);
        if (circleTex != null) Destroy(circleTex);
        if (blipTex != null) Destroy(blipTex);
    }
}
