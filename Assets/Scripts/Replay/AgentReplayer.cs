using UnityEngine;

namespace Replay
{
    public enum ReplayMode { Kinematic, PhysicsFromActions }

    /// Wraps one agent GameObject and applies either kinematic states or recorded actions.
    [RequireComponent(typeof(Rigidbody))]
    public sealed class AgentReplayer : MonoBehaviour
    {
        [Header("Identity")]
        public string agentId = "interceptor_0";   // or "threat_0"

        [Header("Mode")]
        public ReplayMode mode = ReplayMode.Kinematic;

        [Header("Runtime (read-only)")]
        public bool ready;

        Rigidbody rb;
        RLMuxController mux;
        ThrustModel thrust;
        PIDAttitudeController pid;
        GuidanceProNav proNav;
        Missile6DOFController sixdof;

        void Awake()
        {
            rb     = GetComponent<Rigidbody>();
            mux    = GetComponent<RLMuxController>();
            thrust = GetComponent<ThrustModel>();
            pid    = GetComponent<PIDAttitudeController>();
            proNav = GetComponent<GuidanceProNav>();
            sixdof = GetComponent<Missile6DOFController>();
        }

        public void ConfigureForMode(ReplayMode m)
        {
            mode = m;
            if (mode == ReplayMode.Kinematic)
            {
                // Visual-only: disable active control/forces
                if (thrust) thrust.enabled = false;
                if (pid)    pid.enabled    = false;
                if (proNav) proNav.enabled = false;
                if (sixdof) sixdof.enabled = false;
                if (mux)    mux.enabled    = false;

                if (rb)
                {
                    rb.isKinematic = true;
                    rb.velocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
            }
            else // PhysicsFromActions
            {
                if (rb) rb.isKinematic = false;

                // Ensure control pipeline is active
                if (sixdof) sixdof.enabled = true;
                if (pid)    pid.enabled    = true;
                if (thrust) thrust.enabled = true;

                // Guidance must be OFF when RL drives rates
                if (proNav) proNav.enabled = false;

                if (!mux) mux = gameObject.AddComponent<RLMuxController>();
                mux.enabled = true;
                ready = true;
            }
        }

        // ----- Kinematic application -----
        public void ApplyKinematic(Vector3 posUnity, Quaternion rotUnity)
        {
            if (!rb) { transform.SetPositionAndRotation(posUnity, rotUnity); return; }
            rb.MovePosition(posUnity);
            rb.MoveRotation(rotUnity);
        }

        // Optional: set velocities (purely cosmetic here)
        public void SetVelocities(Vector3 velUnity, Vector3 angUnity)
        {
            if (rb && rb.isKinematic == false)
            {
                rb.velocity = velUnity;
                rb.angularVelocity = angUnity;
            }
        }

        // ----- Physics-from-actions -----
        // Action vector u: [ pitch(Y), yaw(Z), roll(X), thrust, aux, aux ]
        // We need (pitch_X, yaw_Y, roll_Z) for PID; mapping: x=u[2], y=u[0], z=u[1]
        public void ApplyActions(float[] u, float minThrustFloor = 0f)
        {
            if (u == null || u.Length < 4 || mux == null) return;

            float pitchY = u[0];
            float yawZ   = u[1];
            float rollX  = u[2];
            float thrust01 = Mathf.Clamp01(u[3]);

            var rateBody = new Vector3(rollX, pitchY, yawZ); // (x=rollX, y=pitchY, z=yawZ)
            mux.ApplyAction(thrust01, rateBody);
        }
    }
}
