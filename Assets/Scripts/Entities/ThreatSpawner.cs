using UnityEngine;

[AddComponentMenu("Simulation/Entities/Threat Spawner")]
public sealed class ThreatSpawner : MonoBehaviour
{
    [Header("Config")]
    public GameSimSettings settings;

    [Header("Prefabs")]
    public GameObject threatPrefabLegacy; // Capsule with ThreatGuidance (existing)
    public GameObject threatPrefabRocket; // NEW: rocket-style threat

    [Header("Spawn Line")]
    public Vector3 pointA = new Vector3(-100, 5, 150);
    public Vector3 pointB = new Vector3( 100, 5, 150);
    public float spawnInterval = 10f;

    float timer;

    public static Transform CurrentThreat { get; private set; }

    void FixedUpdate()
    {
        timer += Time.fixedDeltaTime;
        if (timer < spawnInterval) return;
        timer = 0f;

        Vector3 pos = Vector3.Lerp(pointA, pointB, Random.value);
        Quaternion rot = Quaternion.LookRotation((settings && settings.defendedTarget)
            ? (settings.defendedTarget.position - pos).normalized
            : Vector3.back);

        GameObject prefab = (settings && settings.threatMode == ThreatMode.Rocket6DOF)
            ? threatPrefabRocket
            : threatPrefabLegacy;

        var go = Instantiate(prefab, pos, rot);

        // Wire rocket threat at spawn
        var rocket = go.GetComponent<ThreatRocketController>();
        if (rocket && settings) rocket.ConfigureFrom(settings);

        CurrentThreat = go.transform;
    }
}
