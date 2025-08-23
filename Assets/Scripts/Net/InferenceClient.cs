using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

[AddComponentMenu("Simulation/Net/Inference Client")]
public sealed class InferenceClient : MonoBehaviour
{
    [Header("Config")]
    public InferenceConfig config;

    [Header("Scene Refs")]
    [Tooltip("Set automatically by InterceptorSpawner on launch")]
    public GameObject currentInterceptor;
    [Tooltip("ThreatSpawner provides CurrentThreat")]
    public ThreatSpawner threatSpawner;

    [Header("Session State (read-only)")]
    public bool sessionActive;
    public bool serverHealthy;
    public float lastLatencyMs;

    // last received action (main thread only)
    float   lastThrust01     = 0f;
    Vector3 lastRateCmdBody  = Vector3.zero; // rad/s, body frame

    Coroutine loop;

    // ---- Public API ----
    public void StartSession()
    {
        if (loop != null) StopCoroutine(loop);
        loop = StartCoroutine(SessionLoop());
    }

    public void StopSession()
    {
        sessionActive = false;
        serverHealthy = false;
        if (loop != null) StopCoroutine(loop);
        loop = null;
        // zero out action to be safe
        lastThrust01 = 0f;
        lastRateCmdBody = Vector3.zero;
    }

    public void SetInterceptor(GameObject go) => currentInterceptor = go;

    public float GetThrust01() => lastThrust01;
    public Vector3 GetDesiredBodyRates() => lastRateCmdBody;

    void Awake()
    {
        if (!threatSpawner) threatSpawner = Object.FindFirstObjectByType<ThreatSpawner>();
    }

    void Update()
    {
        // Hotkeys: Y start, U stop
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null)
        {
            if (kb.yKey.wasPressedThisFrame) StartSession();
            if (kb.uKey.wasPressedThisFrame) StopSession();
        }
    }

    IEnumerator SessionLoop()
    {
        if (!config)
        {
            Debug.LogError("[InferenceClient] No InferenceConfig assigned.");
            yield break;
        }

        // Wait for /healthz
        serverHealthy = false;
        var healthUrl = config.baseUrl.TrimEnd('/') + config.healthPath;
        while (!serverHealthy)
        {
            using (var req = UnityWebRequest.Get(healthUrl))
            {
                req.timeout = Mathf.CeilToInt(config.timeoutSec);
                var t0 = Time.realtimeSinceStartup;
                yield return req.SendWebRequest();
                lastLatencyMs = (Time.realtimeSinceStartup - t0) * 1000f;

                serverHealthy = req.result == UnityWebRequest.Result.Success && req.responseCode == 200;
            }
            if (!serverHealthy) yield return new WaitForSeconds(0.5f);
        }

        sessionActive = true;
        var interval = 1f / Mathf.Max(0.01f, config.pollHz);
        var inferUrl = config.baseUrl.TrimEnd('/') + config.inferencePath;

        while (sessionActive)
        {
            // Build observation
            var obs = BuildObservation();
            var json = JsonUtility.ToJson(obs);
            var body = Encoding.UTF8.GetBytes(json);

            using (var req = new UnityWebRequest(inferUrl, "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(body);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = Mathf.CeilToInt(config.timeoutSec);

                var t0 = Time.realtimeSinceStartup;
                yield return req.SendWebRequest();
                lastLatencyMs = (Time.realtimeSinceStartup - t0) * 1000f;

                if (req.result == UnityWebRequest.Result.Success && req.responseCode == 200)
                {
                    var txt = req.downloadHandler.text;
                    InferenceResponse resp = null;
                    try { resp = JsonUtility.FromJson<InferenceResponse>(txt); }
                    catch { /* tolerate schema drift */ }

                    if (resp != null && resp.action != null)
                    {
                        lastThrust01 = Mathf.Clamp01(resp.action.thrust_cmd);
                        if (resp.action.rate_cmd_radps != null) // now a reference type, so null-check is valid
                        {
                            lastRateCmdBody = new Vector3(
                                resp.action.rate_cmd_radps.x,
                                resp.action.rate_cmd_radps.y,
                                resp.action.rate_cmd_radps.z);
                        }
                    }
                }
                else
                {
                    // network hiccup: hold last command, optionally decay thrust
                    lastThrust01 = Mathf.MoveTowards(lastThrust01, 0f, 0.25f);
                }
            }

            yield return new WaitForSeconds(interval);
        }
    }

    Observation BuildObservation()
    {
        var o = new Observation
        {
            time = Time.time
        };

        // Interceptor (blue)
        if (currentInterceptor)
        {
            var tr = currentInterceptor.transform;
            var rb = currentInterceptor.GetComponent<Rigidbody>();

            var p = tr.position;
            var v = rb ? rb.linearVelocity : Vector3.zero;
            var fwd = tr.forward;
            var up = tr.up;
            var w = rb ? rb.angularVelocity : Vector3.zero;

            if (config.sendENU)
            {
                p = UnityToENU(p);
                v = UnityToENU(v);
                fwd = UnityToENU(fwd);
                up = UnityToENU(up);
                w = UnityToENU(w); // angular velocity axes permute the same way
            }

            o.blue = new AgentState
            {
                id = config.interceptorId,
                p = new Vec3(p),
                v = new Vec3(v),
                fwd = new Vec3(fwd),
                up = new Vec3(up),
                w = new Vec3(w),
                fuel_frac = TryFuelFrac(currentInterceptor)
            };
        }

        // Threat (red)
        var threatTf = ThreatSpawner.CurrentThreat;
        if (threatTf)
        {
            var tr = threatTf;
            var rb = tr.GetComponent<Rigidbody>();

            var p = tr.position;
            var v = rb ? rb.linearVelocity : Vector3.zero;
            var fwd = tr.forward;
            var up = tr.up;

            if (config.sendENU)
            {
                p = UnityToENU(p);
                v = UnityToENU(v);
                fwd = UnityToENU(fwd);
                up = UnityToENU(up);
            }

            o.red = new AgentState
            {
                id = config.threatId,
                p = new Vec3(p),
                v = new Vec3(v),
                fwd = new Vec3(fwd),
                up = new Vec3(up)
            };
        }

        return o;
    }

    static float TryFuelFrac(GameObject go)
    {
        var fuel = go.GetComponent<FuelSystem>();
        if (!fuel) return 0f;
        // Placeholder: send mass as a pseudo-fraction (adjust if you track initial mass)
        return Mathf.Clamp01(fuel.fuelKg / Mathf.Max(0.0001f, fuel.fuelKg + 0.0001f));
    }

    // Unity (X,Y,Z) -> ENU (x=X, y=Z, z=Y)
    public static Vector3 UnityToENU(Vector3 v) => new Vector3(v.x, v.z, v.y);
}

// -------- Simple DTOs (JsonUtility-compatible) --------
[System.Serializable]
public sealed class Observation
{
    public float time;
    public AgentState blue; // interceptor
    public AgentState red;  // threat
}

[System.Serializable]
public sealed class AgentState
{
    public string id;
    public Vec3 p;
    public Vec3 v;
    public Vec3 fwd;
    public Vec3 up;
    public Vec3 w;       // optional (blue only)
    public float fuel_frac; // optional
}

[System.Serializable]
public sealed class InferenceResponse
{
    public ActionBlock action;
    public SimulationState simulation_state; // optional, ignored
}

[System.Serializable]
public sealed class ActionBlock
{
    public float thrust_cmd;

    // Use a reference type so null-check works when the field is absent in JSON.
    public Vec3C rate_cmd_radps; // x=pitch, y=yaw, z=roll (body-frame)
}

[System.Serializable]
public sealed class SimulationState
{
    public AgentState blue;
    public AgentState red;
}

[System.Serializable]
public struct Vec3
{
    public float x, y, z;
    public Vec3(Vector3 v) { x = v.x; y = v.y; z = v.z; }
}

[System.Serializable]
public sealed class Vec3C
{
    public float x, y, z;
}
