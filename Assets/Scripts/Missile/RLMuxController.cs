using UnityEngine;

/// Applies RL actions (thrust 0..1, body-rate rad/s) to the existing 6-DoF stack.
/// When RL is active, GuidanceProNav is disabled; when inactive, it's re-enabled.
[RequireComponent(typeof(PIDAttitudeController))]
[RequireComponent(typeof(ThrustModel))]
public sealed class RLMuxController : MonoBehaviour
{
    public bool rlActive;

    PIDAttitudeController pid;
    ThrustModel thrust;
    GuidanceProNav proNav;

    void Awake()
    {
        pid   = GetComponent<PIDAttitudeController>();
        thrust= GetComponent<ThrustModel>();
        proNav= GetComponent<GuidanceProNav>();
    }

    public void ApplyAction(float thrust01, Vector3 desiredBodyRateRad)
    {
        rlActive = true;

        if (proNav && proNav.enabled) proNav.enabled = false;

        // Apply throttle scaling to the engine
        thrust.throttle01 = Mathf.Clamp01(thrust01);

        // Apply desired body rates through PID to convert to torques
        pid.ApplyRateCommand(desiredBodyRateRad);
    }

    public void DeactivateRL()
    {
        rlActive = false;
        if (proNav && !proNav.enabled) proNav.enabled = true;
        thrust.throttle01 = 1f;
    }
}
