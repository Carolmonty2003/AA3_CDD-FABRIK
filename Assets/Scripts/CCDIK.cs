using UnityEngine;

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
    [Tooltip("Peso de colisión (más alto = evita más obstáculos, pero puede atascarse).")]
    public float collisionWeight = 10f;
    [Tooltip("Peso de distancia al target (más alto = recalcula mejor y no se queda clavado).")]
    public float distanceWeight = 2.0f;
    [Tooltip("Ganancia mínima de distancia para aceptar rotación aunque no mejore colisión.")]
    public float minDistanceGain = 0.002f;

    [Header("Debug")]
    public bool debugCollisions = true;
    public int lastIterationsUsed;
    public float currentDistance;
    public int collisionsAvoided = 0;
    public int deflectionsApplied = 0;

    bool initialized = false;

    Vector3 _smoothedTarget;
    bool _hasSmoothed;

    float[] _lengths;
    Vector3[] _axisCache;

    void Start() => Initialize();

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

    void LateUpdate()
    {
        if (target == null) return;
        if (joints == null || joints.Length < 2) return;
        if (!initialized) Initialize();

        collisionsAvoided = 0;
        deflectionsApplied = 0;

        Vector3 targetPos = target.position;

        if (smoothTarget)
        {
            if (!_hasSmoothed)
            {
                _smoothedTarget = targetPos;
                _hasSmoothed = true;
            }

            float t = 1f - Mathf.Exp(-targetDamping * Time.deltaTime);
            _smoothedTarget = Vector3.Lerp(_smoothedTarget, targetPos, t);
            targetPos = _smoothedTarget;
        }

        if (clampUnreachableTarget)
            targetPos = ClampToReach(targetPos);

        lastIterationsUsed = SolveCCD(chain: joints, targetPos: targetPos);

        Transform end = joints[joints.Length - 1];
        currentDistance = Vector3.Distance(end.position, targetPos);
    }

    Vector3 ClampToReach(Vector3 targetPos)
    {
        if (totalReach <= 0f) return targetPos;

        Vector3 basePos = joints[0].position;
        Vector3 v = targetPos - basePos;
        float dist = v.magnitude;

        float max = Mathf.Max(0f, totalReach - reachEpsilon);
        if (dist > max && dist > 1e-6f)
            return basePos + (v / dist) * max;

        return targetPos;
    }

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

                Quaternion desiredRotation = Quaternions.FromToRotation(toEnd, toTarget);
                Quaternion finalRotation = desiredRotation;

                // Clamp ángulo por joint
                if (maxAnglePerJointDeg > 0f)
                {
                    float ang = Quaternion.Angle(Quaternion.identity, finalRotation);
                    if (ang > maxAnglePerJointDeg && ang > 1e-6f)
                    {
                        float clampT = maxAnglePerJointDeg / ang;
                        finalRotation = Lerp.SLerp(Quaternion.identity, finalRotation, clampT);
                    }
                }

                // Eje real + cache
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

                // Avoidance (con término de distancia)
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

                    // MUY IMPORTANTE: la deflection puede saltarse el clamp anterior
                    finalRotation = ClampDeltaRotation(finalRotation, maxAnglePerJointDeg);
                }

                // Step con pesos
                float step = Mathf.Clamp01(rotationStep);

                if (useJointWeights)
                {
                    float u = (endIndex <= 1) ? 1f : (i / (float)(endIndex - 1));
                    u = Mathf.Clamp01(u);

                    float curved = Mathf.Pow(u, weightExponent);
                    float w = Mathf.Lerp(baseJointWeight, endJointWeight, curved);

                    step = Mathf.Clamp01(step * w);
                }

                // Si estamos tocando obstáculo, baja agresividad para evitar espasmos
                if (enableCollisionAvoidance)
                {
                    float pNow = CollisionPenalty(i, endIndex, finalRotation, pivot);
                    if (pNow > 0f)
                        step *= 0.35f;   // prueba 0.25–0.5
                }

                if (step < 0.999f)
                    finalRotation = Lerp.SLerp(Quaternion.identity, finalRotation, step);

                finalRotation = Quaternions.Normalize(finalRotation);

                joint.rotation = Quaternions.Normalize(Quaternions.Multiply(finalRotation, joint.rotation));

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

        // Distancia del end-effector tras aplicar "desired"
        Vector3 endR = joints[endIndex].position - pivot;
        Vector3 desiredEnd = pivot + Quaternions.Rotate3D(endR, desiredRotation);
        float desiredDist = Vector3.Distance(desiredEnd, targetPos);

        if (desiredPenalty <= 0f)
            return desiredRotation;

        collisionsAvoided++;

        Vector3 cd = currentDirection.normalized;

        Vector3 altAxis = Vector3.Cross(primaryAxis, cd);
        if (altAxis.sqrMagnitude > 1e-12f) altAxis.Normalize();
        else altAxis = Vector3.right;

        Vector3[] axesToTry = new Vector3[] { primaryAxis, altAxis };

        Quaternion bestRotation = desiredRotation;
        float bestPenalty = desiredPenalty;
        float bestDist = desiredDist;
        float bestScore = float.MinValue;

        for (int sample = 1; sample <= deflectionSamples; sample++)
        {
            float angleDeg = (sample / (float)deflectionSamples) * deflectionAngle;

            for (int a = 0; a < axesToTry.Length; a++)
            {
                Vector3 axis = axesToTry[a];

                for (int dir = -1; dir <= 1; dir += 2)
                {
                    float testAngle = angleDeg * dir * Mathf.Deg2Rad;
                    Quaternion deflection = Quaternions.AxisAngle(axis, testAngle);
                    Quaternion testRotation = Quaternions.Normalize(Quaternions.Multiply(deflection, desiredRotation));

                    float p = CollisionPenalty(jointIndex, endIndex, testRotation, pivot);

                    Vector3 testEnd = pivot + Quaternions.Rotate3D(endR, testRotation);
                    float d = Vector3.Distance(testEnd, targetPos);

                    // Score: prioriza bajar colisión, pero también acercarse al target (para “recalcular”)
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

        // Si mejora colisión, perfecto
        if (bestPenalty < desiredPenalty)
        {
            deflectionsApplied++;
            return bestRotation;
        }

        // Si NO mejora colisión, pero sí se acerca bastante al target, permite avanzar (evita quedarse clavado)
        if (bestDist < desiredDist - minDistanceGain)
            return bestRotation;

        // Si no mejora nada útil -> no rotar (anti-jitter)
        return Quaternion.identity;
    }

    Quaternion ClampDeltaRotation(Quaternion delta, float maxDeg)
    {
        if (maxDeg <= 0f) return delta;

        float ang = Quaternion.Angle(Quaternion.identity, delta);
        if (ang > maxDeg && ang > 1e-6f)
        {
            float t = maxDeg / ang;
            return Lerp.SLerp(Quaternion.identity, delta, t);
        }
        return delta;
    }


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
