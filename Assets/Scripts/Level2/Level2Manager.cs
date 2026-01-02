using System;
using System.Collections;
using UnityEngine;

public class Level2Manager : MonoBehaviour
{
    [Serializable]
    public class StepPattern
    {
        public string stepName;
        public Laser[] lasersOff;
    }

    [Header("Buttons (in order)")]
    public SequenceButton[] buttons;

    [Header("Lasers")]
    public Laser[] allLasers;
    public StepPattern[] patterns;

    [Header("IK (FABRIK)")]
    public FABRIKIK ik;
    public bool driveIKTarget = true;

    [Tooltip("Pausa tras pulsar un botón correcto")]
    public float pauseAfterPress = 1f;

    [Tooltip("Movimiento del target: SmoothDamp time")]
    public float targetSmoothTime = 0.12f;

    [Header("Avoidance (PRE-collision)")]
    public bool avoidLasers = true;

    [Tooltip("Pon aquí SOLO la layer de los láseres (recomendado: crea layer 'Laser' y así no te estorba nada más).")]
    public LayerMask laserLayerMask = ~0;

    [Tooltip("Radio del SphereCast para considerar el grosor del brazo/target frente al láser.")]
    public float steerSphereRadius = 0.05f;

    [Tooltip("Cuánto nos desviamos alrededor del punto de impacto para generar un detour.")]
    public float detourDistance = 0.22f;

    [Tooltip("Cuántas direcciones probamos alrededor del impacto (8 suele ir bien).")]
    [Range(4, 16)]
    public int detourSamples = 8;

    [Tooltip("Tiempo mínimo manteniendo un detour para evitar temblores.")]
    public float detourHoldTime = 0.35f;

    [Tooltip("A qué distancia del botón dejamos de esquivar (para poder presionar sin “orbitar”).")]
    public float nearGoalRadius = 0.14f;

    [Header("Soft repulsion (helps find holes)")]
    [Tooltip("Radio de influencia para repulsión suave (ayuda a meterse por huecos).")]
    public float influenceRadius = 0.35f;

    [Tooltip("Fuerza de repulsión suave (sube si sigue rozando).")]
    public float repulsionStrength = 0.9f;

    [Tooltip("Distancia de paso cuando estamos esquivando (cómo de rápido “busca camino”).")]
    public float steerStepDistance = 0.22f;

    [Header("Backup Press (sin física)")]
    public bool autoPressByDistance = true;
    public float autoPressRadius = 0.06f;
    public float autoPressCooldown = 0.3f;

    [Header("Arm Detection")]
    public LayerMask armLayerMask;

    [Header("Rules")]
    public bool resetOnLaserHit = true;

    [Header("DEBUG")]
    public bool debugLogs = true;
    public bool debugOnScreen = true;

    [Header("State (read only)")]
    public int currentStep = 0;
    public bool securityEnabled = true;

    Transform _runtimeTarget;
    Vector3 _desiredTargetPos;
    Vector3 _targetVel;

    bool _paused;
    bool _transitioning;
    Coroutine _advanceRoutine;

    float _nextAutoPressTime;
    float _lastDist;
    string _lastEvent = "";
    float _lastEventTime;

    // avoidance state
    Vector3 _detourPos;
    float _detourUntil;

    void Log(string msg)
    {
        _lastEvent = msg;
        _lastEventTime = Time.time;
        if (debugLogs) Debug.Log($"[Level2][t={Time.time:0.00} f={Time.frameCount}] {msg}", this);
    }

    void Awake()
    {
        Log("Awake()");

        // Bind buttons
        if (buttons != null)
        {
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] == null)
                {
                    Log($"buttons[{i}] = NULL (revisa el array)");
                    continue;
                }
                buttons[i].manager = this;
                buttons[i].orderIndex = i;
                buttons[i].armLayerMask = armLayerMask;
                Log($"Bind Button '{buttons[i].name}' index={i}");
            }
        }

        // Bind lasers
        if (allLasers != null)
        {
            for (int i = 0; i < allLasers.Length; i++)
            {
                if (allLasers[i] == null) continue;
                allLasers[i].manager = this;
                allLasers[i].armLayerMask = armLayerMask;
            }
            Log($"Bind Lasers count={allLasers.Length}");
        }
    }

    void Start()
    {
        Log("Start()");

        if (ik == null) ik = FindObjectOfType<FABRIKIK>();
        if (ik == null) Log("ERROR: No encuentro FABRIKIK en escena");

        if (allLasers == null || allLasers.Length == 0)
        {
            allLasers = FindObjectsOfType<Laser>(true);
            Log($"Auto-found Lasers count={allLasers.Length}");
        }

        EnsureIKTarget();

        currentStep = 0;
        ApplyPattern(currentStep);

        // primer objetivo
        _desiredTargetPos = GetStepTargetPos(currentStep);

        Log($"Initial currentStep={currentStep}");
    }

    void Update()
    {
        if (!securityEnabled) return;

        // 1) recalcula el target cada frame
        UpdateDesiredTargetWithAvoidance();

        // 2) mueve el target
        if (driveIKTarget) DriveIKTarget();

        // 3) autoprensado
        if (autoPressByDistance) AutoPressCheck();
    }

    // ---------- IK target ----------
    void EnsureIKTarget()
    {
        if (ik == null) return;

        if (ik.target == null)
        {
            var go = new GameObject("Level2_IK_Target_Runtime");
            go.hideFlags = HideFlags.HideInHierarchy;
            _runtimeTarget = go.transform;

            var end = GetEndEffector();
            _runtimeTarget.position = end != null ? end.position : transform.position;

            ik.target = _runtimeTarget;
            Log("Created runtime IK target (ik.target was null)");
        }
        else
        {
            _runtimeTarget = ik.target;
            Log("Using existing ik.target");
        }
    }

    Transform GetEndEffector()
    {
        if (ik == null || ik.joints == null || ik.joints.Length == 0) return null;
        return ik.joints[ik.joints.Length - 1];
    }

    void DriveIKTarget()
    {
        if (_paused || _transitioning) return;
        if (_runtimeTarget == null) return;

        _runtimeTarget.position = Vector3.SmoothDamp(
            _runtimeTarget.position,
            _desiredTargetPos,
            ref _targetVel,
            targetSmoothTime
        );
    }

    Vector3 GetStepTargetPos(int step)
    {
        if (buttons == null || buttons.Length == 0)
            return _runtimeTarget != null ? _runtimeTarget.position : transform.position;

        int idx = Mathf.Clamp(step, 0, buttons.Length - 1);
        return buttons[idx].GetPressWorldPos();
    }

    void UpdateDesiredTargetWithAvoidance()
    {
        if (_paused || _transitioning) return;

        Vector3 finalGoal = GetStepTargetPos(currentStep);
        Transform end = GetEndEffector();
        Vector3 endPos = end != null ? end.position : (_runtimeTarget != null ? _runtimeTarget.position : transform.position);

        // si estamos muy cerca del botón, NO esquives: ve directo para poder pulsar.
        float distToGoal = Vector3.Distance(endPos, finalGoal);
        if (!avoidLasers || distToGoal <= nearGoalRadius)
        {
            _desiredTargetPos = finalGoal;
            return;
        }

        // si estamos manteniendo un detour (anti-jitter)
        if (Time.time < _detourUntil)
        {
            _desiredTargetPos = _detourPos;
            return;
        }

        // 1) si el camino directo está bloqueado -> crea detour
        if (SphereCastToLaser(endPos, finalGoal, steerSphereRadius, out RaycastHit hit, out Laser hitLaser))
        {
            Vector3 detour = FindBestDetour(endPos, finalGoal, hit.point, (finalGoal - endPos).normalized);
            if (detour != Vector3.zero)
            {
                _detourPos = detour;
                _detourUntil = Time.time + detourHoldTime;
                _desiredTargetPos = _detourPos;
                return;
            }
        }

        // 2) si no hay bloqueo directo, aplica repulsión suave para “buscar huecos”
        Vector3 toGoal = finalGoal - endPos;
        Vector3 force = toGoal.sqrMagnitude > 1e-6f ? toGoal.normalized : Vector3.zero;

        Vector3 repulsion = ComputeRepulsion(endPos);
        force += repulsion * repulsionStrength;

        if (force.sqrMagnitude < 1e-6f)
        {
            _desiredTargetPos = finalGoal;
            return;
        }

        float step = Mathf.Min(steerStepDistance, distToGoal);
        _desiredTargetPos = endPos + force.normalized * step;
    }

    Vector3 ComputeRepulsion(Vector3 pos)
    {
        Vector3 rep = Vector3.zero;

        Collider[] cols = Physics.OverlapSphere(
            pos,
            influenceRadius,
            laserLayerMask,
            QueryTriggerInteraction.Collide
        );

        for (int i = 0; i < cols.Length; i++)
        {
            var laser = cols[i].GetComponentInParent<Laser>();
            if (laser == null || !laser.isOn) continue;

            Vector3 cp = cols[i].ClosestPoint(pos);
            Vector3 v = pos - cp;
            float d = v.magnitude;

            if (d < 1e-4f) continue;
            if (d > influenceRadius) continue;

            float t = 1f - (d / influenceRadius);   // 0..1
            rep += (v / d) * (t * t);                // cuadrática (suave)
        }

        return rep;
    }

    bool SphereCastToLaser(Vector3 from, Vector3 to, float radius, out RaycastHit bestHit, out Laser bestLaser)
    {
        bestHit = default;
        bestLaser = null;

        Vector3 dir = to - from;
        float dist = dir.magnitude;
        if (dist < 1e-5f) return false;
        dir /= dist;

        RaycastHit[] hits = Physics.SphereCastAll(
            from,
            radius,
            dir,
            dist,
            laserLayerMask,
            QueryTriggerInteraction.Collide
        );

        float best = float.PositiveInfinity;

        for (int i = 0; i < hits.Length; i++)
        {
            var laser = hits[i].collider.GetComponentInParent<Laser>();
            if (laser == null || !laser.isOn) continue;

            if (hits[i].distance < best)
            {
                best = hits[i].distance;
                bestHit = hits[i];
                bestLaser = laser;
            }
        }

        return bestLaser != null;
    }

    Vector3 FindBestDetour(Vector3 from, Vector3 goal, Vector3 hitPoint, Vector3 dirToGoal)
    {
        // base ortonormal en el plano perpendicular a dirToGoal
        Vector3 right = Vector3.Cross(Vector3.up, dirToGoal);
        if (right.sqrMagnitude < 1e-6f) right = Vector3.Cross(Vector3.forward, dirToGoal);
        right.Normalize();

        Vector3 up2 = Vector3.Cross(dirToGoal, right);
        up2.Normalize();

        Vector3 best = Vector3.zero;
        float bestScore = float.PositiveInfinity;

        for (int i = 0; i < detourSamples; i++)
        {
            float ang = (i / (float)detourSamples) * Mathf.PI * 2f;
            Vector3 offset = (Mathf.Cos(ang) * right + Mathf.Sin(ang) * up2) * detourDistance;
            Vector3 cand = hitPoint + offset;

            // scoring: penaliza si hay láser en los tramos
            float score = 0f;

            if (SphereCastToLaser(from, cand, steerSphereRadius, out _, out _)) score += 1000f;
            if (SphereCastToLaser(cand, goal, steerSphereRadius, out _, out _)) score += 600f;

            score += Vector3.Distance(from, cand);
            score += Vector3.Distance(cand, goal) * 0.35f;

            if (score < bestScore)
            {
                bestScore = score;
                best = cand;
            }
        }

        // si todo era malísimo, devuelve zero para que use repulsión
        if (bestScore >= 900f) return Vector3.zero;
        return best;
    }

    // ---------- Press flow ----------
    public bool CanPressButton(SequenceButton b) => securityEnabled && !_transitioning;

    public void OnButtonPressed(SequenceButton button)
    {
        if (!securityEnabled || button == null) return;
        if (_transitioning) { Log("OnButtonPressed ignored (transitioning)"); return; }

        Log($"OnButtonPressed '{button.name}' pressedIndex={button.orderIndex} expected={currentStep}");

        if (button.orderIndex == currentStep)
        {
            if (_advanceRoutine != null) StopCoroutine(_advanceRoutine);
            _advanceRoutine = StartCoroutine(AdvanceAfterCorrectPress());
        }
        else
        {
            Log("WRONG BUTTON -> ResetSequence()");
            ResetSequence();
        }
    }

    IEnumerator AdvanceAfterCorrectPress()
    {
        _transitioning = true;

        // Congelar target donde está
        _paused = true;
        if (_runtimeTarget != null) _desiredTargetPos = _runtimeTarget.position;

        currentStep++;
        Log($"CORRECT -> currentStep now {currentStep}");

        if (buttons != null && currentStep >= buttons.Length)
        {
            Log("All buttons done -> DisableSecurity()");
            DisableSecurity();
            yield break;
        }

        ApplyPattern(currentStep);

        yield return new WaitForSeconds(pauseAfterPress);

        _paused = false;
        _transitioning = false;
        _advanceRoutine = null;
    }

    void AutoPressCheck()
    {
        if (_transitioning) return;
        if (buttons == null || buttons.Length == 0) return;
        if (currentStep < 0 || currentStep >= buttons.Length) return;

        var end = GetEndEffector();
        if (end == null) return;

        var b = buttons[currentStep];
        if (b == null) return;

        float d = Vector3.Distance(end.position, b.GetPressWorldPos());
        _lastDist = d;

        if (Time.time < _nextAutoPressTime) return;

        if (d <= autoPressRadius)
        {
            Log($"AUTO-PRESS by distance on '{b.name}' dist={d:0.000} radius={autoPressRadius:0.000}");
            _nextAutoPressTime = Time.time + autoPressCooldown;
            OnButtonPressed(b);
        }
    }

    // ---------- Lasers ----------
    public void OnLaserHit(Laser laser)
    {
        if (!securityEnabled || !resetOnLaserHit) return;
        Log($"OnLaserHit '{laser.name}' -> ResetSequence()");
        ResetSequence();
    }

    void ResetSequence()
    {
        if (_advanceRoutine != null)
        {
            StopCoroutine(_advanceRoutine);
            _advanceRoutine = null;
        }

        _transitioning = false;
        _paused = false;

        currentStep = 0;
        _detourUntil = 0f;

        Log("ResetSequence -> currentStep=0");
        ApplyPattern(currentStep);
    }

    void DisableSecurity()
    {
        securityEnabled = false;
        _paused = true;
        _transitioning = false;

        if (ik != null) ik.isActive = false;

        if (allLasers != null)
        {
            for (int i = 0; i < allLasers.Length; i++)
                if (allLasers[i] != null) allLasers[i].SetOn(false);
        }
    }

    void ApplyPattern(int step)
    {
        // enciende todos
        if (allLasers != null)
        {
            for (int i = 0; i < allLasers.Length; i++)
                if (allLasers[i] != null) allLasers[i].SetOn(true);
        }

        if (patterns == null || patterns.Length == 0)
        {
            Log($"ApplyPattern step={step} -> patterns EMPTY");
            return;
        }

        int idx = Mathf.Clamp(step, 0, patterns.Length - 1);
        var p = patterns[idx];
        int offCount = (p != null && p.lasersOff != null) ? p.lasersOff.Length : 0;

        Log($"ApplyPattern step={step} patternIdx={idx} name='{p?.stepName}' lasersOff={offCount}");

        if (p == null || p.lasersOff == null) return;

        for (int i = 0; i < p.lasersOff.Length; i++)
            if (p.lasersOff[i] != null) p.lasersOff[i].SetOn(false);
    }

    void OnGUI()
    {
        if (!debugOnScreen) return;

        GUILayout.BeginArea(new Rect(10, 10, 560, 190), GUI.skin.box);
        GUILayout.Label("Level2 DEBUG");
        GUILayout.Label($"securityEnabled: {securityEnabled} | paused: {_paused} | transitioning: {_transitioning}");
        GUILayout.Label($"currentStep: {currentStep}/{(buttons != null ? buttons.Length : 0)}");
        GUILayout.Label($"avoidLasers: {avoidLasers} | detourActive: {(Time.time < _detourUntil)}");
        GUILayout.Label($"autoPressByDistance: {autoPressByDistance} | distToCurrent: {_lastDist:0.000} | radius: {autoPressRadius:0.000}");
        GUILayout.Label($"Last: {_lastEvent}");
        GUILayout.EndArea();
    }
}
