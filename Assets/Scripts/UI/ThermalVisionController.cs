using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Toggleable thermal/night vision post-processing effect for the main camera.
/// Press T to toggle the effect on/off.
/// Works with URP by controlling the ThermalVisionFeature.
/// </summary>
public class ThermalVisionController : MonoBehaviour
{
    [Header("Toggle")]
    [Tooltip("Key to toggle thermal vision on/off")]
    public Key toggleKey = Key.T;

    [Tooltip("Start with thermal vision enabled")]
    public bool startEnabled = false;

    [Header("Thermal Settings")]
    [Range(0.5f, 3.0f)]
    public float contrast = 1.5f;

    [Range(-0.5f, 0.5f)]
    public float brightness = 0.0f;

    [Range(0f, 1f)]
    [Tooltip("Threshold for detecting hot spots (lower = more areas glow)")]
    public float hotThreshold = 0.7f;

    [Header("Bloom (Hot Object Glow)")]
    [Range(0f, 2f)]
    [Tooltip("Intensity of the bloom/glow on hot objects")]
    public float bloomIntensity = 0.8f;

    [Range(0f, 1f)]
    [Tooltip("Brightness threshold for bloom (lower = more objects bloom)")]
    public float bloomThreshold = 0.5f;

    [Range(1f, 20f)]
    [Tooltip("Size/spread of the bloom effect")]
    public float bloomRadius = 6.0f;

    [Header("Noise & Scanlines")]
    [Range(0f, 0.15f)]
    public float noiseAmount = 0.03f;

    [Range(0f, 1f)]
    public float scanlineIntensity = 0.1f;

    [Range(100f, 800f)]
    public float scanlineCount = 300f;

    [Header("Vignette")]
    [Range(0f, 1f)]
    public float vignetteIntensity = 0.4f;

    // Static instance for the renderer feature to access
    public static ThermalVisionController Instance { get; private set; }
    public bool IsEnabled { get; private set; }
    public Material ThermalMaterial { get; private set; }

    private static readonly int ContrastID = Shader.PropertyToID("_Contrast");
    private static readonly int BrightnessID = Shader.PropertyToID("_Brightness");
    private static readonly int NoiseAmountID = Shader.PropertyToID("_NoiseAmount");
    private static readonly int ScanlineIntensityID = Shader.PropertyToID("_ScanlineIntensity");
    private static readonly int ScanlineCountID = Shader.PropertyToID("_ScanlineCount");
    private static readonly int VignetteIntensityID = Shader.PropertyToID("_VignetteIntensity");
    private static readonly int HotThresholdID = Shader.PropertyToID("_HotThreshold");
    private static readonly int BloomIntensityID = Shader.PropertyToID("_BloomIntensity");
    private static readonly int BloomThresholdID = Shader.PropertyToID("_BloomThreshold");
    private static readonly int BloomRadiusID = Shader.PropertyToID("_BloomRadius");

    void Awake()
    {
        Instance = this;

        // Create material from shader
        Shader shader = Shader.Find("Hidden/ThermalVision");
        if (shader != null)
        {
            ThermalMaterial = new Material(shader);
            ThermalMaterial.hideFlags = HideFlags.HideAndDontSave;
        }
        else
        {
            Debug.LogError("[ThermalVision] Could not find ThermalVision shader! Make sure ThermalVision.shader exists in Assets/Shaders/");
            enabled = false;
            return;
        }

        IsEnabled = startEnabled;
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb[toggleKey].wasPressedThisFrame)
        {
            IsEnabled = !IsEnabled;
            Debug.Log($"[ThermalVision] {(IsEnabled ? "ENABLED" : "DISABLED")}");
        }

        // Update material properties
        if (ThermalMaterial != null)
        {
            ThermalMaterial.SetFloat(ContrastID, contrast);
            ThermalMaterial.SetFloat(BrightnessID, brightness);
            ThermalMaterial.SetFloat(NoiseAmountID, noiseAmount);
            ThermalMaterial.SetFloat(ScanlineIntensityID, scanlineIntensity);
            ThermalMaterial.SetFloat(ScanlineCountID, scanlineCount);
            ThermalMaterial.SetFloat(VignetteIntensityID, vignetteIntensity);
            ThermalMaterial.SetFloat(HotThresholdID, hotThreshold);
            ThermalMaterial.SetFloat(BloomIntensityID, bloomIntensity);
            ThermalMaterial.SetFloat(BloomThresholdID, bloomThreshold);
            ThermalMaterial.SetFloat(BloomRadiusID, bloomRadius);
        }
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        if (ThermalMaterial != null)
        {
            DestroyImmediate(ThermalMaterial);
        }
    }

    void OnGUI()
    {
        if (IsEnabled)
        {
            // Draw indicator in top-right corner
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.fontSize = 14;
            style.fontStyle = FontStyle.Bold;
            style.normal.textColor = new Color(0.7f, 1f, 0.7f);

            GUI.Label(new Rect(Screen.width - 160, 10, 150, 25), "THERMAL [T]", style);
        }
    }
}
