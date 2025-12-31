using UnityEngine;

/// <summary>
/// Controlador de target con teclado y mouse
/// Útil para probar el nivel manualmente
/// </summary>
public class TargetController : MonoBehaviour
{
    [Header("Velocidad de Movimiento")]
    public float keyboardSpeed = 2f;
    public float mouseSpeed = 5f;
    
    [Header("Límites de Movimiento")]
    public bool useLimits = true;
    public Vector3 minPosition = new Vector3(-2, 0, -2);
    public Vector3 maxPosition = new Vector3(2, 3, 2);
    
    [Header("Modo de Control")]
    public bool useKeyboard = true;
    public bool useMouseDrag = false;
    
    [Header("Visualización")]
    public bool drawGizmos = true;
    
    private Camera mainCamera;
    private bool isDragging = false;
    private Vector3 offset;
    
    void Start()
    {
        mainCamera = Camera.main;
    }
    
    void Update()
    {
        if (useKeyboard)
        {
            HandleKeyboardInput();
        }
        
        if (useMouseDrag)
        {
            HandleMouseDrag();
        }
    }
    
    /// <summary>
    /// Control con teclado (WASD + Space/Shift)
    /// </summary>
    void HandleKeyboardInput()
    {
        float h = Input.GetAxis("Horizontal"); // A/D o flechas izq/der
        float v = Input.GetAxis("Vertical");   // W/S o flechas arriba/abajo
        float y = 0;
        
        // Subir/Bajar con Space y Shift
        if (Input.GetKey(KeyCode.Space)) y = 1;
        if (Input.GetKey(KeyCode.LeftShift)) y = -1;
        
        Vector3 movement = new Vector3(h, y, v) * keyboardSpeed * Time.deltaTime;
        transform.position += movement;
        
        // Aplicar límites
        if (useLimits)
        {
            transform.position = new Vector3(
                MathLite.Clamp(transform.position.x, minPosition.x, maxPosition.x),
                MathLite.Clamp(transform.position.y, minPosition.y, maxPosition.y),
                MathLite.Clamp(transform.position.z, minPosition.z, maxPosition.z)
            );
        }
    }
    
    /// <summary>
    /// Control arrastrando con el mouse (experimental)
    /// </summary>
    void HandleMouseDrag()
    {
        // Click izquierdo presionado
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            
            if (Physics.Raycast(ray, out hit))
            {
                if (hit.transform == transform)
                {
                    isDragging = true;
                    offset = transform.position - GetMouseWorldPosition();
                }
            }
        }
        
        // Arrastrando
        if (Input.GetMouseButton(0) && isDragging)
        {
            Vector3 newPos = GetMouseWorldPosition() + offset;
            
            if (useLimits)
            {
                newPos.x = MathLite.Clamp(newPos.x, minPosition.x, maxPosition.x);
                newPos.y = MathLite.Clamp(newPos.y, minPosition.y, maxPosition.y);
                newPos.z = MathLite.Clamp(newPos.z, minPosition.z, maxPosition.z);
            }
            
            transform.position = newPos;
        }
        
        // Soltar click
        if (Input.GetMouseButtonUp(0))
        {
            isDragging = false;
        }
    }
    
    /// <summary>
    /// Obtiene la posición del mouse en el mundo
    /// </summary>
    Vector3 GetMouseWorldPosition()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        
        // Proyectar en un plano a la altura actual del target
        Plane plane = new Plane(Vector3.up, transform.position.y);
        float distance;
        
        if (plane.Raycast(ray, out distance))
        {
            return ray.GetPoint(distance);
        }
        
        return transform.position;
    }
    
    void OnDrawGizmos()
    {
        if (!drawGizmos || !useLimits) return;
        
        // Dibujar límites del área de movimiento
        Gizmos.color = new Color(1, 1, 0, 0.3f);
        Vector3 center = (minPosition + maxPosition) * 0.5f;
        Vector3 size = maxPosition - minPosition;
        Gizmos.DrawWireCube(center, size);
    }
}
