using System;
using UnityEngine;

/// <summary>
/// CCD (Cyclic Coordinate Descent) Solver
/// NUEVA VERSIÓN: Usa rotación directa como FABRIK, no rotación delta
/// </summary>
public class CCDIK : MonoBehaviour
{
    [Header("Configuración de la cadena")]
    [Tooltip("Array de joints en orden desde la base hasta el end-effector")]
    public Transform[] joints;

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

    [Tooltip("Suavizado de la rotación (0-1)")]
    [Range(0.01f, 1f)]
    public float rotationSpeed = 0.3f;

    [Header("Activación")]
    public bool isActive = true;

    [Header("Debug Info (Read Only)")]
    public int lastIterationsUsed;
    public float lastDistanceToTarget;
    public string algorithmName = "CCD";

    [Header("Visualización")]
    public bool drawGizmos = true;
    public Color gizmoColor = Color.cyan;

    // Cache del end-effector
    private Transform endEffector;
    private Quaternion[] targetRotations;

    void Start()
    {
        if (joints == null || joints.Length < 2)
        {
            Debug.LogError("CCD: Necesitas al menos 2 joints");
            enabled = false;
            return;
        }

        endEffector = joints[joints.Length - 1];
        targetRotations = new Quaternion[joints.Length];
    }

    void LateUpdate()
    {
        if (!isActive || target == null || joints == null || joints.Length == 0)
            return;

        if (endEffector == null)
            endEffector = joints[joints.Length - 1];

        SolveCCD();
    }

    /// <summary>
    /// Resuelve la cinemática inversa usando CCD
    /// </summary>
    void SolveCCD()
    {
        lastIterationsUsed = 0;

        // Iteraciones principales de CCD
        for (int iteration = 0; iteration < maxIterations; iteration++)
        {
            lastIterationsUsed++;

            // Calculamos distancia actual al target
            lastDistanceToTarget = Vectors.Distance(endEffector.position, target.position);

            // Si estamos suficientemente cerca, terminamos
            if (lastDistanceToTarget < tolerance)
                break;

            // CCD: Iteramos desde el penúltimo joint hacia la base
            for (int i = joints.Length - 2; i >= 0; i--)
            {
                // Vector desde este joint al end-effector
                Vector3 toEndEffector = endEffector.position - joints[i].position;

                // Vector desde este joint al target
                Vector3 toTarget = target.position - joints[i].position;

                float distToEnd = Vectors.Magnitude(toEndEffector);
                float distToTarget = Vectors.Magnitude(toTarget);

                // Si alguno es muy pequeño, saltamos
                if (distToEnd < 0.0001f || distToTarget < 0.0001f)
                    continue;

                // Normalizamos para obtener direcciones
                Vector3 dirToEnd = toEndEffector / distToEnd;
                Vector3 dirToTarget = toTarget / distToTarget;

                // Calculamos la rotación OBJETIVO (como FABRIK)
                // Esta es la rotación que haría que el joint apunte al target
                Quaternion targetRotation = Quaternions.LookRotationCustom(dirToTarget, Vectors.Up());

                // Aplicamos suavizado (lerp hacia la rotación objetivo)
                joints[i].rotation = Lerp.SLerp(joints[i].rotation, targetRotation, rotationSpeed);

                // Normalizamos para evitar errores
                joints[i].rotation = Quaternions.Normalize(joints[i].rotation);
            }
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