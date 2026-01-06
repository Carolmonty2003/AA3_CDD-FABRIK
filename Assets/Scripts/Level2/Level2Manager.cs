using System;
using System.Collections;
using UnityEngine;

public class Level2Manager : MonoBehaviour
{
    [Serializable]
    public class Step
    {
        public SequenceButton expectedButton;
        public Laser[] disableLasers;
        public Laser[] enableLasers;
    }

    [Header("References")]
    [SerializeField] private Transform ikTarget;      // Arm (FABRIK)
    [SerializeField] private Transform endEffector;   // opcional; si null usa el último joint del FABRIK
    [SerializeField] private Step[] steps;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 0.35f;
    [SerializeField] private float pressDistance = 0.10f;
    [SerializeField] private float stopAfterPressSeconds = 1.0f;

    [Header("Avoidance (target)")]
    [SerializeField] private LayerMask obstacleMask;           // Layer Laser
    [SerializeField] private float avoidanceRadius = 0.10f;
    [SerializeField] private float avoidanceLookAhead = 0.60f;
    [SerializeField] private float avoidanceStrength = 2.0f;
    [SerializeField] private float emergencyPushStrength = 3.0f;

    [Header("Avoidance (whole chain)")]
    [SerializeField] private float chainRadius = 0.08f;        // “grosor” del brazo para el chequeo cápsula
    [SerializeField] private float chainPushStrength = 4.0f;   // cuánto prioriza sacar el brazo de los láseres
    [SerializeField] private int overlapBufferSize = 32;

    private FABRIKIK fabrik;
    private Transform runtimeTarget;
    private int currentStep;
    private Coroutine runner;

    private Collider[] overlapBuffer;

    private void Awake()
    {
        overlapBuffer = new Collider[Mathf.Max(8, overlapBufferSize)];

        BindFabrik();
        EnsureFabrikTarget();

        if (fabrik != null && endEffector == null && fabrik.joints != null && fabrik.joints.Length > 0)
            endEffector = fabrik.joints[fabrik.joints.Length - 1];

        ResetSequence();
    }

    private void Start()
    {
        if (!IsReady()) return;

        // Evitar salto: empieza el target donde está la punta
        fabrik.target.position = endEffector.position;
        fabrik.isActive = true;

        runner = StartCoroutine(RunSequence());
    }

    private void BindFabrik()
    {
        if (ikTarget != null)
            fabrik = ikTarget.GetComponent<FABRIKIK>();

        if (fabrik == null)
            fabrik = FindFirstObjectByType<FABRIKIK>();
    }

    private void EnsureFabrikTarget()
    {
        if (fabrik == null) return;

        bool needsTarget = fabrik.target == null || IsJointTransform(fabrik, fabrik.target);
        if (!needsTarget) return;

        if (runtimeTarget == null)
        {
            var go = new GameObject("FABRIK_RuntimeTarget");
            go.hideFlags = HideFlags.HideAndDontSave;
            runtimeTarget = go.transform;
        }

        if (fabrik.joints != null && fabrik.joints.Length > 0 && fabrik.joints[fabrik.joints.Length - 1] != null)
            runtimeTarget.position = fabrik.joints[fabrik.joints.Length - 1].position;
        else
            runtimeTarget.position = fabrik.transform.position;

        fabrik.target = runtimeTarget;
    }

    private static bool IsJointTransform(FABRIKIK f, Transform t)
    {
        if (f == null || t == null || f.joints == null) return false;
        for (int i = 0; i < f.joints.Length; i++)
            if (f.joints[i] == t) return true;
        return false;
    }

    private bool IsReady()
    {
        if (fabrik == null) return false;
        if (fabrik.target == null) return false;
        if (endEffector == null) return false;
        if (steps == null || steps.Length == 0) return false;
        if (fabrik.joints == null || fabrik.joints.Length < 2) return false;
        return true;
    }

    private IEnumerator RunSequence()
    {
        while (currentStep < steps.Length)
        {
            var step = steps[currentStep];
            if (step == null || step.expectedButton == null) yield break;

            SetOnlyCurrentButtonPressable(currentStep);

            Vector3 goal = step.expectedButton.GetPressWorldPosition();

            while (Vector3.Distance(endEffector.position, goal) > pressDistance)
            {
                MoveFabrikTargetTowards(goal);
                yield return null; // FABRIK resuelve en LateUpdate
            }

            step.expectedButton.SimulatePress();
            ApplyStep(step);

            currentStep++;
            yield return new WaitForSeconds(stopAfterPressSeconds);
        }

        SetOnlyCurrentButtonPressable(-1);
    }

    private void MoveFabrikTargetTowards(Vector3 goal)
    {
        Vector3 from = fabrik.target.position;
        Vector3 toGoal = goal - from;

        if (toGoal.sqrMagnitude < 1e-8f) return;

        Vector3 desiredDir = toGoal.normalized;
        float castDist = Mathf.Min(avoidanceLookAhead, toGoal.magnitude);

        Vector3 steering = desiredDir;

        // 1) Evitación proactiva para el target
        if (Physics.SphereCast(from, avoidanceRadius, desiredDir, out _, castDist, obstacleMask, QueryTriggerInteraction.Collide))
        {
            Vector3 left = Vector3.Cross(Vector3.up, desiredDir).normalized;
            if (left.sqrMagnitude < 1e-6f) left = Vector3.Cross(Vector3.forward, desiredDir).normalized;
            Vector3 right = -left;

            float leftClear = Clearance(from, left, castDist);
            float rightClear = Clearance(from, right, castDist);

            Vector3 side = (leftClear >= rightClear) ? left : right;
            steering = (desiredDir + side * avoidanceStrength).normalized;
        }

        // 2) Empuje de emergencia si el target está rozando
        Vector3 emergency = ComputeEmergencyPush(from);
        if (emergency.sqrMagnitude > 1e-8f)
            steering = (steering + emergency.normalized * emergencyPushStrength).normalized;

        // 3) B) Anti-cruce de TODA la cadena: si cualquier segmento interseca láseres, empuja fuera
        Vector3 chainPush = ComputeChainPush();
        if (chainPush.sqrMagnitude > 1e-8f)
            steering = (steering + chainPush.normalized * chainPushStrength).normalized;

        float step = moveSpeed * Time.deltaTime;
        if (step > toGoal.magnitude) step = toGoal.magnitude;

        fabrik.target.position = from + steering * step;
    }

    private float Clearance(Vector3 from, Vector3 dir, float dist)
    {
        if (Physics.SphereCast(from, avoidanceRadius, dir, out RaycastHit h, dist, obstacleMask, QueryTriggerInteraction.Collide))
            return h.distance;
        return dist;
    }

    private Vector3 ComputeEmergencyPush(Vector3 pos)
    {
        int hits = Physics.OverlapSphereNonAlloc(pos, avoidanceRadius * 1.2f, overlapBuffer, obstacleMask, QueryTriggerInteraction.Collide);
        if (hits <= 0) return Vector3.zero;

        Vector3 push = Vector3.zero;
        for (int i = 0; i < hits; i++)
        {
            var col = overlapBuffer[i];
            if (col == null) continue;

            Vector3 closest = col.ClosestPoint(pos);
            Vector3 away = pos - closest;

            float d = away.magnitude;
            if (d < 0.0001f) continue;

            push += away.normalized * (1f / (d + 0.001f));
        }

        return Vector3.ProjectOnPlane(push, Vector3.up);
    }

    private Vector3 ComputeChainPush()
    {
        var joints = fabrik.joints;
        if (joints == null || joints.Length < 2) return Vector3.zero;

        Vector3 total = Vector3.zero;
        int contributions = 0;

        for (int i = 0; i < joints.Length - 1; i++)
        {
            Transform aT = joints[i];
            Transform bT = joints[i + 1];
            if (aT == null || bT == null) continue;

            Vector3 a = aT.position;
            Vector3 b = bT.position;

            // cápsula del segmento (a->b)
            int hits = Physics.OverlapCapsuleNonAlloc(a, b, chainRadius, overlapBuffer, obstacleMask, QueryTriggerInteraction.Collide);
            if (hits <= 0) continue;

            Vector3 mid = (a + b) * 0.5f;

            for (int h = 0; h < hits; h++)
            {
                Collider col = overlapBuffer[h];
                if (col == null) continue;

                // empuja alejando desde el punto más cercano al segmento (aprox con mid)
                Vector3 closest = col.ClosestPoint(mid);
                Vector3 away = mid - closest;

                float d = away.magnitude;
                if (d < 0.0001f) continue;

                total += away.normalized * (1f / (d + 0.001f));
                contributions++;
            }
        }

        if (contributions == 0) return Vector3.zero;
        return Vector3.ProjectOnPlane(total, Vector3.up);
    }

    private void ApplyStep(Step step)
    {
        if (step.disableLasers != null)
            for (int i = 0; i < step.disableLasers.Length; i++)
                if (step.disableLasers[i] != null) step.disableLasers[i].SetActive(false);

        if (step.enableLasers != null)
            for (int i = 0; i < step.enableLasers.Length; i++)
                if (step.enableLasers[i] != null) step.enableLasers[i].SetActive(true);
    }

    public void ResetSequence()
    {
        currentStep = 0;

        if (steps != null)
            for (int i = 0; i < steps.Length; i++)
                if (steps[i]?.expectedButton != null)
                    steps[i].expectedButton.ResetState();

        SetOnlyCurrentButtonPressable(0);
    }

    private void SetOnlyCurrentButtonPressable(int stepIndex)
    {
        if (steps == null) return;

        for (int i = 0; i < steps.Length; i++)
        {
            var btn = steps[i]?.expectedButton;
            if (btn != null) btn.AcceptPress = (i == stepIndex);
        }
    }
}
