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
    public Collider[] collidersToToggle;

    [Header("State (read only)")]
    public bool isOn = true;

    [Header("DEBUG")]
    public bool debugLogs = false;

    void Reset()
    {
        renderersToToggle = GetComponentsInChildren<Renderer>(true);
        collidersToToggle = GetComponentsInChildren<Collider>(true);
    }

    void Awake()
    {
        if (renderersToToggle == null || renderersToToggle.Length == 0)
            renderersToToggle = GetComponentsInChildren<Renderer>(true);

        if (collidersToToggle == null || collidersToToggle.Length == 0)
            collidersToToggle = GetComponentsInChildren<Collider>(true);

        // Asegura triggers (láser = detector, no pared sólida)
        if (collidersToToggle != null)
        {
            for (int i = 0; i < collidersToToggle.Length; i++)
                if (collidersToToggle[i] != null) collidersToToggle[i].isTrigger = true;
        }

        SetOn(isOn);
    }

    public void SetOn(bool on)
    {
        isOn = on;

        if (collidersToToggle != null)
        {
            for (int i = 0; i < collidersToToggle.Length; i++)
                if (collidersToToggle[i] != null) collidersToToggle[i].enabled = on;
        }

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
