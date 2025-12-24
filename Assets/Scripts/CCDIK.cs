using System;
using UnityEngine;

/// <summary>
/// CCD (Cyclic Coordinate Descent) IK Solver
/// Este algoritmo resuelve IK iterando desde el end-effector hacia la base,
/// rotando cada joint para que apunte al target.
/// 
/// VENTAJAS:
/// - Muy preciso en rotaciones
/// - Bueno para espacios confinados
/// - Natural para brazos robóticos
/// 
/// DESVENTAJAS:
/// - Puede tener movimientos poco naturales
/// - Más lento que FABRIK
/// </summary>
public class CCDIK : MonoBehaviour
{
    [Header("Configuración de la cadena")]
    [Tooltip("Array de joints en orden desde la base hasta el end-effector")]
    public Transform[] joints;
    
    [Tooltip("End-effector (último joint de la cadena)")]
    public Transform endEffector;
    
    [Header("Target")]
    [Tooltip("Objetivo a alcanzar")]
    public Transform target;
    
    [Header("Parámetros CCD")]
    [Tooltip("Número máximo de iteraciones por frame")]
    [Range(1, 50)]
    public int maxIterations = 10;
    
    [Tooltip("Distancia mínima para considerar que alcanzó el target")]
    [Range(0.001f, 1f)]
    public float tolerance = 0.01f;
    
    [Tooltip("Ángulo máximo de rotación por iteración (grados)")]
    [Range(1f, 180f)]
    public float maxAngleDelta = 30f;
    
    [Header("Activación")]
    public bool isActive = true;
    
    [Header("Debug Info (Read Only)")]
    public int lastIterationsUsed;
    public float lastDistanceToTarget;
    public string algorithmName = "CCD";
    
    [Header("Visualización")]
    public bool drawGizmos = true;
    public Color gizmoColor = Color.cyan;
    
    void LateUpdate()
    {
        if (!isActive || target == null || joints == null || joints.Length == 0)
            return;
        
        SolveCCD();
    }
    
    /// <summary>
    /// Resuelve la cinemática inversa usando CCD
    /// </summary>
    void SolveCCD()
    {
        if (endEffector == null)
            endEffector = joints[joints.Length - 1];
        
        lastIterationsUsed = 0;
        
        // Iteramos hasta alcanzar el target o el límite de iteraciones
        for (int iteration = 0; iteration < maxIterations; iteration++)
        {
            lastIterationsUsed++;
            
            // Calculamos distancia actual al target
            lastDistanceToTarget = Vectors.Distance(endEffector.position, target.position);
            
            // Si estamos suficientemente cerca, terminamos
            if (lastDistanceToTarget < tolerance)
                break;
            
            // Iteramos desde el penúltimo joint hacia la base
            // (El último es el end-effector y no rota)
            for (int i = joints.Length - 2; i >= 0; i--)
            {
                // Vector desde el joint actual al end-effector
                Vector3 toEndEffector = endEffector.position - joints[i].position;
                
                // Vector desde el joint actual al target
                Vector3 toTarget = target.position - joints[i].position;
                
                // Si alguno es casi cero, saltamos
                if (Vectors.SqrMagnitude(toEndEffector) < 1e-6f || 
                    Vectors.SqrMagnitude(toTarget) < 1e-6f)
                    continue;
                
                // Calculamos la rotación necesaria
                Quaternion rotationToTarget = CalculateRotation(toEndEffector, toTarget);
                
                // Limitamos el ángulo de rotación
                rotationToTarget = LimitRotation(rotationToTarget, maxAngleDelta);
                
                // Aplicamos la rotación al joint
                joints[i].rotation = Quaternions.Multiply(rotationToTarget, joints[i].rotation);
            }
        }
    }
    
    /// <summary>
    /// Calcula la rotación necesaria para ir de 'from' a 'to'
    /// </summary>
    Quaternion CalculateRotation(Vector3 from, Vector3 to)
    {
        from = Vectors.Normalize(from);
        to = Vectors.Normalize(to);
        
        // Producto punto para ver si son casi paralelos
        float dot = Vectors.DotProduct(from, to);
        
        // Si apuntan en la misma dirección, no rotamos
        if (dot > 0.999999f)
            return Quaternion.identity;
        
        // Si apuntan en direcciones opuestas, rotamos 180°
        if (dot < -0.999999f)
        {
            // Buscamos un eje perpendicular
            Vector3 axis = Vectors.CrossProduct(Vectors.Up(), from);
            if (Vectors.SqrMagnitude(axis) < 0.01f)
                axis = Vectors.CrossProduct(Vectors.Right(), from);
            
            axis = Vectors.Normalize(axis);
            return Quaternions.AxisAngle(axis, MathLite.PI);
        }
        
        // Rotación normal usando FromToRotation
        return Quaternions.FromToRotation(from, to);
    }
    
    /// <summary>
    /// Limita la rotación a un ángulo máximo
    /// </summary>
    Quaternion LimitRotation(Quaternion rotation, float maxAngle)
    {
        // Convertimos a eje-ángulo
        float angle;
        Vector3 axis;
        ToAxisAngle(rotation, out axis, out angle);
        
        // Si el ángulo es mayor que el máximo, lo limitamos
        if (angle > maxAngle * MathLite.Deg2Rad)
        {
            angle = maxAngle * MathLite.Deg2Rad;
            return Quaternions.AxisAngle(axis, angle);
        }
        
        return rotation;
    }
    
    /// <summary>
    /// Convierte un quaternion a representación eje-ángulo
    /// </summary>
    void ToAxisAngle(Quaternion q, out Vector3 axis, out float angle)
    {
        q = Quaternions.Normalize(q);
        
        angle = 2.0f * MathLite.Acos(q.w);
        float s = MathLite.Sqrt(1.0f - q.w * q.w);
        
        if (s < 0.001f)
        {
            axis = new Vector3(1, 0, 0);
        }
        else
        {
            axis = new Vector3(q.x / s, q.y / s, q.z / s);
        }
    }
    
    void OnDrawGizmos()
    {
        if (!drawGizmos || joints == null || joints.Length == 0)
            return;
        
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
        if (endEffector != null)
        {
            Gizmos.DrawWireSphere(endEffector.position, 0.08f);
        }
        
        // Target
        if (target != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(target.position, 0.1f);
            
            // Línea de distancia
            if (endEffector != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(endEffector.position, target.position);
            }
        }
    }
}
