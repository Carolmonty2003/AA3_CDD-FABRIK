using UnityEngine;

public class level3_FABRIKIK : MonoBehaviour
{
    [Header("Configuración de la cadena")]
    public Transform[] joints;

    [Header("Visuals (optional, NO children)")]
    public Transform[] segmentVisuals;
    public float segmentMeshHeight = 2f;

    [Header("Target")]
    public Transform target;

    [Header("Parámetros FABRIK")]
    [Range(1, 50)] public int maxIterations = 10;
    [Range(0.001f, 1f)] public float tolerance = 0.01f;
    public bool clampToReachable = true;

    [Header("Activación")]
    public bool isActive = true;

    [Header("Debug Info (Read Only)")]
    public string algorithmName = "FABRIK";
    public int lastIterationsUsed;
    public float lastDistanceToTarget;
    public float totalReach;

    [Header("Visualización")]
    public bool drawGizmos = true;
    public Color gizmoColor = Color.green;

    Vector3[] positions;
    float[] lengths;
    Vector3 basePosition;

    void Start()
    {
        ValidateNoJointHierarchyChain();
        InitializeFABRIK();
        UpdateSegmentVisuals();
    }

    void InitializeFABRIK()
    {
        if (joints == null || joints.Length < 2)
        {
            Debug.LogError("level3_FABRIKIK: Necesitas al menos 2 joints");
            return;
        }

        positions = new Vector3[joints.Length];
        lengths = new float[joints.Length - 1];
        totalReach = 0f;

        for (int i = 0; i < joints.Length - 1; i++)
        {
            float length = Vectors.Distance(joints[i].position, joints[i + 1].position);
            lengths[i] = length;
            totalReach += length;
        }

        basePosition = joints[0].position;
    }

    void LateUpdate()
    {
        if (!isActive || target == null || joints == null || joints.Length < 2) return;
        if (positions == null || positions.Length != joints.Length) InitializeFABRIK();

        SolveFABRIK();
        UpdateSegmentVisuals();
    }

    void SolveFABRIK()
    {
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
                lastDistanceToTarget = Vectors.Distance(positions[positions.Length - 1], target.position);
                return;
            }
        }

        lastIterationsUsed = 0;

        for (int iteration = 0; iteration < maxIterations; iteration++)
        {
            lastIterationsUsed++;

            lastDistanceToTarget = Vectors.Distance(positions[positions.Length - 1], targetPos);
            if (lastDistanceToTarget < tolerance) break;

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
    }

    void ApplyPositionsToJoints()
    {
        for (int i = 0; i < joints.Length; i++)
            joints[i].position = positions[i];

        // (Opcional) rotación del joint: no depende de hijos
        for (int i = 0; i < joints.Length - 1; i++)
        {
            Vector3 direction = positions[i + 1] - positions[i];
            if (Vectors.SqrMagnitude(direction) > 1e-6f)
            {
                direction = Vectors.Normalize(direction);

                Vector3 right = Vectors.CrossProduct(Vectors.Up(), direction);
                if (Vectors.SqrMagnitude(right) < 1e-6f)
                    right = Vectors.CrossProduct(Vectors.Forward(), direction);

                right = Vectors.Normalize(right);
                Vector3 forward = Vectors.Normalize(Vectors.CrossProduct(direction, right));

                joints[i].rotation = Quaternions.LookRotationCustom(forward, direction);
            }
        }

        if (joints.Length > 1)
            joints[joints.Length - 1].rotation = joints[joints.Length - 2].rotation;
    }

    // ---- Visuals sin jerarquía ----
    void UpdateSegmentVisuals()
    {
        if (segmentVisuals == null || segmentVisuals.Length == 0) return;
        if (joints == null || joints.Length < 2) return;

        int n = (int)MathLite.Min(segmentVisuals.Length, joints.Length - 1);

        for (int i = 0; i < n; i++)
        {
            Transform seg = segmentVisuals[i];
            if (seg == null || joints[i] == null || joints[i + 1] == null) continue;

            Vector3 a = joints[i].position;
            Vector3 b = joints[i + 1].position;

            Vector3 dir = b - a;
            float dist = Vectors.Magnitude(dir);
            if (dist < 1e-6f) continue;

            seg.position = a + dir * 0.5f;

            Vector3 direction = Vectors.Normalize(dir);
            Vector3 right = Vectors.CrossProduct(Vectors.Up(), direction);
            if (Vectors.SqrMagnitude(right) < 1e-6f)
                right = Vectors.CrossProduct(Vectors.Forward(), direction);

            right = Vectors.Normalize(right);
            Vector3 forward = Vectors.Normalize(Vectors.CrossProduct(direction, right));

            seg.rotation = Quaternions.LookRotationCustom(forward, direction);

            Vector3 s = seg.localScale;
            float denom = (segmentMeshHeight <= 1e-6f) ? 2f : segmentMeshHeight;
            s.y = dist / denom;
            seg.localScale = s;
        }
    }

    // ---- Validación: no cadena padre->hijo entre joints ----
    void ValidateNoJointHierarchyChain()
    {
        if (joints == null) return;

        for (int i = 0; i < joints.Length; i++)
        {
            if (joints[i] == null) continue;

            for (int j = 0; j < joints.Length; j++)
            {
                if (i == j || joints[j] == null) continue;
                if (joints[i].parent == joints[j])
                {
                    Debug.LogError("level3_FABRIKIK: Hay joints parentados entre sí. En Nivel 3 NO se puede usar jerarquía (cadena).");
                    return;
                }
            }
        }
    }

    // ---- Gizmos: círculo sin MathLite.Sin/Cos ----
    static readonly float TAU = 2f * MathLite.PI;

    static float WrapPi(float x) => MathLite.Repeat(x + MathLite.PI, TAU) - MathLite.PI;

    static float SinApprox(float x)
    {
        x = WrapPi(x);
        if (x > MathLite.PI * 0.5f) x = MathLite.PI - x;
        else if (x < -MathLite.PI * 0.5f) x = -MathLite.PI - x;

        float x2 = x * x;
        float x3 = x * x2;
        float x5 = x3 * x2;
        float x7 = x5 * x2;
        return x - x3 * (1f / 6f) + x5 * (1f / 120f) - x7 * (1f / 5040f);
    }

    static float CosApprox(float x)
    {
        x = WrapPi(x);
        float sign = 1f;
        x = MathLite.Abs(x);
        if (x > MathLite.PI * 0.5f)
        {
            x = MathLite.PI - x;
            sign = -1f;
        }

        float x2 = x * x;
        float x4 = x2 * x2;
        float x6 = x4 * x2;
        float c = 1f - x2 * 0.5f + x4 * (1f / 24f) - x6 * (1f / 720f);
        return sign * c;
    }

    void OnDrawGizmos()
    {
        if (!drawGizmos || joints == null || joints.Length == 0) return;

        Gizmos.color = gizmoColor;

        for (int i = 0; i < joints.Length - 1; i++)
        {
            if (joints[i] != null && joints[i + 1] != null)
            {
                Gizmos.DrawLine(joints[i].position, joints[i + 1].position);
                Gizmos.DrawWireSphere(joints[i].position, 0.05f);
            }
        }

        if (joints.Length > 0 && joints[joints.Length - 1] != null)
            Gizmos.DrawWireSphere(joints[joints.Length - 1].position, 0.08f);

        if (target != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(target.position, 0.1f);

            if (Application.isPlaying && joints.Length > 0)
            {
                Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
                DrawCircle_NoTrig(joints[0].position, totalReach, 32);
            }

            if (joints.Length > 0)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(joints[joints.Length - 1].position, target.position);
            }
        }
    }

    void DrawCircle_NoTrig(Vector3 center, float radius, int segments)
    {
        Vector3 prev = center + new Vector3(radius, 0, 0);

        for (int i = 1; i <= segments; i++)
        {
            float a = (float)i / segments * TAU;
            Vector3 next = center + new Vector3(CosApprox(a) * radius, 0f, SinApprox(a) * radius);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
}
