using System;
using UnityEngine;

/// <summary>
/// FABRIK (Forward And Backward Reaching Inverse Kinematics) Solver
/// Este algoritmo resuelve IK alternando entre pasos "hacia adelante" y "hacia atrás",
/// ajustando las posiciones de los joints mientras mantiene las longitudes de los segmentos.
/// 
/// VENTAJAS:
/// - Muy rápido
/// - Movimientos naturales y suaves
/// - Bueno para navegación en espacios abiertos
/// 
/// DESVENTAJAS:
/// - Menos preciso en rotaciones específicas
/// - Puede "estirarse" demasiado
/// </summary>
public class FABRIKIK : MonoBehaviour
{
    [Header("Configuración de la cadena")]
    [Tooltip("Array de joints en orden desde la base hasta el end-effector")]
    public Transform[] joints;
    
    [Header("Target")]
    [Tooltip("Objetivo a alcanzar")]
    public Transform target;
    
    [Header("Parámetros FABRIK")]
    [Tooltip("Número máximo de iteraciones por frame")]
    [Range(1, 50)]
    public int maxIterations = 10;
    
    [Tooltip("Distancia mínima para considerar que alcanzó el target")]
    [Range(0.001f, 1f)]
    public float tolerance = 0.01f;
    
    [Tooltip("Si está fuera de alcance, acercar el target al radio máximo")]
    public bool clampToReachable = true;
    
    [Header("Activación")]
    public bool isActive = true;
    
    [Header("Debug Info (Read Only)")]
    public int lastIterationsUsed;
    public float lastDistanceToTarget;
    public string algorithmName = "FABRIK";
    public float totalReach;
    
    [Header("Visualización")]
    public bool drawGizmos = true;
    public Color gizmoColor = Color.green;
    
    // Arrays internos para el algoritmo
    private Vector3[] positions;
    private float[] lengths;
    private Vector3 basePosition;
    
    void Start()
    {
        InitializeFABRIK();
    }
    
    void InitializeFABRIK()
    {
        if (joints == null || joints.Length < 2)
        {
            Debug.LogError("FABRIK: Necesitas al menos 2 joints");
            return;
        }
        
        // Inicializamos arrays
        positions = new Vector3[joints.Length];
        lengths = new float[joints.Length - 1];
        totalReach = 0f;
        
        // Calculamos longitudes de cada segmento
        for (int i = 0; i < joints.Length - 1; i++)
        {
            float length = Vectors.Distance(joints[i].position, joints[i + 1].position);
            lengths[i] = length;
            totalReach += length;
        }
        
        // Guardamos posición de la base
        basePosition = joints[0].position;
    }
    
    void LateUpdate()
    {
        if (!isActive || target == null || joints == null || joints.Length == 0)
            return;
        
        if (positions == null || positions.Length != joints.Length)
            InitializeFABRIK();
        
        SolveFABRIK();
    }
    
    /// <summary>
    /// Resuelve la cinemática inversa usando FABRIK
    /// </summary>
    void SolveFABRIK()
    {
        // Copiamos posiciones actuales
        for (int i = 0; i < joints.Length; i++)
            positions[i] = joints[i].position;
        
        Vector3 targetPos = target.position;
        basePosition = joints[0].position;
        
        // Calculamos distancia de la base al target
        float distToTarget = Vectors.Distance(basePosition, targetPos);
        
        // Si el target está fuera de alcance
        if (distToTarget > totalReach)
        {
            if (clampToReachable)
            {
                // Acercamos el target al alcance máximo
                Vector3 direction = Vectors.Normalize(targetPos - basePosition);
                targetPos = basePosition + direction * totalReach;
            }
            else
            {
                // Estiramos la cadena hacia el target
                Vector3 direction = Vectors.Normalize(targetPos - basePosition);
                positions[0] = basePosition;
                for (int i = 0; i < lengths.Length; i++)
                {
                    positions[i + 1] = positions[i] + direction * lengths[i];
                }
                ApplyPositionsToJoints();
                lastDistanceToTarget = Vectors.Distance(positions[positions.Length - 1], target.position);
                return;
            }
        }
        
        lastIterationsUsed = 0;
        
        // Iteraciones FABRIK
        for (int iteration = 0; iteration < maxIterations; iteration++)
        {
            lastIterationsUsed++;
            
            // Calculamos distancia actual
            lastDistanceToTarget = Vectors.Distance(positions[positions.Length - 1], targetPos);
            
            // Si estamos suficientemente cerca, terminamos
            if (lastDistanceToTarget < tolerance)
                break;
            
            // PASO BACKWARD: Desde el end-effector hacia la base
            positions[positions.Length - 1] = targetPos;
            
            for (int i = positions.Length - 2; i >= 0; i--)
            {
                Vector3 direction = Vectors.Normalize(positions[i] - positions[i + 1]);
                positions[i] = positions[i + 1] + direction * lengths[i];
            }
            
            // PASO FORWARD: Desde la base hacia el end-effector
            positions[0] = basePosition;
            
            for (int i = 0; i < positions.Length - 1; i++)
            {
                Vector3 direction = Vectors.Normalize(positions[i + 1] - positions[i]);
                positions[i + 1] = positions[i] + direction * lengths[i];
            }
        }
        
        // Aplicamos las posiciones calculadas a los joints
        ApplyPositionsToJoints();
    }

    /// <summary>
    /// Aplica las posiciones calculadas a los transforms de los joints
    /// y ajusta sus rotaciones para que apunten al siguiente joint
    /// </summary>
    /// <summary>
    /// Aplica las posiciones calculadas a los transforms de los joints
    /// y ajusta sus rotaciones para que los CILINDROS HIJOS apunten al siguiente joint
    /// </summary>
    void ApplyPositionsToJoints()
    {
        for (int i = 0; i < joints.Length; i++)
        {
            joints[i].position = positions[i];
        }

        // Ajustamos rotaciones para que cada joint tenga su eje Y apuntando al siguiente
        // (Los cilindros de Unity tienen su eje largo en Y)
        for (int i = 0; i < joints.Length - 1; i++)
        {
            Vector3 direction = positions[i + 1] - positions[i];

            if (Vectors.SqrMagnitude(direction) > 1e-6f)
            {
                // Normalizar dirección
                direction = Vectors.Normalize(direction);

                // Construir rotación donde Y apunte en 'direction'
                // Primero calculamos right (perpendicular a direction)
                Vector3 right = Vectors.CrossProduct(Vectors.Up(), direction);

                // Si direction es casi vertical, usar otro vector de referencia
                if (Vectors.SqrMagnitude(right) < 1e-6f)
                {
                    right = Vectors.CrossProduct(Vectors.Forward(), direction);
                }

                right = Vectors.Normalize(right);

                // Forward es perpendicular a ambos
                Vector3 forward = Vectors.CrossProduct(direction, right);
                forward = Vectors.Normalize(forward);

                // Recalcular right para asegurar ortogonalidad perfecta
                right = Vectors.CrossProduct(direction, forward);

                // Crear rotación usando LookRotationCustom con forward como eje Z y direction como eje Y
                Quaternion rotation = Quaternions.LookRotationCustom(forward, direction);
                joints[i].rotation = rotation;
            }
        }

        // El último joint mantiene la rotación del penúltimo
        if (joints.Length > 1)
        {
            joints[joints.Length - 1].rotation = joints[joints.Length - 2].rotation;
        }
    }

    void OnDrawGizmos()
    {
        if (!drawGizmos || joints == null || joints.Length == 0)
            return;

        #if UNITY_EDITOR
            if (UnityEditor.Selection.activeGameObject != gameObject)
                return;
        #endif

        Gizmos.color = gizmoColor;
        
        // Dibujamos la cadena
        for (int i = 0; i < joints.Length - 1; i++)
        {
            if (joints[i] != null && joints[i + 1] != null)
            {
                Gizmos.DrawLine(joints[i].position, joints[i + 1].position);
                Gizmos.DrawWireSphere(joints[i].position, 0.05f);
            }
        }
        
        // End-effector
        if (joints.Length > 0 && joints[joints.Length - 1] != null)
        {
            Gizmos.DrawWireSphere(joints[joints.Length - 1].position, 0.08f);
        }
        
        // Target
        if (target != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(target.position, 0.1f);
            
            // Círculo de alcance
            if (Application.isPlaying && joints.Length > 0)
            {
                Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
                DrawCircle(joints[0].position, totalReach, 32);
            }
            
            // Línea de distancia
            if (joints.Length > 0)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(joints[joints.Length - 1].position, target.position);
            }
        }
    }
    
    void DrawCircle(Vector3 center, float radius, int segments)
    {
        Vector3 prevPoint = center + new Vector3(radius, 0, 0);
        
        for (int i = 1; i <= segments; i++)
        {
            float angle = (float)i / segments * 2f * MathLite.PI;
            Vector3 newPoint = center + new Vector3(
                MathLite.Cos(angle) * radius,
                0,
                MathLite.Sin(angle) * radius
            );
            
            Gizmos.DrawLine(prevPoint, newPoint);
            prevPoint = newPoint;
        }
    }
}
