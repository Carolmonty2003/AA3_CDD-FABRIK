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
        SetDesiredTargetForStep(currentStep, snap: false);

        Log($"Initial currentStep={currentStep}");
    }

    void Update()
    {
        if (!securityEnabled) return;

        if (driveIKTarget) DriveIKTarget();

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

    void SetDesiredTargetForStep(int step, bool snap)
    {
        _desiredTargetPos = GetStepTargetPos(step);
        if (snap && _runtimeTarget != null) _runtimeTarget.position = _desiredTargetPos;
        Log($"SetDesiredTarget step={step} pos={_desiredTargetPos}");
    }

    Vector3 GetStepTargetPos(int step)
    {
        if (buttons == null || buttons.Length == 0) return _runtimeTarget != null ? _runtimeTarget.position : transform.position;
        int idx = Mathf.Clamp(step, 0, buttons.Length - 1);
        return buttons[idx].GetPressWorldPos();
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

        SetDesiredTargetForStep(currentStep, snap: false);
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
        Log("ResetSequence -> currentStep=0");
        ApplyPattern(currentStep);
        SetDesiredTargetForStep(currentStep, snap: false);
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

        GUILayout.BeginArea(new Rect(10, 10, 520, 170), GUI.skin.box);
        GUILayout.Label("Level2 DEBUG");
        GUILayout.Label($"securityEnabled: {securityEnabled} | paused: {_paused} | transitioning: {_transitioning}");
        GUILayout.Label($"currentStep: {currentStep}/{(buttons != null ? buttons.Length : 0)}");
        GUILayout.Label($"autoPressByDistance: {autoPressByDistance} | distToCurrent: {_lastDist:0.000} | radius: {autoPressRadius:0.000}");
        GUILayout.Label($"Last: {_lastEvent}");
        GUILayout.Label($"Last age: {(Time.time - _lastEventTime):0.00}s");
        GUILayout.EndArea();
    }
}
