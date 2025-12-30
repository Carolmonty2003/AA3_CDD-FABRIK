using UnityEngine;

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
    [Range(0f, 1f)] public float rotationStep = 1f; // 1 = giro completo, <1 = suaviza/limita

    [Header("Debug")]
    public int lastIterationsUsed;
    public float currentDistance;

    void LateUpdate()
    {
        if (target == null) return;
        if (joints == null || joints.Length < 2) return;

        lastIterationsUsed = SolveCCD_Unparented(joints, target.position, maxIterations, tolerance, rotationStep);

        Transform end = joints[joints.Length - 1];
        currentDistance = Vectors.Distance(end.position, target.position);
    }

    // CCD para joints NO parentados (cada Transform es independiente)
    private static int SolveCCD_Unparented(
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

            // Desde penúltimo hasta raíz (no rotamos el end como "articulación")
            for (int i = endIndex - 1; i >= 0; --i)
            {
                Transform joint = joints[i];
                if (joint == null) continue;

                Vector3 pivot = joint.position;

                Vector3 toEnd = end.position - pivot;
                Vector3 toTarget = targetPos - pivot;

                if (Vectors.SqrMagnitude(toEnd) < 1e-12f) continue;
                if (Vectors.SqrMagnitude(toTarget) < 1e-12f) continue;

                // Rotación necesaria para alinear joint->end con joint->target
                Quaternion delta = Quaternions.FromToRotation(toEnd, toTarget);

                // Limitar/suavizar el giro por paso (opcional)
                if (rotationStep < 0.999f)
                    delta = Lerp.SLerp(Quaternion.identity, delta, rotationStep);

                delta = Quaternions.Normalize(delta);

                // 1) Rotar el joint actual (en mundo)
                joint.rotation = Quaternions.Normalize(
                    Quaternions.Multiply(delta, joint.rotation)
                );

                // 2) SIMULAR JERARQUÍA:
                //    rotar posiciones + rotaciones de TODOS los joints posteriores alrededor del pivote
                for (int j = i + 1; j <= endIndex; ++j)
                {
                    Transform child = joints[j];
                    if (child == null) continue;

                    // Rotar posición alrededor del pivote
                    Vector3 r = child.position - pivot;
                    Vector3 rRot = Quaternions.Rotate3D(r, delta);
                    child.position = pivot + rRot;

                    // Rotar orientación (como si fuese hijo)
                    child.rotation = Quaternions.Normalize(
                        Quaternions.Multiply(delta, child.rotation)
                    );
                }
            }
        }

        return used;
    }
}