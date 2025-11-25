using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Replay
{
    /// Master controller: loads JSONL episode, spawns agents, and advances playback.
    [AddComponentMenu("Simulation/Replay/Replay Director")]
    public sealed class ReplayDirector : MonoBehaviour
    {
        [Header("File")]
        [Tooltip("If relative, resolved under StreamingAssets. Example: Replays/70p_fixed_timestamps.jsonl")]
        public string episodePath = "Replays/70p_fixed_timestamps.jsonl";

        [Header("Prefabs")]
        public GameObject interceptorPrefab;  // your Missile_Prefab
        public GameObject threatPrefab;       // your ThreatRocket prefab (or simple visual)

        [Header("Modes")]
        public ReplayMode interceptorMode = ReplayMode.PhysicsFromActions;
        public ReplayMode threatMode      = ReplayMode.Kinematic;

        [Header("Playback")]
        [Range(0.1f, 4f)] public float playSpeed = 1f;
        public bool autoPlay = false;  // Start paused by default

        [Header("Time/DT")]
        public bool matchHeaderFixedDelta = true;
        public float defaultDtNominal = 0.01f;

        [Header("Anchoring / Offsets")]
        [Tooltip("If set, the defended target will be placed at this Transform.position.")]
        public Transform anchor;                   // optional
        [Tooltip("If true, use first missile position as origin.")]
        public bool useFirstMissileAsOrigin = false;
        [Tooltip("Additional ENU offset to subtract.")]
        public Vector3 enuAdditionalOffset = Vector3.zero;

        [Header("Startup")]
        [Tooltip("Number of physics ticks to keep agents kinematic at t0 (prevents early collisions/drift).")]
        public int initialFreezeTicks = 2;
        [Tooltip("Show the controls overlay (HUD) on start")]
        public bool showOverlayOnStart = false;

        [Header("Cruise-In Animation")]
        [Tooltip("Enable cinematic cruise-in phase where threat approaches from far away before replay begins")]
        public bool enableCruiseIn = true;
        [Tooltip("Duration of the cruise-in phase in seconds")]
        [Range(1f, 10f)]
        public float cruiseInDuration = 4f;
        [Tooltip("How far back to extrapolate the threat's starting position (multiplier of cruise duration * velocity)")]
        [Range(1f, 3f)]
        public float cruiseDistanceMultiplier = 1.5f;
        [Tooltip("Speed multiplier for cruise-in playback (1 = realtime cruise speed)")]
        [Range(0.5f, 3f)]
        public float cruiseInPlaySpeed = 1f;

        // runtime
        bool paused;
        float t;                  // current replay time (seconds)
        float dtNominal = 0.01f;  // estimated from data
        string epId = "unknown";
        string outcome = "unknown";
        bool showOverlay;  // toggle for HUD visibility (initialized from showOverlayOnStart)

        // cruise-in state
        enum PlaybackPhase { CruiseIn, Replay }
        PlaybackPhase currentPhase = PlaybackPhase.Replay;
        float cruiseInTime = 0f;           // current time within cruise-in phase
        Vector3 threatCruiseStartENU;      // extrapolated far start position (ENU)
        Vector3 threatCruiseEndENU;        // replay start position (ENU)
        Vector3 threatVelocityENU;         // computed threat velocity (ENU units/sec)
        Quaternion threatCruiseRotation;   // rotation during cruise (based on velocity)

        // parsed data
        List<TimestepLine> frames = new List<TimestepLine>();
        HeaderLine header;
        FooterLine footer;

        // agents
        AgentReplayer blue;   // interceptor
        AgentReplayer red;    // missile

        // Track last rotation for velocity-based orientation
        Quaternion blueLastRotation = Quaternion.identity;
        Quaternion redLastRotation = Quaternion.identity;

        int idx;              // current frame index (frames[idx] <= t < frames[idx+1])

        // anchoring state
        Vector3 enuAnchorOffset = Vector3.zero; // subtract from ENU before mapping
        Vector3 worldAnchorAdd  = Vector3.zero; // add in Unity after mapping

        // freeze at start
        int   freezeLeft;
        Pose  interceptPose0, threatPose0;

        // camera reset and follow
        Vector3 initialCameraPosition;
        Quaternion initialCameraRotation;
        bool cameraInitialized = false;

        // camera follow modes
        enum CameraFollowMode { Free, FollowInterceptor, FollowThreat }
        CameraFollowMode followMode = CameraFollowMode.Free;
        Vector3 followOffset = new Vector3(0, 5, -10); // Default chase camera offset

        void Awake()
        {
            // Initialize overlay visibility from inspector setting
            showOverlay = showOverlayOnStart;

            LoadEpisode(ResolvePath(episodePath));

            if (frames.Count == 0)
            {
                Debug.LogError("[ReplayDirector] No frames loaded. Cannot initialize replay.");
                enabled = false;
                return;
            }

            ComputeAnchors();
            CacheFirstPoses();

            // Compute cruise-in trajectory if enabled
            if (enableCruiseIn)
            {
                ComputeCruiseInTrajectory();
                SpawnForCruiseIn();
                currentPhase = PlaybackPhase.CruiseIn;
                cruiseInTime = 0f;
            }
            else
            {
                SpawnAgentsFrozenAtFirstFrame();
                currentPhase = PlaybackPhase.Replay;
            }

            if (matchHeaderFixedDelta && dtNominal > 0f)
            {
                Time.fixedDeltaTime = dtNominal;
            }

            paused = !autoPlay;
            t = frames[0].t;
            idx = 0;

            // Store initial camera transform
            var mainCam = Camera.main;
            if (mainCam != null)
            {
                initialCameraPosition = mainCam.transform.position;
                initialCameraRotation = mainCam.transform.rotation;
                cameraInitialized = true;
            }

            var ip = frames[0].interceptor.p;
            var tp = frames[0].missile.p;
            Debug.Log($"[ReplayDirector] t0 ENU interceptor p=({ip[0]:0.###},{ip[1]:0.###},{ip[2]:0.###}) -> Unity {interceptPose0.position}");
            Debug.Log($"[ReplayDirector] t0 ENU missile     p=({tp[0]:0.###},{tp[1]:0.###},{tp[2]:0.###}) -> Unity {threatPose0.position}");
            if (enableCruiseIn)
            {
                Debug.Log($"[ReplayDirector] Cruise-in enabled: {cruiseInDuration}s, threat velocity={threatVelocityENU.magnitude:0.#} m/s");
            }
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                // Playback controls
                if (kb.spaceKey.wasPressedThisFrame) paused = !paused;

                // R - Restart replay only, Shift+R - Restart replay AND reset camera
                if (kb.rKey.wasPressedThisFrame)
                {
                    bool resetCamera = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
                    RestartReplay(resetCamera);
                }

                // Speed controls (more granular)
                if (kb.digit1Key.wasPressedThisFrame) playSpeed = 0.1f;  // Very slow
                if (kb.digit2Key.wasPressedThisFrame) playSpeed = 0.25f; // Slow
                if (kb.digit3Key.wasPressedThisFrame) playSpeed = 0.5f;  // Half speed
                if (kb.digit4Key.wasPressedThisFrame) playSpeed = 1f;    // Normal
                if (kb.digit5Key.wasPressedThisFrame) playSpeed = 2f;    // Double
                if (kb.digit6Key.wasPressedThisFrame) playSpeed = 4f;    // Fast

                // Fine speed adjustment
                if (kb.leftBracketKey.wasPressedThisFrame) playSpeed = Mathf.Max(0.1f, playSpeed - 0.25f);
                if (kb.rightBracketKey.wasPressedThisFrame) playSpeed = Mathf.Min(10f, playSpeed + 0.25f);

                // Frame stepping
                if (kb.rightArrowKey.wasPressedThisFrame && paused) StepOnce();
                if (kb.leftArrowKey.wasPressedThisFrame && paused) StepBackward();

                // Toggle overlay
                if (kb.hKey.wasPressedThisFrame) showOverlay = !showOverlay;

                // Camera follow controls
                if (kb.iKey.wasPressedThisFrame)
                {
                    // Toggle interceptor follow
                    if (followMode == CameraFollowMode.FollowInterceptor)
                        followMode = CameraFollowMode.Free;
                    else
                    {
                        followMode = CameraFollowMode.FollowInterceptor;
                        SnapCameraToTarget();
                    }
                }

                if (kb.mKey.wasPressedThisFrame)
                {
                    // Toggle threat/missile follow
                    if (followMode == CameraFollowMode.FollowThreat)
                        followMode = CameraFollowMode.Free;
                    else
                    {
                        followMode = CameraFollowMode.FollowThreat;
                        SnapCameraToTarget();
                    }
                }

                if (kb.fKey.wasPressedThisFrame)
                {
                    // Free camera (unlock)
                    followMode = CameraFollowMode.Free;
                }
            }
        }

        void StepBackward()
        {
            // Step back by one nominal frame
            t = Mathf.Max(frames.Count > 0 ? frames[0].t : 0f, t - dtNominal);
            SnapToTime(t, force:true);
        }

        void RestartReplay(bool resetCamera)
        {
            // If NOT resetting camera, disable follow mode to prevent camera from snapping to missile's t=0 position
            if (!resetCamera)
            {
                followMode = CameraFollowMode.Free;
            }

            // Reset velocity-based orientation tracking
            blueLastRotation = Quaternion.identity;
            redLastRotation = Quaternion.identity;

            // Handle cruise-in restart
            if (enableCruiseIn)
            {
                // Destroy existing agents
                if (blue != null) { Destroy(blue.gameObject); blue = null; }
                if (red != null) { Destroy(red.gameObject); red = null; }

                // Re-spawn for cruise-in
                SpawnForCruiseIn();
                currentPhase = PlaybackPhase.CruiseIn;
                cruiseInTime = 0f;
            }
            else
            {
                // Standard replay restart
                t = frames.Count > 0 ? frames[0].t : 0f;
                idx = 0;
                SnapToTime(t, force: true);
            }

            paused = !autoPlay;

            // Optionally reset camera position (Shift+R only)
            if (resetCamera && cameraInitialized)
            {
                var mainCam = Camera.main;
                if (mainCam != null)
                {
                    // Teleport camera
                    mainCam.transform.position = initialCameraPosition;
                    mainCam.transform.rotation = initialCameraRotation;

                    // Update controller internal state WITHOUT re-running Start()
                    var cameraFlyController = mainCam.GetComponent<CameraFlyController>();
                    if (cameraFlyController != null)
                    {
                        // Use reflection to update internal rotation state
                        var type = cameraFlyController.GetType();
                        var rotXField = type.GetField("rotationX", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        var rotYField = type.GetField("rotationY", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (rotXField != null && rotYField != null)
                        {
                            Vector3 initEuler = initialCameraRotation.eulerAngles;
                            float initRotX = initEuler.x > 180 ? initEuler.x - 360 : initEuler.x;
                            rotXField.SetValue(cameraFlyController, initRotX);
                            rotYField.SetValue(cameraFlyController, initEuler.y);
                        }
                    }

                    var flyCamera = mainCam.GetComponent<FlyCamera>();
                    if (flyCamera != null)
                    {
                        var type = flyCamera.GetType();
                        var rotXField = type.GetField("rotationX", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        var rotYField = type.GetField("rotationY", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (rotXField != null && rotYField != null)
                        {
                            Vector3 initEuler = initialCameraRotation.eulerAngles;
                            float initRotX = initEuler.x > 180 ? initEuler.x - 360 : initEuler.x;
                            rotXField.SetValue(flyCamera, initRotX);
                            rotYField.SetValue(flyCamera, initEuler.y);
                        }
                    }
                }
            }
        }

        void SnapCameraToTarget()
        {
            var mainCam = Camera.main;
            if (mainCam == null) return;

            Transform target = null;
            if (followMode == CameraFollowMode.FollowInterceptor && blue != null)
                target = blue.transform;
            else if (followMode == CameraFollowMode.FollowThreat && red != null)
                target = red.transform;

            if (target != null)
            {
                // Position camera behind and above target
                mainCam.transform.position = target.position + target.TransformDirection(followOffset);
                mainCam.transform.LookAt(target.position);

                // Update controller internal rotation to match
                UpdateCameraControllerRotation(mainCam);
            }
        }

        void UpdateCameraControllerRotation(Camera cam)
        {
            Vector3 euler = cam.transform.eulerAngles;
            float rotX = euler.x > 180 ? euler.x - 360 : euler.x;
            float rotY = euler.y;

            var cameraFlyController = cam.GetComponent<CameraFlyController>();
            if (cameraFlyController != null)
            {
                var type = cameraFlyController.GetType();
                var rotXField = type.GetField("rotationX", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var rotYField = type.GetField("rotationY", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (rotXField != null && rotYField != null)
                {
                    rotXField.SetValue(cameraFlyController, rotX);
                    rotYField.SetValue(cameraFlyController, rotY);
                }
            }

            var flyCamera = cam.GetComponent<FlyCamera>();
            if (flyCamera != null)
            {
                var type = flyCamera.GetType();
                var rotXField = type.GetField("rotationX", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var rotYField = type.GetField("rotationY", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (rotXField != null && rotYField != null)
                {
                    rotXField.SetValue(flyCamera, rotX);
                    rotYField.SetValue(flyCamera, rotY);
                }
            }
        }

        void LateUpdate()
        {
            // Handle camera follow (runs after physics/FixedUpdate)
            if (followMode != CameraFollowMode.Free)
            {
                var mainCam = Camera.main;
                if (mainCam != null)
                {
                    Transform target = null;
                    if (followMode == CameraFollowMode.FollowInterceptor && blue != null)
                        target = blue.transform;
                    else if (followMode == CameraFollowMode.FollowThreat && red != null)
                        target = red.transform;

                    if (target != null)
                    {
                        // Smoothly follow target
                        Vector3 desiredPos = target.position + target.TransformDirection(followOffset);
                        mainCam.transform.position = Vector3.Lerp(mainCam.transform.position, desiredPos, Time.unscaledDeltaTime * 5f);

                        // Look at target
                        Quaternion desiredRot = Quaternion.LookRotation(target.position - mainCam.transform.position);
                        mainCam.transform.rotation = Quaternion.Slerp(mainCam.transform.rotation, desiredRot, Time.unscaledDeltaTime * 5f);

                        // Keep controller state in sync
                        UpdateCameraControllerRotation(mainCam);
                    }
                }
            }
        }

        void FixedUpdate()
        {
            if (frames.Count < 2) return;

            // Handle cruise-in phase
            if (currentPhase == PlaybackPhase.CruiseIn)
            {
                UpdateCruiseIn();
                return;
            }

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

            if (blue != null && a.interceptor != null)
                ApplyAgent(blue, a.interceptor, b.interceptor, alpha);

            if (red != null && a.missile != null)
                ApplyAgent(red, a.missile, b.missile, alpha);
        }

        /// <summary>
        /// Update the cruise-in phase - moves threat from far position toward replay start
        /// </summary>
        void UpdateCruiseIn()
        {
            if (!paused)
            {
                cruiseInTime += Time.fixedDeltaTime * cruiseInPlaySpeed;
            }

            // Calculate interpolation factor (0 = start, 1 = end)
            float alpha = Mathf.Clamp01(cruiseInTime / cruiseInDuration);

            // Interpolate threat position in ENU space
            Vector3 currentPosENU = Vector3.Lerp(threatCruiseStartENU, threatCruiseEndENU, alpha);
            Vector3 currentPosUnity = MapEnuToUnityWithAnchor(currentPosENU);

            // Apply to threat
            if (red != null)
            {
                red.ForceKinematicPose(currentPosUnity, threatCruiseRotation);
            }

            // Check if cruise-in is complete
            if (cruiseInTime >= cruiseInDuration)
            {
                TransitionToReplay();
            }
        }

        void ApplyAgent(AgentReplayer agent, AgentState A, AgentState B, float alpha)
        {
            if (agent.mode == ReplayMode.Kinematic)
            {
                // Interpolate pose (with offsets)
                Vector3 pA = new Vector3(A.p[0], A.p[1], A.p[2]);
                Vector3 pB = (B != null && B.p != null) ? new Vector3(B.p[0], B.p[1], B.p[2]) : pA;

                Vector3 pENU = Vector3.Lerp(pA, pB, alpha);
                Vector3 pU   = MapEnuToUnityWithAnchor(pENU);

                // Calculate orientation from velocity vector (direction of travel)
                Quaternion qU = Quaternion.identity;
                Vector3 velocityDir = (pB - pA).normalized;

                // Only update rotation if there's significant movement
                if (velocityDir.sqrMagnitude > 0.001f)
                {
                    // Map velocity direction to Unity space
                    Vector3 velocityUnity = MapEnuToUnityWithAnchor(pA + velocityDir) - MapEnuToUnityWithAnchor(pA);
                    velocityUnity.Normalize();

                    // Make the object's forward axis (Z+) point along velocity
                    if (velocityUnity.sqrMagnitude > 0.001f)
                    {
                        qU = Quaternion.LookRotation(velocityUnity, Vector3.up);

                        // Store this rotation for next frame
                        if (agent == blue)
                            blueLastRotation = qU;
                        else if (agent == red)
                            redLastRotation = qU;
                    }
                    else
                    {
                        // Fallback to last known rotation
                        qU = (agent == blue) ? blueLastRotation : redLastRotation;
                    }
                }
                else
                {
                    // No movement, use last known rotation
                    qU = (agent == blue) ? blueLastRotation : redLastRotation;
                }

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

            if (blue != null && a.interceptor != null)
                ApplyAgent(blue, a.interceptor, b.interceptor, alpha);
            if (red != null && a.missile != null)
                ApplyAgent(red, a.missile, b.missile, alpha);
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

            if (useFirstMissileAsOrigin && frames.Count > 0 && frames[0].missile?.p != null &&
                frames[0].missile.p.Length >= 3)
            {
                enuAnchorOffset = new Vector3(
                    frames[0].missile.p[0],
                    frames[0].missile.p[1],
                    frames[0].missile.p[2]);
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
            if (first == null || first.interceptor == null || first.missile == null)
            {
                Debug.LogError("[ReplayDirector] No frames/agents.");
                return;
            }
            interceptPose0 = GetUnityPose(first.interceptor);
            threatPose0    = GetUnityPose(first.missile);
        }

        void SpawnAgentsFrozenAtFirstFrame()
        {
            var first = frames.Count > 0 ? frames[0] : null;
            if (first == null || first.interceptor == null || first.missile == null)
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
                blue.agentId = "interceptor";
                blue.SetKinematic(true); // keep frozen initially

                TemporarilyDisableColliders(go, false);

                // Notify PiPCanvasDisplay about the interceptor
                var pipDisplay = FindObjectOfType<PiPCanvasDisplay>();
                if (pipDisplay != null)
                    pipDisplay.AttachInterceptor(go);
            }

            // Threat/Missile
            if (threatPrefab)
            {
                var go = Instantiate(threatPrefab, threatPose0.position, threatPose0.rotation);
                go.name = "Missile_Replay";
                TemporarilyDisableColliders(go, true);

                red = go.GetComponent<AgentReplayer>() ?? go.AddComponent<AgentReplayer>();
                red.agentId = "missile";
                red.SetKinematic(true); // keep frozen initially

                TemporarilyDisableColliders(go, false);

                // Notify PiPCanvasDisplay about the threat
                var pipDisplay = FindObjectOfType<PiPCanvasDisplay>();
                if (pipDisplay != null)
                    pipDisplay.AttachThreat(go);
            }
        }

        /// <summary>
        /// Compute the threat's velocity from the first few frames and extrapolate a cruise-in start position
        /// </summary>
        void ComputeCruiseInTrajectory()
        {
            if (frames.Count < 2)
            {
                Debug.LogWarning("[ReplayDirector] Not enough frames to compute cruise-in trajectory");
                enableCruiseIn = false;
                return;
            }

            // Get threat positions from first few frames to compute average velocity
            int samplesToUse = Mathf.Min(10, frames.Count);
            Vector3 firstPos = new Vector3(frames[0].missile.p[0], frames[0].missile.p[1], frames[0].missile.p[2]);
            Vector3 lastSamplePos = new Vector3(frames[samplesToUse - 1].missile.p[0], frames[samplesToUse - 1].missile.p[1], frames[samplesToUse - 1].missile.p[2]);
            float timeDelta = frames[samplesToUse - 1].t - frames[0].t;

            if (timeDelta < 0.001f)
            {
                Debug.LogWarning("[ReplayDirector] Time delta too small to compute velocity");
                enableCruiseIn = false;
                return;
            }

            // Compute velocity in ENU space
            threatVelocityENU = (lastSamplePos - firstPos) / timeDelta;
            float speed = threatVelocityENU.magnitude;

            if (speed < 10f) // Less than 10 m/s is probably wrong
            {
                Debug.LogWarning($"[ReplayDirector] Computed threat velocity too low ({speed:0.#} m/s), disabling cruise-in");
                enableCruiseIn = false;
                return;
            }

            // The threat's replay start position
            threatCruiseEndENU = firstPos;

            // Extrapolate backward to get cruise start position
            // Distance = speed * duration * multiplier
            float cruiseDistance = speed * cruiseInDuration * cruiseDistanceMultiplier;
            Vector3 velocityDir = threatVelocityENU.normalized;
            threatCruiseStartENU = threatCruiseEndENU - velocityDir * cruiseDistance;

            // Compute the rotation for the threat during cruise (facing velocity direction)
            Vector3 velocityUnity = MapEnuToUnityWithAnchor(threatCruiseEndENU) - MapEnuToUnityWithAnchor(threatCruiseStartENU);
            if (velocityUnity.sqrMagnitude > 0.001f)
            {
                threatCruiseRotation = Quaternion.LookRotation(velocityUnity.normalized, Vector3.up);
            }
            else
            {
                threatCruiseRotation = Quaternion.identity;
            }

            Debug.Log($"[ReplayDirector] Cruise trajectory: start=({threatCruiseStartENU.x:0.#},{threatCruiseStartENU.y:0.#},{threatCruiseStartENU.z:0.#}) -> end=({threatCruiseEndENU.x:0.#},{threatCruiseEndENU.y:0.#},{threatCruiseEndENU.z:0.#}), distance={cruiseDistance:0.#}m");
        }

        /// <summary>
        /// Spawn only the threat at its cruise-in start position. Interceptor is spawned later when replay begins.
        /// </summary>
        void SpawnForCruiseIn()
        {
            // Spawn threat at extrapolated far position
            if (threatPrefab)
            {
                Vector3 startPosUnity = MapEnuToUnityWithAnchor(threatCruiseStartENU);
                var go = Instantiate(threatPrefab, startPosUnity, threatCruiseRotation);
                go.name = "Missile_Replay";
                TemporarilyDisableColliders(go, true);

                red = go.GetComponent<AgentReplayer>() ?? go.AddComponent<AgentReplayer>();
                red.agentId = "missile";
                red.SetKinematic(true);
                red.ConfigureForMode(ReplayMode.Kinematic); // Always kinematic during cruise

                TemporarilyDisableColliders(go, false);

                // Attach PiP camera to threat immediately
                var pipDisplay = FindObjectOfType<PiPCanvasDisplay>();
                if (pipDisplay != null)
                    pipDisplay.AttachThreat(go);

                Debug.Log($"[ReplayDirector] Spawned threat for cruise-in at {startPosUnity}");
            }

            // Interceptor is NOT spawned yet - will spawn when cruise-in ends
            blue = null;
        }

        /// <summary>
        /// Called when cruise-in phase ends to spawn the interceptor and transition to replay
        /// </summary>
        void TransitionToReplay()
        {
            currentPhase = PlaybackPhase.Replay;
            freezeLeft = Mathf.Max(0, initialFreezeTicks);

            // Snap threat to exact replay start position
            if (red != null)
            {
                red.ForceKinematicPose(threatPose0.position, threatPose0.rotation);
                red.ConfigureForMode(threatMode);
            }

            // Now spawn the interceptor
            if (interceptorPrefab)
            {
                var go = Instantiate(interceptorPrefab, interceptPose0.position, interceptPose0.rotation);
                go.name = "Interceptor_Replay";
                TemporarilyDisableColliders(go, true);

                blue = go.GetComponent<AgentReplayer>() ?? go.AddComponent<AgentReplayer>();
                blue.agentId = "interceptor";
                blue.SetKinematic(true);

                TemporarilyDisableColliders(go, false);

                // Attach PiP camera to interceptor
                var pipDisplay = FindObjectOfType<PiPCanvasDisplay>();
                if (pipDisplay != null)
                    pipDisplay.AttachInterceptor(go);

                Debug.Log($"[ReplayDirector] Cruise-in complete. Spawned interceptor, starting replay.");
            }

            // Reset replay time
            t = frames[0].t;
            idx = 0;
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
            footer = null;

            // Parse raw state lines
            var rawStates = new List<StateLine>();

            using (var sr = new StreamReader(path))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (line.Length == 0) continue;

                    if (line.Contains("\"type\""))
                    {
                        if (line.Contains("\"header\""))
                        {
                            header = JsonUtility.FromJson<HeaderLine>(line);
                            epId = header?.episode_id ?? "unknown";
                            continue;
                        }
                        else if (line.Contains("\"state\""))
                        {
                            var state = JsonUtility.FromJson<StateLine>(line);
                            if (state != null && state.state != null)
                            {
                                rawStates.Add(state);
                            }
                            continue;
                        }
                        else if (line.Contains("\"footer\""))
                        {
                            footer = JsonUtility.FromJson<FooterLine>(line);
                            outcome = footer?.outcome ?? "unknown";
                            break;
                        }
                    }
                }
            }

            // Group states by relative timestamp (already relative from JSONL)
            // Round to nearest millisecond to handle floating point differences
            var timestampGroups = rawStates
                .GroupBy(s => Mathf.Round(s.timestamp * 1000f) / 1000f) // Round to ms
                .OrderBy(g => g.Key);

            foreach (var group in timestampGroups)
            {
                var interceptorState = group.FirstOrDefault(s => s.entity_id == "interceptor");
                var missileState = group.FirstOrDefault(s => s.entity_id == "missile");

                if (interceptorState != null && missileState != null)
                {
                    var frame = new TimestepLine
                    {
                        t = group.Key, // Already relative time from JSONL
                        interceptor = ConvertToAgentState(interceptorState, true),
                        missile = ConvertToAgentState(missileState, false)
                    };
                    frames.Add(frame);
                }
            }

            // Estimate dt_nominal from frame differences
            if (frames.Count >= 2)
            {
                float sumDt = 0f;
                for (int i = 1; i < Mathf.Min(10, frames.Count); i++)
                {
                    sumDt += frames[i].t - frames[i - 1].t;
                }
                dtNominal = sumDt / Mathf.Min(9, frames.Count - 1);
            }
            else
            {
                dtNominal = defaultDtNominal;
            }

            if (frames.Count < 2)
                Debug.LogWarning($"[ReplayDirector] Episode loaded but only {frames.Count} timestep frames found.");

            Debug.Log($"[ReplayDirector] Loaded '{Path.GetFileName(path)}' ep_id={epId} frames={frames.Count} dt_nominal={dtNominal:0.###}s outcome={outcome}");
        }

        AgentState ConvertToAgentState(StateLine stateLine, bool isInterceptor)
        {
            var state = new AgentState
            {
                p = stateLine.state.position ?? new float[] { 0, 0, 0 },
                q = new float[] { 1, 0, 0, 0 }, // Default identity quaternion
                v = new float[] { 0, 0, 0 },    // Velocity not in new format
                w = new float[] { 0, 0, 0 },    // Angular velocity not in new format
                status = "active"
            };

            if (isInterceptor)
            {
                state.fuel_kg = stateLine.state.fuel;
                state.u = stateLine.state.action ?? new float[6];
            }
            else
            {
                state.fuel_kg = 0f;
                state.u = new float[6];
            }

            return state;
        }

        string ResolvePath(string relOrAbs)
        {
            if (Path.IsPathRooted(relOrAbs)) return relOrAbs;
            return Path.Combine(Application.streamingAssetsPath, relOrAbs);
        }

        void OnGUI()
        {
            if (!showOverlay)
            {
                // Show minimal hint when overlay is hidden
                GUILayout.BeginArea(new Rect(8, 8, 200, 30), GUI.skin.box);
                GUILayout.Label("Press H to show controls");
                GUILayout.EndArea();
                return;
            }

            // Episode info panel
            GUILayout.BeginArea(new Rect(8, 8, 520, 180), GUI.skin.box);
            GUILayout.Label($"<b>Episode: {epId}</b>  Outcome: <color={(outcome == "intercepted" ? "lime" : "red")}>{outcome}</color>", new GUIStyle(GUI.skin.label) { richText = true });

            // Show phase-appropriate time info
            if (currentPhase == PlaybackPhase.CruiseIn)
            {
                float cruiseProgress = Mathf.Clamp01(cruiseInTime / cruiseInDuration) * 100f;
                GUILayout.Label($"<color=yellow>[CRUISE-IN]</color> {cruiseInTime:0.0}s / {cruiseInDuration:0.0}s ({cruiseProgress:0}%)   {(paused ? "[PAUSED]" : "[INCOMING]")}", new GUIStyle(GUI.skin.label) { richText = true });
                GUILayout.Label($"<color=#888>Threat approaching... Interceptor will launch when in range.</color>", new GUIStyle(GUI.skin.label) { richText = true, fontSize = 10 });
            }
            else
            {
                GUILayout.Label($"Time: {t:0.00}s / {(frames.Count > 0 ? frames[frames.Count-1].t : 0):0.00}s   Speed: {playSpeed:0.##}x   {(paused ? "[PAUSED]" : "[PLAYING]")}");
            }

            // Camera mode indicator
            string camModeStr = followMode switch
            {
                CameraFollowMode.FollowInterceptor => "<color=cyan>[FOLLOW: INTERCEPTOR]</color>",
                CameraFollowMode.FollowThreat => "<color=orange>[FOLLOW: THREAT]</color>",
                _ => "[FREE CAMERA]"
            };
            GUILayout.Label($"Camera: {camModeStr}   Frames: {frames.Count}", new GUIStyle(GUI.skin.label) { richText = true });

            if (footer?.metrics != null)
                GUILayout.Label($"Fuel: {footer.metrics.fuel_used:0.1f}kg   Distance: {footer.metrics.final_distance:0.1f}m   Reward: {footer.metrics.total_reward:0.0f}");
            GUILayout.EndArea();

            // Controls guide panel
            GUILayout.BeginArea(new Rect(8, 198, 740, 220), GUI.skin.box);
            GUILayout.Label("<b>REPLAY CONTROLS</b>", new GUIStyle(GUI.skin.label) { richText = true, fontSize = 12 });
            GUILayout.Label("SPACE - Play/Pause      R - Restart      Shift+R - Reset Camera      Ctrl+R - Full Reload      H - Hide");
            GUILayout.Label("← → - Step frames (paused)      1-6 - Speed: 0.1x / 0.25x / 0.5x / 1x / 2x / 4x      [ ] - ±0.25x");
            GUILayout.Space(6);
            GUILayout.Label("<b>CAMERA MODES</b>", new GUIStyle(GUI.skin.label) { richText = true, fontSize = 11 });
            GUILayout.Label("I - Toggle Follow Interceptor      M - Toggle Follow Threat      F - Free Camera (unlock)");
            GUILayout.Space(6);
            GUILayout.Label("<b>FREE CAMERA CONTROLS</b> (God Mode - works even when paused!)", new GUIStyle(GUI.skin.label) { richText = true, fontSize = 11 });
            GUILayout.Label("WASD - Move      Mouse - Look      Q/E - Down/Up      Scroll - Speed      Shift - Fast      ESC - Cursor");
            GUILayout.EndArea();
        }
    }
}
