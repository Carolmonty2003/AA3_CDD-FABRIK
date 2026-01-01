using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Laser : MonoBehaviour
{
    [Header("Refs")]
    public Level2Manager manager;

    [Header("Detection")]
    public LayerMask armLayerMask;
    public string endEffectorTag = "EndEffector";

    [Header("Visual/Collision")]
    public Renderer[] renderersToToggle;
    public Collider triggerCollider;

    [Header("State (read only)")]
    public bool isOn = true;

    [Header("DEBUG")]
    public bool debugLogs = false;

    void Reset()
    {
        triggerCollider = GetComponent<Collider>();
        triggerCollider.isTrigger = true;
        renderersToToggle = GetComponentsInChildren<Renderer>(true);
    }

    void Awake()
    {
        if (triggerCollider == null) triggerCollider = GetComponent<Collider>();
        triggerCollider.isTrigger = true;

        if (renderersToToggle == null || renderersToToggle.Length == 0)
            renderersToToggle = GetComponentsInChildren<Renderer>(true);

        SetOn(isOn);
    }

    public void SetOn(bool on)
    {
        isOn = on;

        if (triggerCollider != null) triggerCollider.enabled = on;

        if (renderersToToggle != null)
        {
            for (int i = 0; i < renderersToToggle.Length; i++)
                if (renderersToToggle[i] != null) renderersToToggle[i].enabled = on;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!isOn) return;

        bool inLayerMask = ((armLayerMask.value & (1 << other.gameObject.layer)) != 0);
        bool tagMatch = other.CompareTag(endEffectorTag);
        bool rootTagMatch = other.transform.root.CompareTag(endEffectorTag);

        bool isArm = inLayerMask || tagMatch || rootTagMatch;

        if (debugLogs)
        {
            string layerName = LayerMask.LayerToName(other.gameObject.layer);
            Debug.Log($"[LASER][{name}] hit by '{other.name}' tag='{other.tag}' layer='{layerName}' isArm={isArm}", this);
        }

        if (!isArm) return;

        if (manager == null) manager = FindObjectOfType<Level2Manager>();
        if (manager != null) manager.OnLaserHit(this);
    }
}
