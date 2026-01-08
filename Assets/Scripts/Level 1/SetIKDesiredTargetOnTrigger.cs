using UnityEngine;

public class SetIKDesiredTargetOnTrigger : MonoBehaviour
{
    public SmoothIKTargetProxy smoothProxy;
    public Transform newTarget;

    public float overrideSmoothTime = -1f;
    public string requiredTag = "Player";

    public bool clearOnExit = true; // <- nuevo

    void ApplyEnter()
    {
        if (smoothProxy == null || newTarget == null) return;

        if (overrideSmoothTime >= 0f)
            smoothProxy.smoothTime = overrideSmoothTime;

        smoothProxy.SetDesiredTarget(newTarget);
        smoothProxy.EnableIK(true);          
    }

    void ApplyExit()
    {
        if (smoothProxy == null) return;

        smoothProxy.SetDesiredTarget(null);
        smoothProxy.EnableIK(false);      
    }

    // 3D
    void OnTriggerEnter(Collider other)
    {
        if (!string.IsNullOrEmpty(requiredTag) && !other.CompareTag(requiredTag)) return;
        ApplyEnter();
    }

    void OnTriggerExit(Collider other)
    {
        if (!clearOnExit) return;
        if (!string.IsNullOrEmpty(requiredTag) && !other.CompareTag(requiredTag)) return;
        ApplyExit();
    }

    // 2D
    void OnTriggerEnter2D(Collider2D other)
    {
        if (!string.IsNullOrEmpty(requiredTag) && !other.CompareTag(requiredTag)) return;
        ApplyEnter();
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!clearOnExit) return;
        if (!string.IsNullOrEmpty(requiredTag) && !other.CompareTag(requiredTag)) return;
        ApplyExit();
    }
}
