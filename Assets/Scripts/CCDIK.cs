using UnityEngine;

/// <summary>
/// CCDIK mejorado con constraints de distancia
/// Mantiene las longitudes de los segmentos fijas para evitar separación
/// </summary>
public class CCDIK : MonoBehaviour
{
    [Header("Chain (root -> ... -> end)")]
    [Tooltip("IMPORTANTE: ordena el array root -> ... -> endEffector (último).")]
    public Transform[] joints;

    [Header("Target")]
    public Transform target;

    [Header("CCD Params")]
    [Min(1)] public int maxIterations = 10;
    [Min(0f)] public float tolerance = 0.01f;
    [Range(0f, 1f)] public float rotationStep = 1f;

    [Header("Debug")]
    public int lastIterationsUsed;
    public float currentDistance;

    // Longitudes fijas entre joints
    private float[] segmentLengths;
    private bool initialized = false;

    void Start()
    {
        InitializeSegmentLengths();
    }

    void InitializeSegmentLengths()
    {
        if (joints == null || joints.Length < 2)
        {
            Debug.LogError("CCDIK: Necesitas al menos 2 joints");
            return;
        }

        segmentLengths = new float[joints.Length - 1];

        for (int i = 0; i < joints.Length - 1; i++)
        {
            segmentLengths[i] = Vectors.Distance(joints[i].position, joints[i + 1].position);
            Debug.Log($"Segmento {i}: {joints[i].name} -> {joints[i + 1].name} = {segmentLengths[i]:F3}m");
        }

        initialized = true;
        Debug.Log("CCDIK inicializado con longitudes fijas");
    }

    void LateUpdate()
    {
        if (target == null) return;
        if (joints == null || joints.Length < 2) return;

        if (!initialized)
        {
            InitializeSegmentLengths();
        }

        lastIterationsUsed = SolveCCD_WithConstraints(joints, target.position, maxIterations, tolerance, rotationStep);

        Transform end = joints[joints.Length - 1];
        currentDistance = Vectors.Distance(end.position, target.position);
    }

    private int SolveCCD_WithConstraints(
        Transform[] joints,
        Vector3 targetPos,
        int maxIterations,
        float tolerance,
        float rotationStep
    )
    {
        maxIterations = MathLite.ClampInt(maxIterations, 1, 1000);
        tolerance = MathLite.Max(0f, tolerance);
        rotationStep = MathLite.Clamp01(rotationStep);

        int endIndex = joints.Length - 1;
        Transform end = joints[endIndex];

        int used = 0;

        for (int it = 0; it < maxIterations; ++it)
        {
            used = it + 1;

            float err = Vectors.Distance(end.position, targetPos);
            if (err <= tolerance) break;

            // CCD: Desde penúltimo hasta raíz
            for (int i = endIndex - 1; i >= 0; --i)
            {
                Transform joint = joints[i];
                if (joint == null) continue;

                Vector3 pivot = joint.position;
                Vector3 toEnd = end.position - pivot;
                Vector3 toTarget = targetPos - pivot;

                if (Vectors.SqrMagnitude(toEnd) < 1e-12f) continue;
                if (Vectors.SqrMagnitude(toTarget) < 1e-12f) continue;

                // Rotación necesaria
                Quaternion delta = Quaternions.FromToRotation(toEnd, toTarget);

                // Limitar rotación
                if (rotationStep < 0.999f)
                    delta = Lerp.SLerp(Quaternion.identity, delta, rotationStep);

                delta = Quaternions.Normalize(delta);

                // Rotar el joint
                joint.rotation = Quaternions.Normalize(
                    Quaternions.Multiply(delta, joint.rotation)
                );

                // Rotar todos los joints posteriores
                for (int j = i + 1; j <= endIndex; ++j)
                {
                    Transform child = joints[j];
                    if (child == null) continue;

                    // Rotar posición
                    Vector3 r = child.position - pivot;
                    Vector3 rRot = Quaternions.Rotate3D(r, delta);
                    child.position = pivot + rRot;

                    // Rotar orientación
                    child.rotation = Quaternions.Normalize(
                        Quaternions.Multiply(delta, child.rotation)
                    );
                }

                // NUEVO: Aplicar constraints de distancia
                ApplyDistanceConstraints();
            }
        }

        return used;
    }

    /// <summary>
    /// Fuerza que las distancias entre joints se mantengan fijas
    /// Esto previene la separación
    /// </summary>
    void ApplyDistanceConstraints()
    {
        if (segmentLengths == null || segmentLengths.Length == 0)
            return;

        // Desde la raíz hacia el end-effector
        for (int i = 0; i < joints.Length - 1; i++)
        {
            Transform current = joints[i];
            Transform next = joints[i + 1];

            if (current == null || next == null) continue;

            Vector3 direction = next.position - current.position;
            float currentLength = Vectors.Magnitude(direction);

            // Si la distancia cambió, corregirla
            if (currentLength > 0.0001f)
            {
                float targetLength = segmentLengths[i];
                direction = Vectors.Normalize(direction);

                // Forzar la distancia correcta
                next.position = current.position + direction * targetLength;
            }
        }
    }
}