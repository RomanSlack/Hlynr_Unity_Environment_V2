using UnityEngine;
using UnityEngine.UIElements;

[AddComponentMenu("Simulation/UI/Missile HUD Controller")]
public sealed class MissileHUDController : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Main HUD document")]
    public UIDocument hudDocument;

    [Header("PiP Camera Settings")]
    [Tooltip("PiP camera controller for interceptor view")]
    public PiPCameraController interceptorCamera;

    [Tooltip("PiP camera controller for threat view")]
    public PiPCameraController threatCamera;

    // UI Elements
    private VisualElement root;
    private VisualElement telemetryPanel;
    private VisualElement interceptorPip;
    private VisualElement threatPip;
    private VisualElement interceptorCameraView;
    private VisualElement threatCameraView;

    private Label speedLbl, fuelLbl, lockLbl, missLbl;
    private Label simStatusLbl;

    // Missile references
    private Missile6DOFController currentMissile;
    private SeekerSensor seeker;
    private Rigidbody rb;
    private FuelSystem fuel;
    private Transform currentTarget;

    void Awake()
    {
        // Get or find UIDocument
        if (hudDocument == null)
            hudDocument = GetComponent<UIDocument>();

        if (hudDocument != null)
        {
            root = hudDocument.rootVisualElement;

            // Query UI elements
            telemetryPanel = root.Q<VisualElement>("telemetry-panel");
            interceptorPip = root.Q<VisualElement>("interceptor-pip");
            threatPip = root.Q<VisualElement>("threat-pip");
            interceptorCameraView = root.Q<VisualElement>("interceptor-camera-view");
            threatCameraView = root.Q<VisualElement>("threat-camera-view");

            speedLbl = root.Q<Label>("speed");
            fuelLbl = root.Q<Label>("fuel");
            lockLbl = root.Q<Label>("lock");
            missLbl = root.Q<Label>("miss");
            simStatusLbl = root.Q<Label>("sim-status");

            Debug.Log($"HUD initialized. Telemetry panel found: {telemetryPanel != null}");

            // Initially hide telemetry panel
            if (telemetryPanel != null)
                telemetryPanel.style.display = DisplayStyle.None;
        }
        else
        {
            Debug.LogError("MissileHUDController: No UIDocument found!");
        }
    }

    /// <summary>
    /// Attach the HUD to a missile and target
    /// </summary>
    public void AttachMissile(GameObject missile, Transform target)
    {
        Debug.Log($"HUD attached to missile: {missile?.name}, target: {target?.name}");

        currentMissile = missile ? missile.GetComponent<Missile6DOFController>() : null;
        rb = missile ? missile.GetComponent<Rigidbody>() : null;
        fuel = missile ? missile.GetComponent<FuelSystem>() : null;
        seeker = missile ? missile.GetComponent<SeekerSensor>() : null;
        currentTarget = target;

        if (missile != null)
        {
            // Show telemetry panel
            if (telemetryPanel != null)
                telemetryPanel.style.display = DisplayStyle.Flex;

            // Update status
            if (simStatusLbl != null)
                simStatusLbl.text = "INTERCEPTOR ACTIVE";

            // Compute initial miss distance
            if (missLbl != null && target != null)
            {
                float miss = Vector3.Distance(missile.transform.position, target.position);
                missLbl.text = $"Miss: {miss:0} m";
            }

            // Attach PiP cameras
            if (interceptorCamera != null)
            {
                interceptorCamera.AttachToTarget(missile);
                ShowInterceptorPip(true);
            }

            if (threatCamera != null && target != null)
            {
                threatCamera.AttachToTarget(target.gameObject);
                ShowThreatPip(true);
            }

            // Setup RenderTextures for PiP displays
            SetupPipDisplays();
        }
        else
        {
            DetachMissile();
        }
    }

    /// <summary>
    /// Detach from current missile and hide panels
    /// </summary>
    public void DetachMissile()
    {
        currentMissile = null;
        rb = null;
        fuel = null;
        seeker = null;
        currentTarget = null;

        // Hide telemetry panel
        if (telemetryPanel != null)
            telemetryPanel.style.display = DisplayStyle.None;

        // Reset status
        if (simStatusLbl != null)
            simStatusLbl.text = "SIM READY";

        // Detach and hide PiP cameras
        if (interceptorCamera != null)
        {
            interceptorCamera.Detach();
            ShowInterceptorPip(false);
        }

        if (threatCamera != null)
        {
            threatCamera.Detach();
            ShowThreatPip(false);
        }
    }

    void Update()
    {
        if (currentMissile == null) return;

        // Update telemetry
        if (speedLbl != null && rb != null)
            speedLbl.text = $"Speed: {rb.linearVelocity.magnitude:0} m/s";

        if (fuelLbl != null && fuel != null)
            fuelLbl.text = $"Fuel: {fuel.fuelKg:0.0} kg";

        if (lockLbl != null && seeker != null)
        {
            if (seeker.HasLock)
            {
                lockLbl.text = "Lock: YES";
                lockLbl.style.color = new StyleColor(new Color(0f, 1f, 0.6f)); // Green
            }
            else
            {
                lockLbl.text = "Lock: NO";
                lockLbl.style.color = new StyleColor(new Color(1f, 0.3f, 0.3f)); // Red
            }
        }

        // Update miss distance if we have a target
        if (missLbl != null && currentTarget != null && currentMissile != null)
        {
            float miss = Vector3.Distance(currentMissile.transform.position, currentTarget.position);
            missLbl.text = $"Miss: {miss:0.0} m";
        }
    }

    /// <summary>
    /// Setup PiP camera displays with RenderTextures
    /// </summary>
    private void SetupPipDisplays()
    {
        if (interceptorCamera != null && interceptorCameraView != null)
        {
            var rt = interceptorCamera.GetRenderTexture();
            if (rt != null)
            {
                // Convert RenderTexture to Background for UI Toolkit
                interceptorCameraView.style.backgroundImage = Background.FromRenderTexture(rt);
            }
        }

        if (threatCamera != null && threatCameraView != null)
        {
            var rt = threatCamera.GetRenderTexture();
            if (rt != null)
            {
                // Convert RenderTexture to Background for UI Toolkit
                threatCameraView.style.backgroundImage = Background.FromRenderTexture(rt);
            }
        }
    }

    /// <summary>
    /// Show or hide the interceptor PiP camera
    /// </summary>
    public void ShowInterceptorPip(bool show)
    {
        if (interceptorPip != null)
            interceptorPip.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
    }

    /// <summary>
    /// Show or hide the threat PiP camera
    /// </summary>
    public void ShowThreatPip(bool show)
    {
        if (threatPip != null)
            threatPip.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
