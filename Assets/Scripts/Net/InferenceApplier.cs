using UnityEngine;

[AddComponentMenu("Simulation/Net/Inference Applier")]
public sealed class InferenceApplier : MonoBehaviour
{
    public InferenceClient client;

    [Header("Debug / Override")]
    public bool logEverySecond = true;
    public bool showHud = true;

    [Tooltip("Enable to force a temporary yaw command to verify control path.")]
    public bool testOverride = false;
    public float testOverrideSecs = 3f;
    public float testYawRadPerSec = 0.5f; // +left / -right (unity Yaw about +Y)

    float logTimer;
    float overrideTimer;

    void Awake()
    {
        if (!client) client = Object.FindFirstObjectByType<InferenceClient>();
        if (!client) client = Object.FindAnyObjectByType<InferenceClient>(); // fallback
    }

    void OnEnable()
    {
        overrideTimer = 0f;
    }

    void FixedUpdate()
    {
        if (!client || !client.sessionActive) return;
        if (!client.currentInterceptor) return;

        var mux = client.currentInterceptor.GetComponent<RLMuxController>();
        if (!mux) return;

        float thrust = client.GetThrust01();
        Vector3 rate  = client.GetDesiredBodyRates(); // (pitch,x) (yaw,y) (roll,z)

        // Temporary functional test: force a yaw command for a few seconds
        if (testOverride && overrideTimer < testOverrideSecs)
        {
            overrideTimer += Time.fixedDeltaTime;
            rate = new Vector3(0f, testYawRadPerSec, 0f);
            thrust = Mathf.Max(thrust, 0.2f); // ensure some thrust while testing
        }

        mux.ApplyAction(thrust, rate);

        if (logEverySecond)
        {
            logTimer += Time.fixedDeltaTime;
            if (logTimer >= 1f)
            {
                logTimer = 0f;
                Debug.Log($"[InferenceApplier] thrust={thrust:0.00} rate(p,y,r)=({rate.x:0.00},{rate.y:0.00},{rate.z:0.00})  latency={client.lastLatencyMs:0}ms");
            }
        }
    }

    void OnDisable()
    {
        if (client && client.currentInterceptor)
        {
            var mux = client.currentInterceptor.GetComponent<RLMuxController>();
            if (mux) mux.DeactivateRL();
        }
    }

    void OnGUI()
    {
        if (!showHud || !client) return;
        const int pad = 8;
        GUILayout.BeginArea(new Rect(pad, pad, 320, 110), GUI.skin.box);
        GUILayout.Label($"RL Session: {(client.sessionActive ? "ON" : "OFF")}  Healthy: {client.serverHealthy}");
        GUILayout.Label($"Latency: {client.lastLatencyMs:0} ms");
        GUILayout.Label($"Action: thrust={client.GetThrust01():0.00}  PYR=({client.GetDesiredBodyRates().x:0.00}, {client.GetDesiredBodyRates().y:0.00}, {client.GetDesiredBodyRates().z:0.00})");
        GUILayout.EndArea();
    }
}
