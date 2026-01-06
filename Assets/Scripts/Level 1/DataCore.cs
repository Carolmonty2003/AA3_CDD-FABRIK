using UnityEngine;

/// <summary>
/// DataCore con feedback visual mientras espera (pulso/rotación),
/// y callbacks de estado para "Collect" y "Deposit".
/// </summary>
public class DataCore : MonoBehaviour
{
    [Header("Estado")]
    public bool isCollected = false;

    [Header("Configuración")]
    public string endEffectorTag = "EndEffector";

    [Header("Efectos Visuales")]
    public bool enablePulse = true;
    public bool enableRotation = true;
    public float pulseSpeed = 2f;
    public float pulseAmount = 0.2f;

    [Header("Colores")]
    public Color waitingColor = Color.cyan;
    public Color collectedColor = Color.green;
    public Color depositedColor = new Color(1f, 0.8f, 0f); // Amarillo dorado

    private Vector3 initialScale;
    private float pulseTimer = 0f;
    private Renderer coreRenderer;
    private Material coreMaterial;

    void Start()
    {
        initialScale = transform.localScale;

        // Cachea renderer/material para cambiar color y emisión.
        coreRenderer = GetComponent<Renderer>();
        if (coreRenderer != null)
        {
            coreMaterial = coreRenderer.material;
            coreMaterial.color = waitingColor;

            if (coreMaterial.HasProperty("_EmissionColor"))
            {
                coreMaterial.EnableKeyword("_EMISSION");
                coreMaterial.SetColor("_EmissionColor", waitingColor * 0.5f);
            }
        }
    }

    void Update()
    {
        if (isCollected) return;

        // Mientras está “libre”, hace pulso/rotación para feedback visual.
        if (enablePulse)
        {
            pulseTimer += Time.deltaTime * pulseSpeed;
            float pulse = 1f + Mathf.Sin(pulseTimer) * pulseAmount;
            transform.localScale = initialScale * pulse;
        }

        if (enableRotation)
        {
            transform.Rotate(Vector3.up, 30f * Time.deltaTime);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (isCollected) return;

        // Detección flexible del end-effector (tag, root tag, o nombre de joint).
        bool isEndEffector = other.CompareTag(endEffectorTag) ||
                            other.transform.root.CompareTag(endEffectorTag) ||
                            other.name.Contains("Joint_5") ||
                            other.name.Contains("Joint_6") ||
                            other.name.Contains("end");

        if (isEndEffector)
        {
            // Notifica al Level1Manager; el manager decide la secuencia real.
            Level1Manager manager = FindObjectOfType<Level1Manager>();
            if (manager != null)
            {
                manager.OnDataCoreCollected(this);
            }
        }
    }

    /// <summary>
    /// Marca el core como recogido y ajusta feedback visual.
    /// </summary>
    public void Collect()
    {
        if (isCollected) return;

        isCollected = true;

        Debug.Log($"[DataCore] {gameObject.name} siendo recogido");

        if (coreMaterial != null)
        {
            coreMaterial.color = collectedColor;

            if (coreMaterial.HasProperty("_EmissionColor"))
            {
                coreMaterial.SetColor("_EmissionColor", collectedColor * 0.5f);
            }
        }

        enablePulse = false;
        enableRotation = false;
        transform.localScale = initialScale;
    }

    /// <summary>
    /// Llamado al depositar: cambia color y dispara animación sutil.
    /// </summary>
    public void Deposit()
    {
        Debug.Log($"[DataCore] {gameObject.name} depositado");

        if (coreMaterial != null)
        {
            coreMaterial.color = depositedColor;

            if (coreMaterial.HasProperty("_EmissionColor"))
            {
                coreMaterial.SetColor("_EmissionColor", depositedColor * 0.7f);
            }
        }

        StartCoroutine(DepositAnimation());
    }

    /// <summary>
    /// Animación “squash & return” al depositar.
    /// </summary>
    System.Collections.IEnumerator DepositAnimation()
    {
        Vector3 startScale = transform.localScale;
        Vector3 squashScale = new Vector3(
            startScale.x * 1.2f,
            startScale.y * 0.8f,
            startScale.z * 1.2f
        );

        float t = 0;
        while (t < 1)
        {
            t += Time.deltaTime * 4f;
            transform.localScale = Vector3.Lerp(startScale, squashScale, t);
            yield return null;
        }

        t = 0;
        while (t < 1)
        {
            t += Time.deltaTime * 4f;
            transform.localScale = Vector3.Lerp(squashScale, initialScale, t);
            yield return null;
        }

        transform.localScale = initialScale;
    }

    /// <summary>
    /// Reseteo del core para reutilizarlo (estado + visual + parent).
    /// </summary>
    public void Reset()
    {
        isCollected = false;
        gameObject.SetActive(true);
        transform.localScale = initialScale;
        enablePulse = true;
        enableRotation = true;

        transform.SetParent(null);

        if (coreMaterial != null)
        {
            coreMaterial.color = waitingColor;

            if (coreMaterial.HasProperty("_EmissionColor"))
            {
                coreMaterial.SetColor("_EmissionColor", waitingColor * 0.5f);
            }
        }
    }
}
