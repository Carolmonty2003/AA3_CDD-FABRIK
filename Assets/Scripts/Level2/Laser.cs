using UnityEngine;

public class Laser : MonoBehaviour
{
    [Header("Auto-detect if empty")]
    [SerializeField] private Collider[] hitColliders;
    [SerializeField] private Renderer[] visuals;
    [SerializeField] private Behaviour[] extraBehaviours;

    [SerializeField] private bool activeOnStart = true;

    public bool IsActive { get; private set; }

    private void Awake()
    {
        if (hitColliders == null || hitColliders.Length == 0)
            hitColliders = GetComponentsInChildren<Collider>(includeInactive: true);

        if (visuals == null || visuals.Length == 0)
            visuals = GetComponentsInChildren<Renderer>(includeInactive: true);
    }

    private void Start()
    {
        SetActive(activeOnStart);
    }

    public void SetActive(bool active)
    {
        IsActive = active;

        if (hitColliders != null)
            for (int i = 0; i < hitColliders.Length; i++)
                if (hitColliders[i] != null) hitColliders[i].enabled = active;

        if (visuals != null)
            for (int i = 0; i < visuals.Length; i++)
                if (visuals[i] != null) visuals[i].enabled = active;

        if (extraBehaviours != null)
            for (int i = 0; i < extraBehaviours.Length; i++)
                if (extraBehaviours[i] != null) extraBehaviours[i].enabled = active;
    }
}
