using UnityEngine;

/// <summary>
/// Script helper para manejar targets, botones, y lógica de juego
/// Útil para los 3 niveles del proyecto
/// </summary>
public class GameLevelManager : MonoBehaviour
{
    [Header("Referencias")]
    public Transform targetObject;
    
    [Header("Movimiento del Target (Nivel 3)")]
    public bool moveTarget = false;
    public Vector3 oscillationAmplitude = new Vector3(1f, 0f, 0f);
    public float oscillationSpeed = 1f;
    
    private Vector3 initialPosition;
    
    void Start()
    {
        if (targetObject != null)
            initialPosition = targetObject.position;
    }
    
    void Update()
    {
        if (moveTarget && targetObject != null)
        {
            OscillateTarget();
        }
    }
    
    /// <summary>
    /// Hace que el target oscile (para el nivel 3)
    /// </summary>
    void OscillateTarget()
    {
        float time = Time.time * oscillationSpeed;
        
        Vector3 offset = new Vector3(
            oscillationAmplitude.x * MathLite.Sin(time),
            oscillationAmplitude.y * MathLite.Sin(time * 1.3f), // Diferentes frecuencias
            oscillationAmplitude.z * MathLite.Sin(time * 0.7f)
        );
        
        targetObject.position = initialPosition + offset;
    }
    
    /// <summary>
    /// Mueve el target a una posición específica (para niveles 1 y 2)
    /// </summary>
    public void MoveTargetTo(Vector3 newPosition)
    {
        if (targetObject != null)
        {
            targetObject.position = newPosition;
            initialPosition = newPosition;
        }
    }
    
    /// <summary>
    /// Activa/desactiva el movimiento del target
    /// </summary>
    public void SetTargetMovement(bool active)
    {
        moveTarget = active;
    }
}

/// <summary>
/// Script para botones que necesitan ser presionados
/// </summary>
public class PressableButton : MonoBehaviour
{
    [Header("Configuración")]
    public string triggerTag = "EndEffector";
    public Color normalColor = Color.red;
    public Color pressedColor = Color.green;
    public float pressDistance = 0.1f;
    
    [Header("Estado")]
    public bool isPressed = false;
    
    private Renderer buttonRenderer;
    private Vector3 initialPosition;
    private Material buttonMaterial;
    
    void Start()
    {
        buttonRenderer = GetComponent<Renderer>();
        initialPosition = transform.position;
        
        if (buttonRenderer != null)
        {
            buttonMaterial = buttonRenderer.material;
            buttonMaterial.color = normalColor;
        }
    }
    
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(triggerTag) || other.transform.root.CompareTag(triggerTag))
        {
            Press();
        }
    }
    
    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(triggerTag) || other.transform.root.CompareTag(triggerTag))
        {
            Release();
        }
    }
    
    void Press()
    {
        isPressed = true;
        
        if (buttonMaterial != null)
            buttonMaterial.color = pressedColor;
        
        // Animación visual de presionar
        transform.position = initialPosition - transform.up * pressDistance;
        
        Debug.Log($"Botón {gameObject.name} presionado!");
    }
    
    void Release()
    {
        isPressed = false;
        
        if (buttonMaterial != null)
            buttonMaterial.color = normalColor;
        
        transform.position = initialPosition;
    }
}

/// <summary>
/// Detector de colisión con láseres (para el nivel 2)
/// </summary>
public class LaserCollisionDetector : MonoBehaviour
{
    [Header("Configuración")]
    public string armTag = "RobotArm";
    public int maxCollisions = 3;
    
    [Header("Estado")]
    public int currentCollisions = 0;
    public bool failed = false;
    
    [Header("Feedback")]
    public Color laserNormalColor = Color.red;
    public Color laserHitColor = Color.yellow;
    
    void OnTriggerEnter(Collider other)
    {
        if (failed) return;
        
        if (other.CompareTag(armTag) || other.transform.root.CompareTag(armTag))
        {
            currentCollisions++;
            Debug.LogWarning($"Colisión con láser! ({currentCollisions}/{maxCollisions})");
            
            // Cambiar color temporalmente
            StartCoroutine(FlashLaser());
            
            if (currentCollisions >= maxCollisions)
            {
                Failed();
            }
        }
    }
    
    System.Collections.IEnumerator FlashLaser()
    {
        Renderer rend = GetComponent<Renderer>();
        if (rend != null)
        {
            Color original = rend.material.color;
            rend.material.color = laserHitColor;
            yield return new WaitForSeconds(0.2f);
            rend.material.color = original;
        }
    }
    
    void Failed()
    {
        failed = true;
        Debug.LogError("¡Demasiadas colisiones! Nivel fallido.");
        // Aquí puedes añadir lógica de reinicio de nivel
    }
    
    public void ResetCollisions()
    {
        currentCollisions = 0;
        failed = false;
    }
}
