using System;
using System.Collections;
using UnityEngine;

public class Level2Manager : MonoBehaviour
{
    [Serializable]
    public class Step
    {
        // Botón que toca pulsar en este paso
        public SequenceButton expectedButton;

        // Láseres que se apagan/encienden al completar el paso
        public Laser[] disableLasers;
    }

    [Header("References")]
    [SerializeField] private Transform ikTarget; // Objeto que tiene el componente FABRIKIK
    [SerializeField] private Transform endEffector; // Punta del brazo
    [SerializeField] private Step[] steps; // Secuencia de pasos (botón + desactivacicón de laseres)

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float pressDistance = 0.10f;
    [SerializeField] private float stopAfterPressSeconds = 1.0f; // Tiempo de espera tras pulsar un botón

    [Header("Avoidance (target)")]
    [SerializeField] private LayerMask obstacleMask; // Layer de obstáculos a evitar
    [SerializeField] private float avoidanceRadius = 0.5f; // Radio de evitación
    [SerializeField] private float avoidanceLookAhead = 1.0f; // Distancia para evitar obstáculos
    [SerializeField] private float avoidanceStrength = 2.0f; // Fuerza de desviación al evitar
    [SerializeField] private float emergencyPushStrength = 3.0f; // Fuerza de empuje al estar rozando/penetrando un obstáculo

    [Header("Avoidance (whole chain)")]
    [SerializeField] private float chainRadius = 0.5f; // Radio de la cápsula para evitar obstáculos con la cadena
    [SerializeField] private float chainPushStrength = 4.0f; // Fuerza de empuje para toda la cadena
    [SerializeField] private int bufSize = 32; // Tamaño del buffer reutilizable para OverlapCapsuleNonAlloc

    private FABRIKIK fabrik;
    private Transform runtimeTarget; // Target creado si FABRIK no tiene target o si el target es un joint (mala práctica)
    private int currentStep;
    private Coroutine runner;

    private Collider[] overlapBuffer;

    private void Awake()
    {
        // Buffer para evitar allocations en OverlapCapsuleNonAlloc
        overlapBuffer = new Collider[bufSize];

        // Obtiene referencia al componente FABRIKIK
        fabrik = ikTarget.GetComponent<FABRIKIK>();
        EnsureFabrikTarget();
    }

    private void Start()
    {
        // Coloca el target del fabrik en la posición inicial de la punta
        fabrik.target.position = endEffector.position;
        fabrik.isActive = true;

        // Inicia la secuencia de pasos
        runner = StartCoroutine(RunSequence());
    }

    private void EnsureFabrikTarget()
    {
        if (fabrik == null) return;

        if (runtimeTarget == null)
        {
            var go = new GameObject("FABRIK_RuntimeTarget");
            go.hideFlags = HideFlags.HideAndDontSave;
            runtimeTarget = go.transform;
        }

        // Coloca ese target donde está la punta para evitar saltos
        if (fabrik.joints != null && fabrik.joints.Length > 0 && fabrik.joints[fabrik.joints.Length - 1] != null)
            runtimeTarget.position = fabrik.joints[fabrik.joints.Length - 1].position;
        else
            runtimeTarget.position = fabrik.transform.position;

        fabrik.target = runtimeTarget;
    }

    private IEnumerator RunSequence()
    {
        // Lógica principal: para cada step -> ir al botón -> pulsar -> desactivar láseres -> siguiente
        while (currentStep < steps.Length)
        {
            var step = steps[currentStep];
            if (step == null || step.expectedButton == null) yield break;

            // Solo el botón del step actual puede aceptar pulsación
            SetOnlyCurrentButtonPressable(currentStep);

            Vector3 goal = step.expectedButton.GetPressWorldPosition();

            // Mueve el end effector hasta estar cerca del botón
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

        // Al terminar, desactiva la aceptacion de todos
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

        // Dirección base hacia el objetivo
        Vector3 steering = desiredDir;

        // 1) Si hay obstáculo delante, probamos varias direcciones y elegimos la que más despeje tenga sin alejarse del target.
        // Devuelve true si el SphereCast choca con un laser (cápsula)
        if (SphereCastHitsCapsule(from, desiredDir, castDist))
        {
            // Prueba varias direcciones de evasión y devuelve la mejor
            steering = ChooseBestAvoidanceDirection(from, desiredDir, castDist);
        }

        // 2) Si ya está rozando/penetrando un obstáculo, empuja hacia fuera
        Vector3 emergency = ComputeEmergencyPush(from);
        if (Vectors.SqrMagnitude(emergency) > 1e-8f)
            steering = Vectors.Normalize(steering + Vectors.Normalize(emergency) * emergencyPushStrength);

        // 3) Evitación para toda la cadena: revisa cada segmento del brazo y empuja fuera
        Vector3 chainPush = ComputeChainPush();
        if (Vectors.SqrMagnitude(chainPush) > 1e-8f)
            steering = Vectors.Normalize(steering + Vectors.Normalize(chainPush) * chainPushStrength);

        // Movimiento final del end effector
        float step = moveSpeed * Time.deltaTime;
        if (step > toGoalMag) step = toGoalMag;

        fabrik.target.position = from + steering * step;
    }

    // Devuelve true si el SphereCast choca con un laser (cápsula)
    private bool SphereCastHitsCapsule(Vector3 from, Vector3 dir, float dist)
    {
        if (Physics.SphereCast(from, avoidanceRadius, dir, out RaycastHit hit, dist, obstacleMask, QueryTriggerInteraction.Collide))
            return hit.collider is CapsuleCollider;
        return false;
    }

    // Prueba varias direcciones de evasión y devuelve la mejor
    private Vector3 ChooseBestAvoidanceDirection(Vector3 from, Vector3 desiredDir, float castDist)
    {
        Vector3 up = Vectors.Up();

        // Base lateral
        Vector3 left = Vectors.Normalize(Vectors.CrossProduct(up, desiredDir));
        if (Vectors.SqrMagnitude(left) < 1e-6f)
            left = Vectors.Normalize(Vectors.CrossProduct(Vectors.Forward(), desiredDir));
        Vector3 right = -left;

        Vector3 down = -up;

        // Candidatos a dirección: directa + laterales + arriba/abajo + diagonales
        Vector3[] candidates = new Vector3[]
        {
            desiredDir,
            // Laterales y verticales
            Vectors.Normalize(desiredDir + left  * avoidanceStrength),
            Vectors.Normalize(desiredDir + right * avoidanceStrength),
            Vectors.Normalize(desiredDir + up    * avoidanceStrength),
            Vectors.Normalize(desiredDir + down  * avoidanceStrength),

            // Diagonales
            Vectors.Normalize(desiredDir + (left  + up)   * (avoidanceStrength * 0.75f)),
            Vectors.Normalize(desiredDir + (right + up)   * (avoidanceStrength * 0.75f)),
            Vectors.Normalize(desiredDir + (left  + down) * (avoidanceStrength * 0.75f)),
            Vectors.Normalize(desiredDir + (right + down) * (avoidanceStrength * 0.75f)),
        };

        float bestScore = -1f;
        Vector3 bestDir = desiredDir;

        // Evalúa cada candidato
        for (int i = 0; i < candidates.Length; i++)
        {
            Vector3 c = candidates[i];
            float clearance = ClearanceCapsule(from, c, castDist);

            // Distancia libre * alineación con dirección deseada
            float align = Mathf.Max(0f, Vector3.Dot(c, desiredDir)); // 0..1
            float score = clearance * (0.35f + 0.65f * align);

            if (score > bestScore)
            {
                bestScore = score;
                bestDir = c;
            }
        }

        // Devuelve la mejor dirección encontrada
        return bestDir;
    }

    // Si choca con una cápsula, devuelve distancia al hit, si no dist
    private float ClearanceCapsule(Vector3 from, Vector3 dir, float dist)
    {
        if (Physics.SphereCast(from, avoidanceRadius, dir, out RaycastHit h, dist, obstacleMask, QueryTriggerInteraction.Collide)
            && h.collider is CapsuleCollider)
            return h.distance;

        return dist;
    }

    // Calcula un vector de empuje fuera de obstáculos cercanos al target
    private Vector3 ComputeEmergencyPush(Vector3 pos)
    {
        // Busca obstáculos cercanos
        int hits = Physics.OverlapSphereNonAlloc(pos, avoidanceRadius * 1.2f, overlapBuffer, obstacleMask, QueryTriggerInteraction.Collide);
        if (hits <= 0) return Vector3.zero;

        Vector3 push = new Vector3(0, 0, 0);

        // Para cada obstáculo cercano, calcula empuje fuera
        for (int i = 0; i < hits; i++)
        {
            // Solo cápsulas (láseres)
            var col = overlapBuffer[i] as CapsuleCollider;
            if (col == null) continue;

            Vector3 closest = col.ClosestPoint(pos);
            Vector3 away = pos - closest;

            float d = Vectors.Magnitude(away);
            if (d < 0.0001f) continue;

            // Empuja fuera, más cuanto más cerca esté
            push += Vectors.Normalize(away) * (1f / (d + 0.001f));
        }

        // Devuelve vector de empuje acumulado
        return push;
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

            // Cápsula del segmento (a->b). Si intersecta obstáculos, empuja fuera.
            int hits = Physics.OverlapCapsuleNonAlloc(a, b, chainRadius, overlapBuffer, obstacleMask, QueryTriggerInteraction.Collide);
            if (hits <= 0) continue;

            Vector3 mid = (a + b) * 0.5f;

            for (int h = 0; h < hits; h++)
            {
                // Solo cápsulas (láseres)
                var col = overlapBuffer[h] as CapsuleCollider;
                if (col == null) continue;

                Vector3 closest = col.ClosestPoint(mid);
                Vector3 away = mid - closest;

                float d = Vectors.Magnitude(away);
                if (d < 0.0001f) continue;

                total += Vectors.Normalize(away) * (1f / (d + 0.001f));
                contributions++;
            }
        }

        if (contributions == 0) return Vector3.zero;

        // Antes se proyectaba en plano horizontal; ahora dejamos componente vertical
        return total;
    }

    private void ApplyStep(Step step)
    {
        // Aplica cambios del puzzle (láseres) al completar el step
        if (step.disableLasers != null)
            for (int i = 0; i < step.disableLasers.Length; i++)
                if (step.disableLasers[i] != null) step.disableLasers[i].SetActive(false);
    }

    private void SetOnlyCurrentButtonPressable(int stepIndex)
    {
        // Solo el botón del stepIndex acepta pulsación, el resto se bloquean
        if (steps == null) return;

        for (int i = 0; i < steps.Length; i++)
        {
            var btn = steps[i]?.expectedButton;
            if (btn != null) btn.AcceptPress = (i == stepIndex);
        }
    }

    void OnGUI()
    {
        // UI debug rápida (solo lectura): estado FABRIK, step actual y datos del botón objetivo
        GUILayout.BeginArea(new Rect(12, 12, 360, 210), GUI.skin.box);

        GUILayout.Label("<b>LEVEL 2 - Debug IK</b>", new GUIStyle(GUI.skin.label) { richText = true });
        GUILayout.Space(6);

        if (fabrik == null)
        {
            GUILayout.Label("FABRIK: (sin referencia)");
            GUILayout.EndArea();
            return;
        }

        int totalSteps = (steps != null) ? steps.Length : 0;
        bool done = (totalSteps == 0) || (currentStep >= totalSteps);

        GUILayout.Label("FABRIK activo: " + fabrik.isActive);

        GUILayout.Label("FABRIK iter/frame: " + fabrik.LastIterations + " / " + fabrik.maxIterations);
        GUILayout.Label("FABRIK error: " + fabrik.LastDistanceToTarget.ToString("F4"));

        if (!done)
            GUILayout.Label("Step: " + (currentStep + 1) + " / " + totalSteps);
        else
            GUILayout.Label("Step: DONE (" + totalSteps + " / " + totalSteps + ")");

        if (fabrik.target != null)
            GUILayout.Label("Target pos: " + fabrik.target.position.ToString("F3"));

        if (endEffector != null)
            GUILayout.Label("EndEffector pos: " + endEffector.position.ToString("F3"));

        if (!done && steps[currentStep] != null && steps[currentStep].expectedButton != null && endEffector != null)
        {
            Vector3 goal = steps[currentStep].expectedButton.GetPressWorldPosition();
            float dist = Vector3.Distance(endEffector.position, goal);

            GUILayout.Space(8);
            GUILayout.Label("Goal (botón) pos: " + goal.ToString("F3"));
            GUILayout.Label("Distancia al botón: " + dist.ToString("F4"));
            GUILayout.Label("AcceptPress: " + steps[currentStep].expectedButton.AcceptPress);
        }
        else
        {
            GUILayout.Space(8);
            GUILayout.Label("Goal: DONE");
        }

        GUILayout.EndArea();
    }
}
