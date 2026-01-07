using System.Collections;
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

    Vector3[] positions;
    float[] lengths;
    Vector3 basePosition;

    void Start()
    {
        InitializeFABRIK();
        UpdateSegmentVisuals();
        StartCoroutine(AssignTarget());
    }

    private IEnumerator AssignTarget()
    {
        yield return new WaitForSeconds(0.01f);
        var go = GameObject.Find("Right Target");
            if (go) target = go.transform;
    }

    void InitializeFABRIK()
    {
        if (joints == null || joints.Length < 2)
        {
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

        // Rotación del joint: no depende de hijos
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
}