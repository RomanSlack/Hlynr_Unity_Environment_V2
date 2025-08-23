using UnityEngine;

[CreateAssetMenu(menuName = "Simulation/Config/Inference Config")]
public sealed class InferenceConfig : ScriptableObject
{
    [Header("Server")]
    [Tooltip("Base URL, e.g. http://127.0.0.1:5000")]
    public string baseUrl = "http://127.0.0.1:5000";

    [Tooltip("Health endpoint, relative to baseUrl")]
    public string healthPath = "/healthz";

    [Tooltip("Inference endpoint, relative to baseUrl")]
    public string inferencePath = "/v1/inference";

    [Header("Timing")]
    [Tooltip("Inference polls per second (1-5 is typical).")]
    [Range(0.5f, 10f)] public float pollHz = 2f;

    [Tooltip("Network timeout seconds")]
    [Range(0.1f, 10f)] public float timeoutSec = 2.0f;

    [Header("IDs / Naming")]
    public string interceptorId = "interceptor_0";
    public string threatId = "threat_0";

    [Header("Frames / Units")]
    [Tooltip("Send ENU (x=East,y=North,z=Up) to server. Internally converts from Unity coords.")]
    public bool sendENU = true;
}
