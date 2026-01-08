using System.Collections;
using UnityEngine;

public class level3_CCDIK : MonoBehaviour
{
    [Header("Chain (root -> ... -> end)")]
    public Transform[] joints;

    [Header("Visuals")]
    public Transform[] segmentVisuals;      
    public float segmentMeshHeight = 2f;   

    [Header("Target")]
    public Transform target; 

    [Header("CCD Parameters")]
    [Min(1)] public int maxIterations = 10;   
    [Min(0f)] public float tolerance = 0.01f; 
    [Range(0f, 1f)] public float rotationStep = 1f; 

    [Header("Debug")]
    public string algorithmName = "CCD"; 
    public int lastIterationsUsed;   
    public float currentDistance;      

    float[] segmentLengths;
    bool initialized = false;

    void Start()
    {
        InitializeSegmentLengths(); 
        UpdateSegmentVisuals();   
        StartCoroutine(AssignTarget());
    }

    // Busca el objeto "Left Target" tras un frame corto y lo asigna como target.
    private IEnumerator AssignTarget()
    {
        yield return new WaitForSeconds(0.01f);
        var go = GameObject.Find("Left Target");
        if (go) target = go.transform;
    }

    // Setter público para cambiar el target desde otros scripts.
    public void SetTarget(Transform newTarget)
    {
        Debug.Log("Setting new target for IK");
        target = newTarget;
    }

    // Quita el target (desactiva el solve en LateUpdate).
    public void ResetTarget()
    {
        target = null;
    }

    // Calcula y guarda las longitudes de cada segmento entre joints consecutivos.
    void InitializeSegmentLengths()
    {
        if (joints == null || joints.Length < 2) return;

        segmentLengths = new float[joints.Length - 1];
        for (int i = 0; i < joints.Length - 1; i++)
            segmentLengths[i] = Vectors.Distance(joints[i].position, joints[i + 1].position);

        initialized = true;
    }

    void LateUpdate()
    {
        if (target == null) return;              // Sin target no resolvemos.
        if (joints == null || joints.Length < 2) return;
        if (!initialized) InitializeSegmentLengths();

        // Resuelve CCD hacia target con límites y paso parcial.
        lastIterationsUsed = SolveCCD_WithConstraints(joints, target.position, maxIterations, tolerance, rotationStep);

        // Debug: distancia actual del end-effector al target.
        Transform end = joints[joints.Length - 1];
        currentDistance = Vectors.Distance(end.position, target.position);

        UpdateSegmentVisuals(); // Actualiza mallas de segmentos para que “sigan” a los joints.
    }

    // CCD: rota joints desde el penúltimo al primero para alinear end->target, aplicando constraints de distancia.
    int SolveCCD_WithConstraints(Transform[] chain, Vector3 targetPos, int maxIters, float tol, float rotStep)
    {
        // Sanitiza parámetros (sin Mathf).
        maxIters = MathLite.ClampInt(maxIters, 1, 1000);
        tol = MathLite.Max(0f, tol);
        rotStep = MathLite.Clamp01(rotStep);

        int endIndex = chain.Length - 1;
        Transform end = chain[endIndex];

        int used = 0;

        for (int it = 0; it < maxIters; ++it)
        {
            used = it + 1;

            // Si ya estamos dentro de tolerancia, paramos.
            float err = Vectors.Distance(end.position, targetPos);
            if (err <= tol) break;

            // Recorre joints desde el final hacia la raíz (CCD).
            for (int i = endIndex - 1; i >= 0; --i)
            {
                Transform joint = chain[i];
                if (joint == null) continue;

                Vector3 pivot = joint.position;
                Vector3 toEnd = end.position - pivot;     // Vector del joint al end.
                Vector3 toTarget = targetPos - pivot;     // Vector del joint al target.

                // Evita degenerados.
                if (Vectors.SqrMagnitude(toEnd) < 1e-12f) continue;
                if (Vectors.SqrMagnitude(toTarget) < 1e-12f) continue;

                // Rotación que lleva toEnd hacia toTarget (sin trig).
                Quaternion fullDelta = FromToRotation_NoTrig(toEnd, toTarget);

                // Aplica paso parcial si rotStep < 1 (suaviza / estabiliza).
                Quaternion delta = (rotStep < 0.999f)
                    ? Nlerp(Quaternion.identity, fullDelta, rotStep)
                    : fullDelta;

                delta = Quaternions.Normalize(delta);

                // Rota el joint actual.
                joint.rotation = Quaternions.Normalize(
                    Quaternions.Multiply(delta, joint.rotation)
                );

                // Aplica la misma rotación a todos los joints posteriores (posición y rotación).
                for (int j = i + 1; j <= endIndex; ++j)
                {
                    Transform t = chain[j];
                    if (t == null) continue;

                    Vector3 r = t.position - pivot;
                    Vector3 rRot = Quaternions.Rotate3D(r, delta);
                    t.position = pivot + rRot;

                    t.rotation = Quaternions.Normalize(
                        Quaternions.Multiply(delta, t.rotation)
                    );
                }

                ApplyDistanceConstraints(); // Re-ajusta distancias para mantener longitudes originales.
            }
        }

        return used;
    }

    // Fuerza que cada segmento mantenga su longitud original (posiciona b a distancia fija de a).
    void ApplyDistanceConstraints()
    {
        if (segmentLengths == null || segmentLengths.Length == 0) return;

        for (int i = 0; i < joints.Length - 1; i++)
        {
            Transform a = joints[i];
            Transform b = joints[i + 1];
            if (a == null || b == null) continue;

            Vector3 d = b.position - a.position;
            float len = Vectors.Magnitude(d);
            if (len > 0.0001f)
            {
                d = Vectors.Normalize(d);
                b.position = a.position + d * segmentLengths[i];
            }
        }
    }

    // Actualiza las mallas/cilindros para que conecten visualmente joints[i] con joints[i+1].
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

            // Posiciona el segmento en el punto medio.
            Vector3 mid = a + dir * 0.5f;
            seg.position = mid;

            Vector3 direction = Vectors.Normalize(dir);

            // Construye una base ortonormal y rota el seg para que su “Y” apunte al siguiente joint.
            Vector3 right = Vectors.CrossProduct(Vectors.Up(), direction);
            if (Vectors.SqrMagnitude(right) < 1e-6f)
                right = Vectors.CrossProduct(Vectors.Forward(), direction);

            right = Vectors.Normalize(right);
            Vector3 forward = Vectors.Normalize(Vectors.CrossProduct(direction, right));

            seg.rotation = Quaternions.LookRotationCustom(forward, direction);

            // Escala en Y para que el cilindro tenga la longitud correcta.
            Vector3 s = seg.localScale;
            float denom = (segmentMeshHeight <= 1e-6f) ? 2f : segmentMeshHeight;
            s.y = dist / denom;
            seg.localScale = s;
        }
    }

    // Nlerp: interpolación lineal normalizada entre dos quaternions (útil para rotStep parcial).
    static Quaternion Nlerp(Quaternion a, Quaternion b, float t)
    {
        t = MathLite.Clamp01(t);
        float dot = a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w;
        if (dot < 0f) b = new Quaternion(-b.x, -b.y, -b.z, -b.w); // Mantiene el camino corto.

        Quaternion q = new Quaternion(
            a.x + (b.x - a.x) * t,
            a.y + (b.y - a.y) * t,
            a.z + (b.z - a.z) * t,
            a.w + (b.w - a.w) * t
        );
        return Quaternions.Normalize(q);
    }

    // Calcula el quaternion que rota "from" hacia "to" sin usar trig (casos: igual, opuesto, general).
    static Quaternion FromToRotation_NoTrig(Vector3 from, Vector3 to)
    {
        Vector3 f = Vectors.Normalize(from);
        Vector3 t = Vectors.Normalize(to);
        float dot = Vectors.DotProduct(f, t);

        if (dot > 0.999999f) return Quaternion.identity; // Ya alineados.

        if (dot < -0.999999f)
        {
            // 180º: elige un eje ortogonal a f.
            Vector3 ortho = MathLite.Abs(f.x) < 0.1f ? new Vector3(1, 0, 0) : new Vector3(0, 1, 0);
            Vector3 axis = Vectors.Normalize(Vectors.CrossProduct(f, ortho));
            return Quaternions.Normalize(new Quaternion(axis.x, axis.y, axis.z, 0f));
        }

        // Caso general: fórmula basada en cross y dot.
        Vector3 axis2 = Vectors.CrossProduct(f, t);
        float s2 = MathLite.Sqrt((1f + dot) * 2f);
        float invs2 = 1f / s2;
        Quaternion q = new Quaternion(axis2.x * invs2, axis2.y * invs2, axis2.z * invs2, s2 * 0.5f);
        return Quaternions.Normalize(q);
    }
}
