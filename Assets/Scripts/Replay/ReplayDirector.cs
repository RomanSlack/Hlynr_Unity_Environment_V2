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
        [Tooltip("If relative, resolved under StreamingAssets. Example: Replays/ep_000001.jsonl")]
        public string episodePath = "Replays/ep_000001.jsonl";

        [Header("Prefabs")]
        public GameObject interceptorPrefab;  // your Missile_Prefab
        public GameObject threatPrefab;       // your ThreatRocket prefab (or simple visual)

        [Header("Modes")]
        public ReplayMode interceptorMode = ReplayMode.PhysicsFromActions;
        public ReplayMode threatMode      = ReplayMode.Kinematic;

        [Header("Playback")]
        [Range(0.1f, 4f)] public float playSpeed = 1f;
        public bool autoPlay = true;

        [Header("Time/DT")]
        public bool matchHeaderFixedDelta = true;

        [Header("Anchoring / Offsets")]
        [Tooltip("If set, the defended target (aim_point) will be placed at this Transform.position.")]
        public Transform anchor;                   // optional
        [Tooltip("If true, subtract scene.threat_0.aim_point (ENU) from all positions before mapping to Unity.")]
        public bool useAimPointAsOrigin = true;
        [Tooltip("Additional ENU offset to subtract (applied after aim_point).")]
        public Vector3 enuAdditionalOffset = Vector3.zero;

        [Header("Startup")]
        [Tooltip("Number of physics ticks to keep agents kinematic at t0 (prevents early collisions/drift).")]
        public int initialFreezeTicks = 2;

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

        // anchoring state
        Vector3 enuAnchorOffset = Vector3.zero; // subtract from ENU before mapping
        Vector3 worldAnchorAdd  = Vector3.zero; // add in Unity after mapping

        // freeze at start
        int   freezeLeft;
        Pose  interceptPose0, threatPose0;

        void Awake()
        {
            LoadEpisode(ResolvePath(episodePath));
            ComputeAnchors();
            CacheFirstPoses();
            SpawnAgentsFrozenAtFirstFrame();

            if (matchHeaderFixedDelta && header?.meta?.dt_nominal > 0f)
            {
                dtNominal = header.meta.dt_nominal;
                Time.fixedDeltaTime = dtNominal;
            }

            paused = !autoPlay;
            t = (frames.Count > 0 ? frames[0].t : 0f);
            idx = 0;

            var ip = frames[0].agents.interceptor_0.p;
            var tp = frames[0].agents.threat_0.p;
            Debug.Log($"[ReplayDirector] t0 ENU interceptor p=({ip[0]:0.###},{ip[1]:0.###},{ip[2]:0.###}) -> Unity {interceptPose0.position}");
            Debug.Log($"[ReplayDirector] t0 ENU threat      p=({tp[0]:0.###},{tp[1]:0.###},{tp[2]:0.###}) -> Unity {threatPose0.position}");
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

            // Freeze window: hold exact t0 pose; keep both kinematic
            if (freezeLeft > 0)
            {
                freezeLeft--;
                if (blue)  { blue.SetKinematic(true);  blue.ForceKinematicPose(interceptPose0.position, interceptPose0.rotation); }
                if (red)   { red.SetKinematic(true);   red.ForceKinematicPose(threatPose0.position,    threatPose0.rotation);    }

                if (freezeLeft == 0)
                {
                    // Switch to requested modes after deterministic placement
                    if (blue) blue.ConfigureForMode(interceptorMode);
                    if (red)  red.ConfigureForMode(threatMode);
                }
                return;
            }

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

            if (blue != null && a.agents?.interceptor_0 != null)
                ApplyAgent(blue, a.agents.interceptor_0, b.agents?.interceptor_0, alpha);

            if (red != null && a.agents?.threat_0 != null)
                ApplyAgent(red, a.agents.threat_0, b.agents?.threat_0, alpha);
        }

        void ApplyAgent(AgentReplayer agent, AgentState A, AgentState B, float alpha)
        {
            if (agent.mode == ReplayMode.Kinematic)
            {
                // Interpolate pose (with offsets)
                Vector3 pA = new Vector3(A.p[0], A.p[1], A.p[2]);
                Vector3 pB = (B != null && B.p != null) ? new Vector3(B.p[0], B.p[1], B.p[2]) : pA;

                Quaternion qA = EnuUnity.UnityRotationFromEnuWorldToBody(A.q[0], A.q[1], A.q[2], A.q[3]);
                Quaternion qB = (B != null && B.q != null)
                    ? EnuUnity.UnityRotationFromEnuWorldToBody(B.q[0], B.q[1], B.q[2], B.q[3]) : qA;

                Vector3 pENU = Vector3.Lerp(pA, pB, alpha);
                Vector3 pU   = MapEnuToUnityWithAnchor(pENU);
                Quaternion qU = Quaternion.Slerp(qA, qB, alpha);
                agent.ApplyKinematic(pU, qU);
            }
            else // PhysicsFromActions
            {
                // Use latest available actions (ZOH)
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
            int j = Mathf.Clamp(BinarySearchByTime(time), 0, frames.Count-1);
            idx = Mathf.Max(0, j - 1);

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

        // ---------- Anchoring ----------
        void ComputeAnchors()
        {
            enuAnchorOffset = Vector3.zero;
            worldAnchorAdd  = (anchor ? anchor.position : Vector3.zero);

            if (useAimPointAsOrigin && header?.scene?.threat_0?.aim_point != null &&
                header.scene.threat_0.aim_point.Length >= 3)
            {
                enuAnchorOffset = new Vector3(
                    header.scene.threat_0.aim_point[0],
                    header.scene.threat_0.aim_point[1],
                    header.scene.threat_0.aim_point[2]);
            }
            enuAnchorOffset += enuAdditionalOffset;
        }

        Vector3 MapEnuToUnityWithAnchor(Vector3 pENU)
        {
            // Subtract ENU anchor (aim_point etc.), then map to Unity, then add world anchor
            Vector3 shifted = pENU - enuAnchorOffset;
            return EnuUnity.ENUtoUnity(shifted) + worldAnchorAdd;
        }

        Pose GetUnityPose(AgentState s)
        {
            Vector3 pENU = new Vector3(s.p[0], s.p[1], s.p[2]);
            Vector3 pU   = MapEnuToUnityWithAnchor(pENU);
            Quaternion qU = EnuUnity.UnityRotationFromEnuWorldToBody(s.q[0], s.q[1], s.q[2], s.q[3]);
            return new Pose(pU, qU);
        }

        void CacheFirstPoses()
        {
            var first = frames.Count > 0 ? frames[0] : null;
            if (first == null || first.agents == null)
            {
                Debug.LogError("[ReplayDirector] No frames/agents.");
                return;
            }
            interceptPose0 = GetUnityPose(first.agents.interceptor_0);
            threatPose0    = GetUnityPose(first.agents.threat_0);
        }

        void SpawnAgentsFrozenAtFirstFrame()
        {
            var first = frames.Count > 0 ? frames[0] : null;
            if (first == null || first.agents == null)
            {
                Debug.LogError("[ReplayDirector] No frames/agents.");
                return;
            }

            freezeLeft = Mathf.Max(0, initialFreezeTicks);

            // Interceptor
            if (interceptorPrefab)
            {
                var go = Instantiate(interceptorPrefab, interceptPose0.position, interceptPose0.rotation);
                go.name = "Interceptor_Replay";
                TemporarilyDisableColliders(go, true);

                blue = go.GetComponent<AgentReplayer>() ?? go.AddComponent<AgentReplayer>();
                blue.agentId = "interceptor_0";
                blue.SetKinematic(true); // keep frozen initially

                TemporarilyDisableColliders(go, false);
            }

            // Threat
            if (threatPrefab)
            {
                var go = Instantiate(threatPrefab, threatPose0.position, threatPose0.rotation);
                go.name = "Threat_Replay";
                TemporarilyDisableColliders(go, true);

                red = go.GetComponent<AgentReplayer>() ?? go.AddComponent<AgentReplayer>();
                red.agentId = "threat_0";
                red.SetKinematic(true); // keep frozen initially

                TemporarilyDisableColliders(go, false);
            }
        }

        static void TemporarilyDisableColliders(GameObject go, bool disable)
        {
            var cols = go.GetComponentsInChildren<Collider>(true);
            foreach (var c in cols) c.enabled = !disable;
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
            return Path.Combine(Application.streamingAssetsPath, relOrAbs);
        }

        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(8, 8, 440, 140), GUI.skin.box);
            GUILayout.Label($"Episode: {epId}");
            GUILayout.Label($"t = {t:0.00}s   speed = {playSpeed:0.##}x   paused = {paused}");
            GUILayout.Label($"Frames: {frames.Count}   dt_nominal = {dtNominal:0.###}s");
            GUILayout.Label($"Modes: interceptor={interceptorMode}  threat={threatMode}");
            if (useAimPointAsOrigin)
                GUILayout.Label($"Anchored at aim_point ENU {enuAnchorOffset} → world add {worldAnchorAdd}");
            GUILayout.EndArea();
        }
    }
}
