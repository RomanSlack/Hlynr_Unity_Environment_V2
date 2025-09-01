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
                    // Do NOT set rb.velocity/angVel on kinematic bodies (Unity warns).
                    rb.useGravity = false;
                }
                
                // Reset kinematic tracking state
                firstKinematicUpdate = true;
            }
            else // PhysicsFromActions
            {
                if (rb)
                {
                    rb.isKinematic = false;
                    rb.useGravity = true;
                }

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

        /// Make the body kinematic on/off and zero velocities when turning on.
        public void SetKinematic(bool kinematic)
        {
            if (!rb) return;
            rb.isKinematic = kinematic;
            if (kinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.useGravity = false;
            }
        }

        /// Force a kinematic pose write (safe even if rb was non-kinematic; restores prior state).
        public void ForceKinematicPose(Vector3 posUnity, Quaternion rotUnity)
        {
            if (rb)
            {
                bool prevKin = rb.isKinematic;
                rb.isKinematic = true;
                rb.MovePosition(posUnity);
                rb.MoveRotation(rotUnity);
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = prevKin;
            }
            else
            {
                transform.SetPositionAndRotation(posUnity, rotUnity);
            }
        }

        // ----- Kinematic application during playback -----
        Vector3 lastPosition = Vector3.zero;
        Quaternion lastRotation = Quaternion.identity;
        bool firstKinematicUpdate = true;

        public void ApplyKinematic(Vector3 posUnity, Quaternion rotUnity)
        {
            if (!rb)
            {
                transform.SetPositionAndRotation(posUnity, rotUnity);
                return;
            }

            // Calculate velocities from position/rotation changes for visual realism
            if (!firstKinematicUpdate)
            {
                Vector3 deltaPos = posUnity - lastPosition;
                rb.linearVelocity = deltaPos / Time.fixedDeltaTime;

                // Calculate angular velocity from rotation change
                Quaternion deltaRot = rotUnity * Quaternion.Inverse(lastRotation);
                deltaRot.ToAngleAxis(out float angle, out Vector3 axis);
                if (angle > 180f) angle -= 360f; // Handle wrap-around
                Vector3 angularVel = axis * (angle * Mathf.Deg2Rad / Time.fixedDeltaTime);
                rb.angularVelocity = angularVel;
            }
            else
            {
                firstKinematicUpdate = false;
            }

            lastPosition = posUnity;
            lastRotation = rotUnity;

            // MovePosition/MoveRotation are valid for kinematic bodies.
            rb.MovePosition(posUnity);
            rb.MoveRotation(rotUnity);
        }

        // ----- Physics-from-actions -----
        // Action vector u (from JSONL):
        //   [0] pitch about body Y (rad/s)
        //   [1] yaw   about body Z (rad/s)
        //   [2] roll  about body X (rad/s)
        //   [3] thrust in [0,1] (float)
        //   [4],[5] reserved
        //
        // Our control expects body rates as (x=roll, y=pitch, z=yaw).
        public void ApplyActions(float[] u, float minThrustFloor = 0f)
        {
            if (u == null || u.Length < 4 || mux == null) return;

            float pitchY   = u[0];
            float yawZ     = u[1];
            float rollX    = u[2];
            float thrust01 = Mathf.Max(minThrustFloor, Mathf.Clamp01(u[3]));

            var rateBody = new Vector3(rollX, pitchY, yawZ); // (x=roll, y=pitch, z=yaw)
            mux.ApplyAction(thrust01, rateBody);
        }
    }
}
