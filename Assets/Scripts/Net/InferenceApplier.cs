using UnityEngine;

[AddComponentMenu("Simulation/Net/Inference Applier")]
public sealed class InferenceApplier : MonoBehaviour
{
    public InferenceClient client;

    void Awake()
    {
        if (!client) client = FindObjectOfType<InferenceClient>();
    }

    void FixedUpdate()
    {
        if (!client || !client.sessionActive) return;
        if (!client.currentInterceptor) return;

        var mux = client.currentInterceptor.GetComponent<RLMuxController>();
        if (!mux) return;

        // Pull latest action from client and apply
        mux.ApplyAction(client.GetThrust01(), client.GetDesiredBodyRates());
    }

    void OnDisable()
    {
        if (client && client.currentInterceptor)
        {
            var mux = client.currentInterceptor.GetComponent<RLMuxController>();
            if (mux) mux.DeactivateRL();
        }
    }
}
