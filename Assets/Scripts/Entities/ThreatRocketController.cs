using UnityEngine;

/// Drives a rocket-style attacker using the SAME physics stack as the interceptor.
/// On spawn, it is aimed at the defended target and uses guidance to pursue it.
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(FuelSystem))]
[RequireComponent(typeof(ThrustModel))]
[RequireComponent(typeof(Missile6DOFController))]
[RequireComponent(typeof(PIDAttitudeController))]
[RequireComponent(typeof(GuidanceProNav))]
[AddComponentMenu("Simulation/Entities/Threat Rocket Controller")]
public sealed class ThreatRocketController : MonoBehaviour
{
    [Tooltip("Target to attack (usually the defended asset)")]
    public Transform attackTarget;

    [Header("Launch")]
    public float launchImpulse = 15f;

    // Internal refs
    Rigidbody rb;
    FuelSystem fuel;
    ThrustModel thrust;
    Missile6DOFController sixdof;
    PIDAttitudeController pid;
    GuidanceProNav guidance;

    void Awake()
    {
        rb       = GetComponent<Rigidbody>();
        fuel     = GetComponent<FuelSystem>();
        thrust   = GetComponent<ThrustModel>();
        sixdof   = GetComponent<Missile6DOFController>();
        pid      = GetComponent<PIDAttitudeController>();
        guidance = GetComponent<GuidanceProNav>();
    }

    void Start()
    {
        if (attackTarget)
        {
            // Point roughly toward target at spawn
            Vector3 fwd = (attackTarget.position - transform.position).normalized;
            if (fwd.sqrMagnitude > 1e-6f)
                transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);

            // Use LOS pursuit
            guidance.target = attackTarget;
        }

        // Kick it out of the tube
        rb.AddForce(transform.forward * launchImpulse, ForceMode.Impulse);
    }

    /// Called by spawner to apply settings-driven parameters.
    public void ConfigureFrom(GameSimSettings s)
    {
        attackTarget = s.defendedTarget;
        launchImpulse = s.rocketLaunchImpulse;

        // Thrust curve + fuel settings
        if (s.rocketThrustCurve) thrust.GetType() // keep compiler happy
            ; // (assigned in Editor below; here we only ensure presence)

        var fuelSys = GetComponent<FuelSystem>();
        fuelSys.fuelKg = s.rocketFuelKg;
        fuelSys.massFlowKgPerSec = s.rocketMassFlowKgPerSec;

        // Max torque & PID/guidance aggressiveness
        GetComponent<Missile6DOFController>().maxTorque = s.rocketMaxTorque;
        GetComponent<GuidanceProNav>().timeToAlign = s.rocketTimeToAlign;
    }
}
