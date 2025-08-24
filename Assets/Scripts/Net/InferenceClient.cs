using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.IO;
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

    [Tooltip("ThreatSpawner provides CurrentThreat transform")]
    public ThreatSpawner threatSpawner;

    [Header("Session State (read-only)")]
    public bool sessionActive;
    public bool serverHealthy;
    public float lastLatencyMs;

    // last received action (main thread only)
    float   lastThrust01    = 0f;
    Vector3 lastRateCmdBody = Vector3.zero; // rad/s, body frame

    // episode bookkeeping
    string episodeId;
    float  sessionStartTime;
    int    simTick;

    Coroutine loop;

    // cache for initial fuel mass per interceptor instance
    readonly Dictionary<int, float> initialFuelKg = new Dictionary<int, float>();

    // debug
    float logTimer;
    bool  loggedLast422 = false;

    [Header("Debug Dump")]
    [Tooltip("Dump first N requests as JSON files to persistentDataPath for diffing with Python client.")]
    public bool dumpFirstNRequests = true;
    public int dumpN = 3;

    void Awake()
    {
        if (!threatSpawner) threatSpawner = Object.FindFirstObjectByType<ThreatSpawner>();

        // Guard against accidentally assigned prefab (not scene instance)
        if (currentInterceptor && !currentInterceptor.scene.IsValid())
        {
            Debug.LogWarning("[InferenceClient] currentInterceptor is a prefab asset; clearing. It will be set by the spawner at runtime.");
            currentInterceptor = null;
        }
    }

    void Update()
    {
        // Hotkeys:
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null)
        {
            if (kb.yKey.wasPressedThisFrame) StartSession();
            if (kb.uKey.wasPressedThisFrame) StopSession();

            // Diagnostics: toggle frames mid-run (for testing only)
            if (kb.f1Key.wasPressedThisFrame)
            {
                config.sendENU = !config.sendENU;
                Debug.Log($"[InferenceClient] Toggled sendENU -> {config.sendENU}");
            }
            if (kb.f2Key.wasPressedThisFrame)
            {
                // flip unity_lh flag only (keeps ENU mapping as-is)
                _unityLHOverride = !_unityLHOverride;
                Debug.Log($"[InferenceClient] Toggled unity_lh -> {_unityLHOverride}");
            }
        }
    }

    // Local override for frames.unity_lh diagnostics
    bool _unityLHOverride = true;

    // ---- Public API ----
    public void StartSession()
    {
        if (loop != null) StopCoroutine(loop);
        episodeId = $"unity_episode_{System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
        sessionStartTime = Time.time;
        simTick = 0;
        loggedLast422 = false;
        Debug.Log($"[InferenceClient] StartSession: {episodeId}");
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

        Debug.Log("[InferenceClient] StopSession");
    }

    public void SetInterceptor(GameObject go) => currentInterceptor = go;

    public float GetThrust01() => lastThrust01;
    public Vector3 GetDesiredBodyRates() => lastRateCmdBody;

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
            // Build request object matching server schema (sanitized)
            var reqObj = BuildRequestObject(out bool ok, out float closingPos, out float closingNeg);
            if (!ok)
            {
                // If we have no interceptor or threat yet, just wait
                yield return new WaitForSeconds(interval);
                continue;
            }

            var json = JsonUtility.ToJson(reqObj);
            var body = Encoding.UTF8.GetBytes(json);

            // Dump first N requests for offline comparison
            if (dumpFirstNRequests && simTick < dumpN)
            {
                var path = Path.Combine(Application.persistentDataPath, $"inference_req_tick{simTick}.json");
                try { File.WriteAllText(path, json); Debug.Log($"[InferenceClient] Wrote {path}"); } catch {}
            }

            // Show both interpretations of closing speed (sanity aid)
            if (simTick % 10 == 0)
                Debug.Log($"[InferenceClient] closing(+approach)={closingPos:0.00}  closingAlt={closingNeg:0.00}  range={reqObj.guidance.range_m:0.0}m");

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
                    loggedLast422 = false;

                    var txt = req.downloadHandler.text;
                    InferenceResponse resp = null;
                    try { resp = JsonUtility.FromJson<InferenceResponse>(txt); }
                    catch { /* tolerate schema drift */ }

                    if (resp != null && resp.action != null)
                    {
                        lastThrust01 = Mathf.Clamp01(resp.action.thrust_cmd);
                        if (resp.action.rate_cmd_radps != null)
                        {
                            lastRateCmdBody = new Vector3(
                                San(resp.action.rate_cmd_radps.x),
                                San(resp.action.rate_cmd_radps.y),
                                San(resp.action.rate_cmd_radps.z));
                        }
                    }
                }
                else
                {
                    // 422 or other: log server response and the JSON we sent (once per failure burst)
                    if (!loggedLast422)
                    {
                        loggedLast422 = true;
                        Debug.LogError($"[InferenceClient] HTTP {(int)req.responseCode} {req.error}\nServer says: {req.downloadHandler?.text}\nLast JSON sent:\n{json}");
                    }
                    // network hiccup: hold last command, optionally decay thrust
                    lastThrust01 = Mathf.MoveTowards(lastThrust01, 0f, 0.25f);
                }
            }

            // 1 Hz debug: show the last action
            logTimer += interval;
            if (logTimer >= 1f)
            {
                logTimer = 0f;
                Debug.Log($"[InferenceClient] action thrust={lastThrust01:0.00} rate(p,y,r)=({lastRateCmdBody.x:0.00},{lastRateCmdBody.y:0.00},{lastRateCmdBody.z:0.00})  latency={lastLatencyMs:0}ms tick={simTick}");
            }

            simTick++;
            yield return new WaitForSeconds(interval);
        }
    }

    // ---------- Build server request per provided schema (with sanitization) ----------
    InferenceRequest BuildRequestObject(out bool ok, out float closingPos, out float closingNeg)
    {
        ok = false;
        closingPos = 0f;
        closingNeg = 0f;

        if (!currentInterceptor) return new InferenceRequest();
        var threatTf = ThreatSpawner.CurrentThreat;
        if (!threatTf) return new InferenceRequest();

        var ibody = currentInterceptor.GetComponent<Rigidbody>();
        var tbody = threatTf.GetComponent<Rigidbody>();

        // World (Unity) vectors
        Vector3 Pi_u = currentInterceptor.transform.position;
        Vector3 Vi_u = ibody ? ibody.linearVelocity : Vector3.zero;
        Vector3 Wi_u = ibody ? ibody.angularVelocity : Vector3.zero;

        Vector3 Pt_u = threatTf.position;
        Vector3 Vt_u = tbody ? tbody.linearVelocity : Vector3.zero;

        // Convert to ENU if requested
        Vector3 Pi = config.sendENU ? UnityToENU(Pi_u) : Pi_u;
        Vector3 Vi = config.sendENU ? UnityToENU(Vi_u) : Vi_u;
        Vector3 Wi = config.sendENU ? UnityToENU(Wi_u) : Wi_u;

        Vector3 Pt = config.sendENU ? UnityToENU(Pt_u) : Pt_u;
        Vector3 Vt = config.sendENU ? UnityToENU(Vt_u) : Vt_u;

        // Orientation quaternions (ENU RH) as wxyz (normalized)
        float[] Qi = QuatWXYZ_ENU_FromUnity(currentInterceptor.transform);
        float[] Qt = QuatWXYZ_ENU_FromUnity(threatTf);

        // Guidance features (all in ENU world frame)
        Vector3 r = Pt - Pi;                // interceptor -> threat
        float range = San(Mathf.Max(0f, r.magnitude));
        Vector3 los = range > 1e-6f ? r / range : new Vector3(1f, 0f, 0f); // safe default

        Vector3 relV = Vt - Vi;             // threat relative to interceptor

        // Two definitions to compare:
        // closingPos: positive when approaching (our current choice)
        closingPos = San(-Vector3.Dot(los, relV));
        // closingNeg: negative when approaching (alternate)
        closingNeg = San(Vector3.Dot(los, relV));

        // LOS rate ω ≈ (r × r_dot) / |r|^2
        Vector3 losRate = Vector3.zero;
        float r2 = range * range;
        if (r2 > 1e-6f)
        {
            losRate = Vector3.Cross(r, relV) / r2;
            losRate = San(losRate);
        }

        // Fuel fraction
        float fuelFrac = San01(TryFuelFracNormalized(currentInterceptor));

        // Meta/env
        float tNow = San(Time.time - sessionStartTime);
        float dt = San(Time.fixedDeltaTime);

        var req = new InferenceRequest
        {
            meta = new MetaBlock
            {
                episode_id = episodeId,
                t = tNow,
                dt = dt,
                sim_tick = simTick
            },
            frames = new FramesBlock
            {
                frame = "ENU",
                unity_lh = _unityLHOverride
            },
            blue = new BlueRedBlock
            {
                pos_m = Arr3(San(Pi)),
                vel_mps = Arr3(San(Vi)),
                quat_wxyz = Arr4(Qi),
                ang_vel_radps = Arr3(San(Wi)),
                fuel_frac = fuelFrac
            },
            red = new BlueRedBlock
            {
                pos_m = Arr3(San(Pt)),
                vel_mps = Arr3(San(Vt)),
                quat_wxyz = Arr4(Qt),
                ang_vel_radps = null, // optional; omit for red
                fuel_frac = 0f        // optional; omit for red
            },
            guidance = new GuidanceBlock
            {
                los_unit = Arr3(San(los)),
                los_rate_radps = Arr3(San(losRate)),
                range_m = range,
                closing_speed_mps = closingPos, // <-- currently using "positive when approaching"
                fov_ok = true,
                g_limit_ok = true
            },
            env = new EnvBlock
            {
                wind_mps = Arr3(San(config.wind_mps)),
                noise_std = San(config.noise_std),
                episode_step = simTick,
                max_steps = Mathf.Max(1, config.max_steps)
            },
            normalization = new NormBlock
            {
                obs_version = string.IsNullOrEmpty(config.obs_version) ? "obs_v1.0" : config.obs_version,
                vecnorm_stats_id = config.vecnorm_stats_id
            }
        };

        ok = true;
        return req;
    }

    // ---------- Helpers & Sanitizers ----------
    static float San(float x) => (float)(float.IsNaN(x) || float.IsInfinity(x) ? 0.0 : x);
    static Vector3 San(Vector3 v) => new Vector3(San(v.x), San(v.y), San(v.z));
    static float San01(float x) => Mathf.Clamp01(San(x));

    static float[] Arr3(Vector3 v) => new float[] { v.x, v.y, v.z };
    static float[] Arr4(float[] q)
    {
        if (q == null || q.Length != 4) return new float[] { 1f, 0f, 0f, 0f };
        return q;
    }

    // Unity (X,Y,Z) -> ENU (x=X, y=Z, z=Y)
    public static Vector3 UnityToENU(Vector3 v) => new Vector3(v.x, v.z, v.y);

    // Build ENU quaternion (wxyz) from Unity transform: columns of ENU basis = [right_ENU, up_ENU, forward_ENU]
    static float[] QuatWXYZ_ENU_FromUnity(Transform t)
    {
        Vector3 ex = UnityToENU(t.right).normalized;   // object right in ENU
        Vector3 ey = UnityToENU(t.up).normalized;      // object up    in ENU
        Vector3 ez = UnityToENU(t.forward).normalized; // object fwd   in ENU

        Quaternion q = QuaternionFromBasisRH(ex, ey, ez);
        // normalize and sanitize
        float mag = Mathf.Sqrt(q.w*q.w + q.x*q.x + q.y*q.y + q.z*q.z);
        if (mag > 1e-8f) { q.w /= mag; q.x /= mag; q.y /= mag; q.z /= mag; }
        else { q = Quaternion.identity; }
        return new float[] { San(q.w), San(q.x), San(q.y), San(q.z) };
    }

    // Create RH quaternion from orthonormal basis (columns ex,ey,ez)
    static Quaternion QuaternionFromBasisRH(Vector3 ex, Vector3 ey, Vector3 ez)
    {
        float m00 = ex.x, m01 = ey.x, m02 = ez.x;
        float m10 = ex.y, m11 = ey.y, m12 = ez.y;
        float m20 = ex.z, m21 = ey.z, m22 = ez.z;

        float trace = m00 + m11 + m22;
        float w, x, y, z;

        if (trace > 0f)
        {
            float s = Mathf.Sqrt(trace + 1f) * 2f;
            w = 0.25f * s;
            x = (m21 - m12) / s;
            y = (m02 - m20) / s;
            z = (m10 - m01) / s;
        }
        else if (m00 > m11 && m00 > m22)
        {
            float s = Mathf.Sqrt(1f + m00 - m11 - m22) * 2f;
            w = (m21 - m12) / s;
            x = 0.25f * s;
            y = (m01 + m10) / s;
            z = (m02 + m20) / s;
        }
        else if (m11 > m22)
        {
            float s = Mathf.Sqrt(1f + m11 - m00 - m22) * 2f;
            w = (m02 - m20) / s;
            x = (m01 + m10) / s;
            y = 0.25f * s;
            z = (m12 + m21) / s;
        }
        else
        {
            float s = Mathf.Sqrt(1f + m22 - m00 - m11) * 2f;
            w = (m10 - m01) / s;
            x = (m02 + m20) / s;
            y = (m12 + m21) / s;
            z = 0.25f * s;
        }
        return new Quaternion(x, y, z, w);
    }

    float TryFuelFracNormalized(GameObject go)
    {
        var fuel = go.GetComponent<FuelSystem>();
        if (!fuel) return 0f;
        int id = go.GetInstanceID();
        if (!initialFuelKg.ContainsKey(id))
            initialFuelKg[id] = Mathf.Max(1e-6f, fuel.fuelKg); // capture starting fuel as "max"
        return Mathf.Clamp01(fuel.fuelKg / initialFuelKg[id]);
    }
}

// ---------------- DTOs matching server schema ----------------
[System.Serializable]
public sealed class InferenceRequest
{
    public MetaBlock meta;
    public FramesBlock frames;
    public BlueRedBlock blue;
    public BlueRedBlock red;
    public GuidanceBlock guidance;
    public EnvBlock env;
    public NormBlock normalization;
}

[System.Serializable] public sealed class MetaBlock
{
    public string episode_id;
    public float t;
    public float dt;
    public int sim_tick;
}

[System.Serializable] public sealed class FramesBlock
{
    public string frame;    // "ENU"
    public bool unity_lh;   // true
}

[System.Serializable] public sealed class BlueRedBlock
{
    public float[] pos_m;
    public float[] vel_mps;
    public float[] quat_wxyz;      // [w,x,y,z]
    public float[] ang_vel_radps;  // optional (only blue)
    public float   fuel_frac;      // optional (only blue)
}

[System.Serializable] public sealed class GuidanceBlock
{
    public float[] los_unit;
    public float[] los_rate_radps;
    public float   range_m;
    public float   closing_speed_mps;
    public bool    fov_ok;
    public bool    g_limit_ok;
}

[System.Serializable] public sealed class EnvBlock
{
    public float[] wind_mps;
    public float   noise_std;
    public int     episode_step;
    public int     max_steps;
}

[System.Serializable] public sealed class NormBlock
{
    public string obs_version;
    public string vecnorm_stats_id;
}

[System.Serializable] public sealed class InferenceResponse
{
    public ActionBlock action;
    public SimulationState simulation_state; // optional
}

[System.Serializable] public sealed class ActionBlock
{
    public float thrust_cmd;
    public Vec3C rate_cmd_radps; // x=pitch, y=yaw, z=roll
}

[System.Serializable] public sealed class SimulationState
{
    public AgentStateC blue;
    public AgentStateC red;
}

[System.Serializable] public sealed class AgentStateC
{
    public float[] pos_m;
    public float[] vel_mps;
    public float[] quat_wxyz;
}

[System.Serializable] public sealed class Vec3C
{
    public float x, y, z;
}
