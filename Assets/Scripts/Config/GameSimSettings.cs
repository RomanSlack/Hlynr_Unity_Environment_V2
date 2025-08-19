using UnityEngine;

public enum ThreatMode { LegacyStraight, Rocket6DOF }

[CreateAssetMenu(menuName = "Simulation/Config/Game Sim Settings")]
public sealed class GameSimSettings : ScriptableObject
{
    [Header("Threat Mode Toggle")]
    public ThreatMode threatMode = ThreatMode.LegacyStraight;

    [Header("Targets")]
    public Transform defendedTarget;   // the asset/base the attacker aims for

    [Header("Rocket Threat Defaults")]
    public float rocketLaunchImpulse = 15f;
    public ThrustCurve rocketThrustCurve;  // assign a reasonable curve
    public float rocketTimeToAlign = 0.4f;
    public Vector3 rocketMaxTorque = new Vector3(8000, 8000, 2000);
    public float rocketFuelKg = 4f;
    public float rocketMassFlowKgPerSec = 0.8f;
}
