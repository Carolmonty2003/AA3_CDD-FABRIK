using UnityEngine;

/// <summary>
/// CCDIK (Cyclic Coordinate Descent) para una cadena de joints.
/// - Objetivo: rotar joints (de end hacia base) para que el end-effector alcance el target.
/// - Incluye:
///   - clamp de reach (evita targets imposibles)
///   - suavizado del target (reduce jitter por movimiento del target)
///   - avoidance de colisiones (busca rotaciones alternativas si choca con obstáculos)
///   - pesos por joint (movimiento más “natural”: base rota menos, end rota más)
/// </summary>
public class CCDIK : MonoBehaviour
{
    [Header("Chain (root -> ... -> end)")]
    public Transform[] joints;

    [Header("Target")]
    public Transform target;

    [Header("CCD Params")]
    [Min(1)] public int maxIterations = 25;
    [Min(0f)] public float tolerance = 0.01f;
    [Range(0f, 1f)] public float rotationStep = 0.45f;
    [Min(0f)] public float maxAnglePerJointDeg = 15f;

    [Header("Reach handling (IMPORTANT)")]
    public bool clampUnreachableTarget = true;
    [Min(0f)] public float reachEpsilon = 0.02f;
    [Tooltip("Solo lectura: alcance total de la cadena (suma longitudes).")]
    public float totalReach = 0f;

    [Header("COLLISION AVOIDANCE")]
    public bool enableCollisionAvoidance = true;
    public float collisionRadius = 0.12f;
    public LayerMask obstacleLayer = -1;
    [Range(4, 24)] public int deflectionSamples = 12;
    [Range(10f, 180f)] public float deflectionAngle = 70f;

    [Header("Stability / Natural Motion")]
    public bool smoothTarget = true;
    [Min(0f)] public float targetDamping = 18f;

    public bool useJointWeights = true;
    [Range(0f, 1f)] public float baseJointWeight = 0.08f;
    [Range(0f, 1f)] public float endJointWeight = 1.0f;
    [Min(0.1f)] public float weightExponent = 2.6f;

    [Header("Avoidance scoring (NEW)")]
    public float collisionWeight = 10f;
    public float distanceWeight = 2.0f;
    public float minDistanceGain = 0.002f;

    [Header("Debug")]
    public bool debugCollisions = true;
    public int lastIterationsUsed;
    public float currentDistance;
    public int collisionsAvoided = 0;
    public int deflectionsApplied = 0;

    bool initialized = false;

    // Variables para suavizado del target (exponential smoothing)
    Vector3 _smoothedTarget;
    bool _hasSmoothed;

    // Longitudes por segmento (útil para reach total) y caché de ejes para estabilidad
    float[] _lengths;
    Vector3[] _axisCache;

    void Start() => Initialize();

    /// <summary>
    /// Precalcula:
    /// - longitudes de segmentos
    /// - totalReach (suma longitudes)
    /// - caché de ejes por joint (para casos degenerados cuando cross-product da 0)
    /// </summary>
    void Initialize()
    {
        if (joints == null || joints.Length < 2)
        {
            Debug.LogError("CCDIK: Necesitas al menos 2 joints.");
            return;
        }

        _lengths = new float[joints.Length - 1];
        totalReach = 0f;

        for (int i = 0; i < joints.Length - 1; i++)
        {
            float len = Vector3.Distance(joints[i].position, joints[i + 1].position);
            _lengths[i] = len;
            totalReach += len;
        }

        _axisCache = new Vector3[joints.Length];
        for (int i = 0; i < _axisCache.Length; i++)
            _axisCache[i] = transform.up;

        initialized = true;
        _hasSmoothed = false;
    }

    /// <summary>
    /// LateUpdate para que el IK se aplique después de que otros scripts hayan movido el target/escena.
    /// Flujo:
    /// 1) limpiar contadores debug
    /// 2) obtener targetPos (y suavizar si procede)
    /// 3) clamp de reach si el target está fuera
    /// 4) ejecutar CCD
    /// 5) guardar distancia final (debug)
    /// </summary>
    void LateUpdate()
    {
        if (target == null) return;
        if (joints == null || joints.Length < 2) return;
        if (!initialized) Initialize();

        collisionsAvoided = 0;
        deflectionsApplied = 0;

        Vector3 targetPos = target.position;

        // Suavizado exponencial del target: reduce micro-jitter si el target se mueve/tiembla.
        if (smoothTarget)
        {
            if (!_hasSmoothed)
            {
                _smoothedTarget = targetPos;
                _hasSmoothed = true;
            }

            float t = MathLite.ExpDampT(targetDamping, Time.deltaTime);
            _smoothedTarget = Lerp.Lerpp(_smoothedTarget, targetPos, t);
            targetPos = _smoothedTarget;
        }

        // Clamp del target a una esfera de radio (totalReach - epsilon) alrededor de la base del brazo.
        if (clampUnreachableTarget)
        {
            float max = Mathf.Max(0f, totalReach - reachEpsilon);
            targetPos = Vectors.ClampToMaxDistance(joints[0].position, targetPos, max);
        }

        lastIterationsUsed = SolveCCD(chain: joints, targetPos: targetPos);

        Transform end = joints[joints.Length - 1];
        currentDistance = Vector3.Distance(end.position, targetPos);
    }

    /// <summary>
    /// Solver CCD:
    /// - Itera hasta maxIterations o hasta que el end-effector esté dentro de tolerance.
    /// - En cada iteración recorre joints desde end-1 hasta 0:
    ///     - calcula rotación "desired" para alinear (joint->end) con (joint->target)
    ///     - aplica clamp de grados por joint
    ///     - (opcional) busca alternativa sin colisión (deflection)
    ///     - aplica step/weights (para que el movimiento sea gradual y “natural”)
    ///     - aplica la rotación al joint y propaga a los hijos de la cadena (reposiciona)
    /// </summary>
    int SolveCCD(Transform[] chain, Vector3 targetPos)
    {
        int endIndex = chain.Length - 1;
        Transform end = chain[endIndex];

        int used = 0;

        for (int it = 0; it < maxIterations; ++it)
        {
            used = it + 1;

            float err = Vector3.Distance(end.position, targetPos);
            if (err <= tolerance) break;

            for (int i = endIndex - 1; i >= 0; --i)
            {
                Transform joint = chain[i];
                if (joint == null) continue;

                Vector3 pivot = joint.position;

                Vector3 toEnd = end.position - pivot;
                Vector3 toTarget = targetPos - pivot;

                if (toEnd.sqrMagnitude < 1e-12f) continue;
                if (toTarget.sqrMagnitude < 1e-12f) continue;

                // Rotación que alinea el vector hacia el end con el vector hacia el target.
                Quaternion desiredRotation = Quaternions.FromToRotation(toEnd, toTarget);
                Quaternion finalRotation = desiredRotation;

                // Límite máximo de giro por joint (evita cambios bruscos).
                finalRotation = Quaternions.ClampDeltaRotation(finalRotation, maxAnglePerJointDeg);

                // Axis estable para orientar deflection/avoidance.
                // Si el cross es degenerado, usa un eje cacheado del frame anterior.
                Vector3 axis = Vector3.Cross(toEnd, toTarget);
                if (axis.sqrMagnitude > 1e-12f)
                {
                    axis.Normalize();
                    _axisCache[i] = axis;
                }
                else
                {
                    axis = _axisCache[i];
                    if (axis.sqrMagnitude < 1e-12f) axis = transform.up;
                }

                // Avoidance: si la rotación deseada colisiona, prueba variantes (deflection)
                // y escoge la mejor según score (colisión + distancia al target).
                if (enableCollisionAvoidance)
                {
                    finalRotation = FindCollisionFreeRotation(
                        jointIndex: i,
                        endIndex: endIndex,
                        desiredRotation: finalRotation,
                        pivot: pivot,
                        currentDirection: toEnd,
                        primaryAxis: axis,
                        targetPos: targetPos
                    );

                    // Se clampa otra vez por seguridad (evita que el avoidance meta giros grandes).
                    finalRotation = Quaternions.ClampDeltaRotation(finalRotation, maxAnglePerJointDeg);
                }

                // Step base: fracción de la rotación a aplicar este frame/iteración.
                float step = Mathf.Clamp01(rotationStep);

                // Pesos por joint: reduce movimiento en base y aumenta cerca del end.
                if (useJointWeights)
                {
                    float u = (endIndex <= 1) ? 1f : (i / (float)(endIndex - 1));
                    u = Mathf.Clamp01(u);

                    float curved = Mathf.Pow(u, weightExponent);
                    float w = Mathf.Lerp(baseJointWeight, endJointWeight, curved);

                    step = Mathf.Clamp01(step * w);
                }

                // Si “aun así” hay colisión en esta rotación, baja el step para reducir jitter al contacto.
                if (enableCollisionAvoidance)
                {
                    float pNow = CollisionPenalty(i, endIndex, finalRotation, pivot);
                    if (pNow > 0f)
                        step *= 0.35f;
                }

                // Aplicación gradual del delta (slerp hacia identidad).
                if (step < 0.999f)
                    finalRotation = Lerp.SLerp(Quaternion.identity, finalRotation, step);

                finalRotation = Quaternions.Normalize(finalRotation);

                // Aplica al joint.
                joint.rotation = Quaternions.Normalize(Quaternions.Multiply(finalRotation, joint.rotation));

                // Propaga a todos los hijos de la cadena:
                // - rota posiciones alrededor del pivot
                // - rota también sus orientaciones
                for (int j = i + 1; j <= endIndex; ++j)
                {
                    Transform child = chain[j];
                    if (child == null) continue;

                    Vector3 r = child.position - pivot;
                    Vector3 rRot = Quaternions.Rotate3D(r, finalRotation);
                    child.position = pivot + rRot;

                    child.rotation = Quaternions.Normalize(Quaternions.Multiply(finalRotation, child.rotation));
                }
            }
        }

        return used;
    }

    /// <summary>
    /// Busca una rotación alternativa cuando "desiredRotation" colisiona.
    /// Estrategia:
    /// - calcula penalización de colisión y distancia al target para la rotación deseada
    /// - si colisiona, prueba rotaciones "deflection" alrededor de ejes candidatos (primary y alternativo)
    /// - puntúa cada opción con un score (menos colisión y más cerca del target)
    /// - si no mejora nada útil, devuelve identidad para evitar jitter
    /// </summary>
    Quaternion FindCollisionFreeRotation(
        int jointIndex,
        int endIndex,
        Quaternion desiredRotation,
        Vector3 pivot,
        Vector3 currentDirection,
        Vector3 primaryAxis,
        Vector3 targetPos
    )
    {
        float desiredPenalty = CollisionPenalty(jointIndex, endIndex, desiredRotation, pivot);

        Vector3 endR = joints[endIndex].position - pivot;
        Vector3 desiredEnd = pivot + Quaternions.Rotate3D(endR, desiredRotation);
        float desiredDist = Vector3.Distance(desiredEnd, targetPos);

        if (desiredPenalty <= 0f)
            return desiredRotation;

        collisionsAvoided++;

        Vector3 cd = currentDirection.normalized;

        // Eje alternativo por si el principal no resuelve bien en este ángulo/configuración.
        Vector3 altAxis = Vector3.Cross(primaryAxis, cd);
        if (altAxis.sqrMagnitude > 1e-12f) altAxis.Normalize();
        else altAxis = Vector3.right;

        Vector3[] axesToTry = new Vector3[] { primaryAxis, altAxis };

        Quaternion bestRotation = desiredRotation;
        float bestPenalty = desiredPenalty;
        float bestDist = desiredDist;
        float bestScore = float.MinValue;

        // Muestras progresivas: desde un ángulo pequeño hasta deflectionAngle.
        for (int sample = 1; sample <= deflectionSamples; sample++)
        {
            float angleDeg = (sample / (float)deflectionSamples) * deflectionAngle;

            for (int a = 0; a < axesToTry.Length; a++)
            {
                Vector3 axis = axesToTry[a];

                // Prueba en ambas direcciones (+ y -)
                for (int dir = -1; dir <= 1; dir += 2)
                {
                    float testAngle = angleDeg * dir * Mathf.Deg2Rad;
                    Quaternion deflection = Quaternions.AxisAngle(axis, testAngle);
                    Quaternion testRotation = Quaternions.Normalize(Quaternions.Multiply(deflection, desiredRotation));

                    float p = CollisionPenalty(jointIndex, endIndex, testRotation, pivot);

                    Vector3 testEnd = pivot + Quaternions.Rotate3D(endR, testRotation);
                    float d = Vector3.Distance(testEnd, targetPos);

                    float score = (-p * collisionWeight) + (-d * distanceWeight);

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestRotation = testRotation;
                        bestPenalty = p;
                        bestDist = d;
                    }

                    if (bestPenalty <= 0f) break;
                }

                if (bestPenalty <= 0f) break;
            }

            if (bestPenalty <= 0f) break;
        }

        // Si mejora colisión respecto a la deseada, úsala.
        if (bestPenalty < desiredPenalty)
        {
            deflectionsApplied++;
            return bestRotation;
        }

        // Si no mejora colisión pero mejora distancia lo suficiente, permite avanzar.
        if (bestDist < desiredDist - minDistanceGain)
            return bestRotation;

        // Si no mejora nada, no rotar (reduce jitter).
        return Quaternion.identity;
    }

    /// <summary>
    /// Penalización de colisión del “brazo” tras aplicar una rotación delta en un joint.
    /// Implementación:
    /// - construye segmentos (pivot -> cada joint siguiente) rotados
    /// - evalúa OverlapCapsule por segmento
    /// - suma 1 por collider válido detectado (ignorando triggers y el propio brazo)
    /// </summary>
    float CollisionPenalty(int jointIndex, int endIndex, Quaternion rotation, Vector3 pivot)
    {
        float penalty = 0f;
        Vector3 prev = pivot;

        for (int j = jointIndex + 1; j <= endIndex; j++)
        {
            Vector3 r = joints[j].position - pivot;
            Vector3 curr = pivot + Quaternions.Rotate3D(r, rotation);

            Collider[] hits = Physics.OverlapCapsule(prev, curr, collisionRadius, obstacleLayer);

            for (int h = 0; h < hits.Length; h++)
            {
                Collider hit = hits[h];
                if (hit == null) continue;
                if (hit.isTrigger) continue;
                if (IsPartOfArm(hit.gameObject)) continue;
                penalty += 1f;
            }

            prev = curr;
        }

        return penalty;
    }

    /// <summary>
    /// Evita que el avoidance se detecte a sí mismo: ignora colliders del propio brazo/jerarquía.
    /// </summary>
    bool IsPartOfArm(GameObject obj)
    {
        foreach (Transform joint in joints)
        {
            if (joint == null) continue;
            if (obj == joint.gameObject) return true;
            if (obj.transform.IsChildOf(joint)) return true;
            if (joint.IsChildOf(obj.transform)) return true;
        }

        if (obj == gameObject || obj.transform.IsChildOf(transform))
            return true;

        return false;
    }

    /// <summary>
    /// Gizmos para visualizar el radio de colisión usado por el avoidance.
    /// </summary>
    void OnDrawGizmos()
    {
        if (!debugCollisions || joints == null) return;

        Gizmos.color = new Color(0f, 1f, 0f, 0.15f);
        foreach (Transform joint in joints)
        {
            if (joint != null)
                Gizmos.DrawWireSphere(joint.position, collisionRadius);
        }
    }
}
