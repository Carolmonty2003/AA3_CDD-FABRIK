using UnityEngine;

public class Laser : MonoBehaviour
{
    [Header("Auto-detect if empty")]
    // Si están vacías, se auto-rellenan buscando en hijos.
    [SerializeField] private Collider[] hitColliders;
    [SerializeField] private Renderer[] visuals;

    [SerializeField] private bool activeOnStart = true;

    // Estado actual.
    public bool IsActive { get; private set; }

    // Si no se assignanron colliders o renderers en el inspector, se buscan automáticamente en los hijos.
    private void Awake()
    {
        if (hitColliders == null || hitColliders.Length == 0)
            hitColliders = GetComponentsInChildren<Collider>(includeInactive: true);

        if (visuals == null || visuals.Length == 0)
            visuals = GetComponentsInChildren<Renderer>(includeInactive: true);
    }

    private void Start()
    {
        // Aplica el estado inicial configurado.
        SetActive(activeOnStart);
    }

    public void SetActive(bool active)
    {
        IsActive = active;

        // Activa/desactiva colisiones i visuales.
        if (hitColliders != null)
            for (int i = 0; i < hitColliders.Length; i++)
                if (hitColliders[i] != null) hitColliders[i].enabled = active;

        if (visuals != null)
            for (int i = 0; i < visuals.Length; i++)
                if (visuals[i] != null) visuals[i].enabled = active;
    }
}
