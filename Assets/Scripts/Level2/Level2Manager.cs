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
    [SerializeField] private Transform ikTarget;
    [SerializeField] private Transform endEffector;
    [SerializeField] private Step[] steps;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 0.35f;
    [SerializeField] private float pressDistance = 0.10f;
    [SerializeField] private float stopAfterPressSeconds = 1.0f;

    [Header("Avoidance (target)")]
    [SerializeField] private LayerMask obstacleMask;
    [SerializeField] private float avoidanceRadius = 0.10f;
    [SerializeField] private float avoidanceLookAhead = 0.60f;
    [SerializeField] private float avoidanceStrength = 2.0f;
    [SerializeField] private float emergencyPushStrength = 3.0f;

    [Header("Avoidance (whole chain)")]
    [SerializeField] private float chainRadius = 0.08f;
    [SerializeField] private float chainPushStrength = 4.0f;
    [SerializeField] private int overlapBufferSize = 32;

    private FABRIKIK fabrik;
    private int currentStep;
    private Coroutine runner;

    private Collider[] overlapBuffer;

    private void Awake()
    {
        overlapBuffer = new Collider[overlapBufferSize];

        fabrik = ikTarget.GetComponent<FABRIKIK>();

        // El target del FABRIK será ESTE GameObject (no hay que asignar nada extra)
        fabrik.target = transform;

        ResetSequence();
    }

    private void Start()
    {
        // Evitar salto: el target empieza donde está la punta
        fabrik.target.position = endEffector.position;
        fabrik.isActive = true;

        runner = StartCoroutine(RunSequence());
    }

    private IEnumerator RunSequence()
    {
        while (currentStep < steps.Length)
        {
            var step = steps[currentStep];

            SetOnlyCurrentButtonPressable(currentStep);

            Vector3 goal = step.expectedButton.GetPressWorldPosition();

            while (Vectors.Distance(endEffector.position, goal) > pressDistance)
            {
                MoveFabrikTargetTowards(goal);
                yield return null;
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

        if (Vectors.SqrMagnitude(toGoal) < 1e-8f) return;

        float toGoalMag = Vectors.Magnitude(toGoal);
        Vector3 desiredDir = Vectors.Normalize(toGoal);
        float castDist = MathLite.Min(avoidanceLookAhead, toGoalMag);

        Vector3 steering = desiredDir;

        if (Physics.SphereCast(from, avoidanceRadius, desiredDir, out _, castDist, obstacleMask, QueryTriggerInteraction.Collide))
        {
            Vector3 left = Vectors.Normalize(Vectors.CrossProduct(Vectors.Up(), desiredDir));
            if (Vectors.SqrMagnitude(left) < 1e-6f)
                left = Vectors.Normalize(Vectors.CrossProduct(Vectors.Forward(), desiredDir));

            Vector3 right = -left;

            float leftClear = Clearance(from, left, castDist);
            float rightClear = Clearance(from, right, castDist);

            Vector3 side = (leftClear >= rightClear) ? left : right;
            steering = Vectors.Normalize(desiredDir + side * avoidanceStrength);
        }

        Vector3 emergency = ComputeEmergencyPush(from);
        if (Vectors.SqrMagnitude(emergency) > 1e-8f)
            steering = Vectors.Normalize(steering + Vectors.Normalize(emergency) * emergencyPushStrength);

        Vector3 chainPush = ComputeChainPush();
        if (Vectors.SqrMagnitude(chainPush) > 1e-8f)
            steering = Vectors.Normalize(steering + Vectors.Normalize(chainPush) * chainPushStrength);

        float step = moveSpeed * Time.deltaTime;
        if (step > toGoalMag) step = toGoalMag;

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

            Vector3 closest = col.ClosestPoint(pos);
            Vector3 away = pos - closest;

            float d = Vectors.Magnitude(away);
            if (d < 0.0001f) continue;

            push += Vectors.Normalize(away) * (1f / (d + 0.001f));
        }

        return Vectors.ProjectOnPlane(push, Vectors.Up());
    }

    private Vector3 ComputeChainPush()
    {
        var joints = fabrik.joints;

        Vector3 total = Vector3.zero;
        int contributions = 0;

        for (int i = 0; i < joints.Length - 1; i++)
        {
            Vector3 a = joints[i].position;
            Vector3 b = joints[i + 1].position;

            int hits = Physics.OverlapCapsuleNonAlloc(a, b, chainRadius, overlapBuffer, obstacleMask, QueryTriggerInteraction.Collide);
            if (hits <= 0) continue;

            Vector3 mid = (a + b) * 0.5f;

            for (int h = 0; h < hits; h++)
            {
                Collider col = overlapBuffer[h];

                Vector3 closest = col.ClosestPoint(mid);
                Vector3 away = mid - closest;

                float d = Vectors.Magnitude(away);
                if (d < 0.0001f) continue;

                total += Vectors.Normalize(away) * (1f / (d + 0.001f));
                contributions++;
            }
        }

        if (contributions == 0) return Vector3.zero;
        return Vectors.ProjectOnPlane(total, Vectors.Up());
    }

    private void ApplyStep(Step step)
    {
        for (int i = 0; i < step.disableLasers.Length; i++)
            step.disableLasers[i].SetActive(false);

        for (int i = 0; i < step.enableLasers.Length; i++)
            step.enableLasers[i].SetActive(true);
    }

    public void ResetSequence()
    {
        currentStep = 0;

        for (int i = 0; i < steps.Length; i++)
            steps[i].expectedButton.ResetState();

        SetOnlyCurrentButtonPressable(0);
    }

    private void SetOnlyCurrentButtonPressable(int stepIndex)
    {
        for (int i = 0; i < steps.Length; i++)
            steps[i].expectedButton.AcceptPress = (i == stepIndex);
    }
}
