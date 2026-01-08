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

    [Header("Debug (solo lectura)")]
    public int LastIterations { get; private set; }          
    public float LastDistanceToTarget { get; private set; }


    [HideInInspector]
    public float totalReach;

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
        // Reset debug del frame
        LastIterations = 0;
        LastDistanceToTarget = 0f;

        // Copiamos posiciones actuales
        for (int i = 0; i < joints.Length; i++)
            positions[i] = joints[i].position;

        Vector3 targetPos = target.position;
        basePosition = joints[0].position;

        float distToTarget = Vectors.Distance(basePosition, targetPos);

        if (distToTarget > totalReach)
        {
            if (clampToReachable)
            {
                Vector3 direction = Vectors.Normalize(targetPos - basePosition);
                targetPos = basePosition + direction * totalReach;
            }
            else
            {
                Vector3 direction = Vectors.Normalize(targetPos - basePosition);
                positions[0] = basePosition;

                for (int i = 0; i < lengths.Length; i++)
                    positions[i + 1] = positions[i] + direction * lengths[i];

                ApplyPositionsToJoints();

                // Debug
                LastDistanceToTarget = Vectors.Distance(positions[positions.Length - 1], targetPos);
                return;
            }
        }

        for (int iteration = 0; iteration < maxIterations; iteration++)
        {
            float distanceToTarget = Vectors.Distance(positions[positions.Length - 1], targetPos);

            if (distanceToTarget < tolerance)
                break;

            // (vamos a ejecutar esta iteración)
            LastIterations = iteration + 1;

            // BACKWARD
            positions[positions.Length - 1] = targetPos;
            for (int i = positions.Length - 2; i >= 0; i--)
            {
                Vector3 direction = Vectors.Normalize(positions[i] - positions[i + 1]);
                positions[i] = positions[i + 1] + direction * lengths[i];
            }

            // FORWARD
            positions[0] = basePosition;
            for (int i = 0; i < positions.Length - 1; i++)
            {
                Vector3 direction = Vectors.Normalize(positions[i + 1] - positions[i]);
                positions[i + 1] = positions[i] + direction * lengths[i];
            }
        }

        ApplyPositionsToJoints();

        // Debug final (error tras aplicar)
        LastDistanceToTarget = Vectors.Distance(positions[positions.Length - 1], targetPos);
    }

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
}