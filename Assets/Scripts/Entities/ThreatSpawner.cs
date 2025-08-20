using UnityEngine;

[AddComponentMenu("Simulation/Entities/Threat Spawner")]
public sealed class ThreatSpawner : MonoBehaviour
{
    [Header("Config")]
    public GameSimSettings settings;

    [Header("Scene Target (assign your defended asset here)")]
    public Transform defendedTarget;   // Scene reference lives here

    [Header("Prefabs")]
    public GameObject threatPrefabLegacy; // Capsule with ThreatGuidance (legacy)
    public GameObject threatPrefabRocket; // Rocket-style threat (copied from Missile)

    [Header("Spawn Line")]
    public Vector3 pointA = new Vector3(-100, 5, 150);
    public Vector3 pointB = new Vector3( 100, 5, 150);
    public float spawnInterval = 10f;

    float timer;
    public static Transform CurrentThreat { get; private set; }

    void Awake()
    {
        // Auto-find defended target by tag if not assigned and a tag is provided
        if (defendedTarget == null && settings != null && !string.IsNullOrWhiteSpace(settings.defendedTargetTag))
        {
            var go = GameObject.FindWithTag(settings.defendedTargetTag);
            if (go) defendedTarget = go.transform;
        }
    }

    void FixedUpdate()
    {
        timer += Time.fixedDeltaTime;
        if (timer < spawnInterval) return;
        timer = 0f;

        Vector3 pos = Vector3.Lerp(pointA, pointB, Random.value);

        Vector3 aimVec = (defendedTarget ? defendedTarget.position : Vector3.zero) - pos;
        Vector3 forward = aimVec.sqrMagnitude > 1e-6f ? aimVec.normalized : Vector3.back;
        Quaternion rot = Quaternion.LookRotation(forward, Vector3.up);

        GameObject prefab = (settings && settings.threatMode == ThreatMode.Rocket6DOF)
            ? threatPrefabRocket
            : threatPrefabLegacy;

        var go = Instantiate(prefab, pos, rot);

        // Wire rocket-style threat (if this prefab uses it)
        var rocket = go.GetComponent<ThreatRocketController>();
        if (rocket)
        {
            if (settings) rocket.ConfigureFrom(settings);
            rocket.attackTarget = defendedTarget; // pass SCENE defended target
        }

        CurrentThreat = go.transform;
    }
}
