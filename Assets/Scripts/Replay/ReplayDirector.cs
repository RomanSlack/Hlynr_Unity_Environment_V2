using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Replay
{
    /// Master controller: loads JSONL episode, spawns agents, and advances playback.
    [AddComponentMenu("Simulation/Replay/Replay Director")]
    public sealed class ReplayDirector : MonoBehaviour
    {
        [Header("File")]
        [Tooltip("If relative, it is resolved under StreamingAssets. Example: Replays/ep_000001.jsonl")]
        public string episodePath = "Replays/ep_000001.jsonl";

        [Header("Prefabs")]
        public GameObject interceptorPrefab;  // your Missile_Prefab
        public GameObject threatPrefab;       // your ThreatRocket prefab (or simple cube)

        [Header("Modes")]
        public ReplayMode interceptorMode = ReplayMode.PhysicsFromActions;
        public ReplayMode threatMode      = ReplayMode.Kinematic;

        [Header("Playback")]
        [Range(0.1f, 4f)] public float playSpeed = 1f;
        public bool autoPlay = true;

        [Header("Time/DT")]
        public bool matchHeaderFixedDelta = true;

        // runtime
        bool paused;
        float t;                  // current replay time (seconds)
        float dtNominal = 0.01f;  // from header
        string epId = "unknown";

        // parsed data
        List<TimestepLine> frames = new List<TimestepLine>();
        HeaderLine header;
        SummaryLine summary;

        // agents
        AgentReplayer blue;   // interceptor_0
        AgentReplayer red;    // threat_0

        int idx;              // current frame index (frames[idx] <= t < frames[idx+1])

        void Awake()
        {
            LoadEpisode(ResolvePath(episodePath));
            SpawnAgents();

            if (matchHeaderFixedDelta && header != null && header.meta != null && header.meta.dt_nominal > 0f)
            {
                dtNominal = header.meta.dt_nominal;
                Time.fixedDeltaTime = dtNominal;
            }

            paused = !autoPlay;
            t = (frames.Count > 0 ? frames[0].t : 0f);
            idx = 0;
            SnapToTime(t, force:true);
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.spaceKey.wasPressedThisFrame) paused = !paused;
                if (kb.rKey.wasPressedThisFrame) { t = frames.Count>0 ? frames[0].t : 0f; idx = 0; SnapToTime(t, force:true); paused = !autoPlay; }
                if (kb.digit1Key.wasPressedThisFrame) playSpeed = 0.5f;
                if (kb.digit2Key.wasPressedThisFrame) playSpeed = 1f;
                if (kb.digit3Key.wasPressedThisFrame) playSpeed = 2f;
                if (kb.rightArrowKey.wasPressedThisFrame && paused) StepOnce();
            }
        }

        void FixedUpdate()
        {
            if (frames.Count < 2) return;
            if (!paused) t += Time.fixedDeltaTime * playSpeed;

            // clamp to last frame
            if (t >= frames[frames.Count-1].t)
            {
                t = frames[frames.Count-1].t;
                idx = Mathf.Max(0, frames.Count - 2);
            }

            // advance frame index
            while (idx+1 < frames.Count && frames[idx+1].t <= t) idx++;

            // Interpolate between idx and idx+1
            var a = frames[idx];
            var b = (idx+1 < frames.Count) ? frames[idx+1] : a;
            float span = Mathf.Max(1e-6f, b.t - a.t);
            float alpha = Mathf.Clamp01((t - a.t) / span);

            // Apply interceptor
            if (blue != null && a.agents?.interceptor_0 != null)
                ApplyAgent(blue, a.agents.interceptor_0, b.agents?.interceptor_0, alpha);

            // Apply threat
            if (red != null && a.agents?.threat_0 != null)
                ApplyAgent(red, a.agents.threat_0, b.agents?.threat_0, alpha);
        }

        void ApplyAgent(AgentReplayer agent, AgentState A, AgentState B, float alpha)
        {
            if (agent.mode == ReplayMode.Kinematic)
            {
                // Interpolate pose
                Vector3 pA = new Vector3(A.p[0], A.p[1], A.p[2]);
                Vector3 pB = (B != null && B.p != null) ? new Vector3(B.p[0], B.p[1], B.p[2]) : pA;

                // Convert/world->body quats to Unity, then slerp in Unity space
                Quaternion qA = EnuUnity.UnityRotationFromEnuWorldToBody(A.q[0], A.q[1], A.q[2], A.q[3]);
                Quaternion qB = (B != null && B.q != null)
                    ? EnuUnity.UnityRotationFromEnuWorldToBody(B.q[0], B.q[1], B.q[2], B.q[3]) : qA;

                Vector3 pU = EnuUnity.ENUtoUnity(Vector3.Lerp(pA, pB, alpha));
                Quaternion qU = Quaternion.Slerp(qA, qB, alpha);
                agent.ApplyKinematic(pU, qU);
            }
            else // PhysicsFromActions
            {
                // Use latest available actions (zero-order hold)
                float[] u = (A.u != null && A.u.Length >= 4) ? A.u : (B?.u);
                agent.ApplyActions(u);
            }
        }

        void StepOnce()
        {
            // advance by one nominal frame
            t = Mathf.Min(t + dtNominal, frames[frames.Count-1].t);
            SnapToTime(t, force:true);
        }

        void SnapToTime(float time, bool force)
        {
            if (frames.Count == 0) return;
            // find surrounding frames
            int j = Mathf.Clamp(BinarySearchByTime(time), 0, frames.Count-1);
            idx = Mathf.Max(0, j - 1);

            // apply exact pose at 'time' via interpolation with alpha
            var a = frames[idx];
            var b = (idx+1 < frames.Count) ? frames[idx+1] : a;
            float span = Mathf.Max(1e-6f, b.t - a.t);
            float alpha = Mathf.Clamp01((time - a.t) / span);

            if (blue != null && a.agents?.interceptor_0 != null)
                ApplyAgent(blue, a.agents.interceptor_0, b.agents?.interceptor_0, alpha);
            if (red != null && a.agents?.threat_0 != null)
                ApplyAgent(red, a.agents.threat_0, b.agents?.threat_0, alpha);
        }

        int BinarySearchByTime(float time)
        {
            int lo = 0, hi = frames.Count - 1;
            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1;
                float tm = frames[mid].t;
                if (tm < time) lo = mid + 1;
                else if (tm > time) hi = mid - 1;
                else return mid;
            }
            return lo;
        }

        void SpawnAgents()
        {
            // Use first timestep (after header) to place initial pose
            var first = frames.Count > 0 ? frames[0] : null;
            if (first == null || first.agents == null) { Debug.LogError("[ReplayDirector] No frames/agents."); return; }

            // Interceptor
            if (interceptorPrefab)
            {
                var go = Instantiate(interceptorPrefab);
                go.name = "Interceptor_Replay";
                blue = go.GetComponent<AgentReplayer>();
                if (!blue) blue = go.AddComponent<AgentReplayer>();
                blue.agentId = "interceptor_0";
                blue.ConfigureForMode(interceptorMode);
            }

            // Threat
            if (threatPrefab)
            {
                var go = Instantiate(threatPrefab);
                go.name = "Threat_Replay";
                red = go.GetComponent<AgentReplayer>();
                if (!red) red = go.AddComponent<AgentReplayer>();
                red.agentId = "threat_0";
                red.ConfigureForMode(threatMode);
            }
        }

        void LoadEpisode(string path)
        {
            if (!File.Exists(path))
            {
                Debug.LogError($"[ReplayDirector] File not found: {path}");
                return;
            }

            frames.Clear();
            header = null;
            summary = null;

            using (var sr = new StreamReader(path))
            {
                string line;
                bool headerParsed = false;
                while ((line = sr.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (line.Length == 0) continue;

                    if (!headerParsed && line.Contains("\"meta\"") && line.Contains("\"scene\""))
                    {
                        header = JsonUtility.FromJson<HeaderLine>(line);
                        headerParsed = true;
                        epId = header?.meta?.ep_id ?? "unknown";
                        dtNominal = header?.meta?.dt_nominal ?? dtNominal;
                        continue;
                    }

                    if (line.Contains("\"agents\""))
                    {
                        var ts = JsonUtility.FromJson<TimestepLine>(line);
                        if (ts != null && ts.agents != null &&
                            ts.agents.interceptor_0 != null && ts.agents.threat_0 != null)
                        {
                            frames.Add(ts);
                        }
                        continue;
                    }

                    if (line.Contains("\"summary\""))
                    {
                        summary = JsonUtility.FromJson<SummaryLine>(line);
                        break;
                    }
                }
            }

            if (frames.Count < 2)
                Debug.LogWarning($"[ReplayDirector] Episode loaded but only {frames.Count} timestep lines found.");

            Debug.Log($"[ReplayDirector] Loaded '{Path.GetFileName(path)}' ep_id={epId} frames={frames.Count} dt_nominal={dtNominal:0.###}s");
        }

        string ResolvePath(string relOrAbs)
        {
            if (Path.IsPathRooted(relOrAbs)) return relOrAbs;
            // StreamingAssets is backed by filesystem on desktop
            return Path.Combine(Application.streamingAssetsPath, relOrAbs);
        }

        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(8, 8, 360, 120), GUI.skin.box);
            GUILayout.Label($"Episode: {epId}");
            GUILayout.Label($"t = {t:0.00}s   speed = {playSpeed:0.##}x   paused = {paused}");
            GUILayout.Label($"Frames: {frames.Count}   dt_nominal = {dtNominal:0.###}s");
            GUILayout.Label($"Modes: interceptor={interceptorMode}  threat={threatMode}");
            GUILayout.EndArea();
        }
    }
}
