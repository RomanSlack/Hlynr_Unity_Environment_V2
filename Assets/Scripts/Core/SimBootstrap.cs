using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

[AddComponentMenu("Simulation/Sim Bootstrap")]
[DefaultExecutionOrder(-100)]
public sealed class SimBootstrap : MonoBehaviour
{
    [Tooltip("Seconds per physics tick")]
    [SerializeField] float fixedTimeStep = 0.01f;

    [Header("Optional: assign GameSimSettings asset here")]
    [SerializeField] GameSimSettings settings;

    bool paused;

    void Awake()
    {
        Time.fixedDeltaTime = fixedTimeStep;
        Application.runInBackground = true;

        // If not assigned in Inspector, try to find it in scene (ThreatSpawner) or Resources.
        if (settings == null)
        {
            var spawner = FindObjectOfType<ThreatSpawner>();
            if (spawner != null) settings = spawner.GetComponent<ThreatSpawner>()?.GetComponent<ThreatSpawner>() == null
                ? spawner.settings
                : spawner.settings;

            if (settings == null)
                settings = Resources.Load<GameSimSettings>("GameSimSettings"); // if you put it under Resources/
        }

        if (settings != null)
            Debug.Log($"[SimBootstrap] Threat mode at start: {settings.threatMode}");
        else
            Debug.LogWarning("[SimBootstrap] No GameSimSettings found. Assign it on this component or via ThreatSpawner.");
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        // Toggle Threat Mode with T
        if (kb.tKey.wasPressedThisFrame)
            ToggleThreatMode();

        // Toggle pause with Space
        if (kb.spaceKey.wasPressedThisFrame)
        {
            paused = !paused;
            Time.timeScale = paused ? 0f : 1f;
            Debug.Log($"[SimBootstrap] Paused: {paused}");
        }

        // Step one physics tick when paused
        if (paused && kb.rightArrowKey.wasPressedThisFrame)
            StartCoroutine(StepOnce());

        // Reset scene with R
        if (kb.rKey.wasPressedThisFrame)
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void ToggleThreatMode()
    {
        if (settings == null)
        {
            Debug.LogWarning("[SimBootstrap] Cannot toggle threat mode: no GameSimSettings assigned.");
            return;
        }

        settings.threatMode = (settings.threatMode == ThreatMode.LegacyStraight)
            ? ThreatMode.Rocket6DOF
            : ThreatMode.LegacyStraight;

        Debug.Log($"[SimBootstrap] Threat mode set to: {settings.threatMode}");
    }

    System.Collections.IEnumerator StepOnce()
    {
        Time.timeScale = 1f;
        yield return new WaitForFixedUpdate();
        yield return null; // let one render frame pass
        Time.timeScale = 0f;
    }
}
