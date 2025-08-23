using UnityEngine;

/// Applies RL actions (thrust 0..1, body-rate rad/s) to the existing 6-DoF stack.
/// When RL is active, GuidanceProNav is disabled; when inactive, it's re-enabled.
[RequireComponent(typeof(PIDAttitudeController))]
[RequireComponent(typeof(ThrustModel))]
public sealed class RLMuxController : MonoBehaviour
{
    [Header("Status")]
    public bool rlActive;

    [Header("Mapping / Limits")]
    [Tooltip("Optional scale on rate commands (rad/s). Use 0.5..2.0 to tune authority.")]
    public float rateGain = 1.0f;

    [Tooltip("Clamp magnitude of body rate command (rad/s).")]
    public float maxRateRad = 3.0f;

    [Tooltip("Minimum thrust to keep control effective (0..1).")]
    public float minThrustFloor = 0.0f;

    PIDAttitudeController pid;
    ThrustModel thrust;
    GuidanceProNav proNav;

    void Awake()
    {
        pid   = GetComponent<PIDAttitudeController>();
        thrust= GetComponent<ThrustModel>();
        proNav= GetComponent<GuidanceProNav>();
        if (!thrust) Debug.LogError("[RLMuxController] Missing ThrustModel.");
        if (!pid)    Debug.LogError("[RLMuxController] Missing PIDAttitudeController.");
    }

    /// NOTE: Server sends body rates as (pitch, yaw, roll) in rad/s.
    /// Unity body axes: X(right)=pitch, Y(up)=yaw, Z(forward)=roll.
    /// So mapping to Vector3(x,y,z) is direct: (pitch->x, yaw->y, roll->z).
    public void ApplyAction(float thrust01, Vector3 desiredBodyRate_PYR)
    {
        rlActive = true;

        if (proNav && proNav.enabled) proNav.enabled = false;

        // Throttle
        float tcmd = Mathf.Clamp01(thrust01);
        if (tcmd < minThrustFloor) tcmd = minThrustFloor;
        if (thrust) thrust.throttle01 = tcmd;

        // Body-rate command
        Vector3 cmd = desiredBodyRate_PYR * Mathf.Max(0f, rateGain);
        if (cmd.magnitude > maxRateRad) cmd = cmd.normalized * maxRateRad;

        // Feed PID to generate torques
        pid.ApplyRateCommand(cmd);
    }

    public void DeactivateRL()
    {
        rlActive = false;
        if (proNav && !proNav.enabled) proNav.enabled = true;
        if (thrust) thrust.throttle01 = 1f;
    }
}
