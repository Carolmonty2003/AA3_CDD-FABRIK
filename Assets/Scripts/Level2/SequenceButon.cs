using UnityEngine;

[RequireComponent(typeof(Collider))]
public class SequenceButton : MonoBehaviour
{
    [Header("Refs")]
    public Level2Manager manager;

    [Header("Order")]
    public int orderIndex = 0;

    [Header("Press Point (optional)")]
    public Transform pressPoint;

    [Header("Detection")]
    public LayerMask armLayerMask;
    public string endEffectorTag = "EndEffector";
    public float pressCooldown = 0.25f;

    [Header("DEBUG")]
    public bool debugLogs = true;

    bool _cooldown;

    void Awake()
    {
        var c = GetComponent<Collider>();
        c.isTrigger = true;

        if (debugLogs)
            Debug.Log($"[BTN][{name}] Awake() isTrigger={c.isTrigger}", this);
    }

    public Vector3 GetPressWorldPos()
    {
        return pressPoint != null ? pressPoint.position : transform.position;
    }

    void OnTriggerEnter(Collider other)
    {
        if (debugLogs)
        {
            string layerName = LayerMask.LayerToName(other.gameObject.layer);
            Debug.Log(
                $"[BTN][{name}] OnTriggerEnter by '{other.name}' tag='{other.tag}' layer='{layerName}' cooldown={_cooldown}",
                this
            );
        }

        if (_cooldown) return;

        bool inLayerMask = ((armLayerMask.value & (1 << other.gameObject.layer)) != 0);
        bool tagMatch = other.CompareTag(endEffectorTag);
        bool rootTagMatch = other.transform.root.CompareTag(endEffectorTag);

        bool isArm = inLayerMask || tagMatch || rootTagMatch;

        if (debugLogs)
            Debug.Log($"[BTN][{name}] isArm={isArm} (layer={inLayerMask}, tag={tagMatch}, rootTag={rootTagMatch})", this);

        if (!isArm) return;

        if (manager == null) manager = FindObjectOfType<Level2Manager>();
        if (manager == null) return;

        if (!manager.CanPressButton(this)) return;

        _cooldown = true;
        manager.OnButtonPressed(this);
        Invoke(nameof(ResetCooldown), pressCooldown);
    }

    void ResetCooldown()
    {
        _cooldown = false;
        if (debugLogs) Debug.Log($"[BTN][{name}] cooldown reset", this);
    }
}
