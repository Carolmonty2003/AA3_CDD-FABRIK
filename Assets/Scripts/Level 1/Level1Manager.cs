using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manager del nivel: controla el “flujo” (ir a core, agarrar, transportar, depositar).
/// Usa waypoints para el target en el retorno al depósito (subir -> horizontal -> bajar),
/// y puede elevar la altura de transporte si detecta obstáculo en el tramo horizontal.
/// </summary>
public class Level1Manager : MonoBehaviour
{
    [Header("Referencias del Brazo")]
    public CCDIK ccdSolver;

    [Header("Punto de Depósito")]
    public Transform depositPoint;
    public bool autoCreateDepositPoint = true;

    [Header("Target")]
    public Transform target;
    public bool autoCreateTarget = true;

    [Header("Núcleos de Datos")]
    public DataCore[] dataCores;
    public int coresCollected = 0;
    public int coresDeposited = 0;
    public int totalCores = 0;

    [Header("Control del Flujo")]
    public bool autoTargetNextCore = true;

    // Estados principales del “ciclo” de recolección.
    private enum State
    {
        APPROACHING_CORE,
        WAITING_TO_GRAB,
        GRABBING,
        LIFTING_CORE,
        CARRYING_CORE,
        LOWERING_CORE,
        DEPOSITING,
        LEVEL_COMPLETE
    }

    private State currentState = State.APPROACHING_CORE;
    private DataCore currentCore = null;

    [Header("Configuración de Movimiento")]
    public float targetMoveSpeed = 3.0f;
    public float pauseBeforeGrab = 0.3f;
    public float grabDuration = 0.5f;

    [Tooltip("Altura local extra al depositar (por encima del depósito).")]
    public float liftHeight = 0.3f;

    public float pauseBeforeDeposit = 0.3f;
    public float depositDisplayTime = 1.0f;

    [Header("Configuración Técnica")]
    public float distanceThreshold = 0.15f;
    public float depositDistance = 0.25f;

    [Header("Reach / Safety")]
    public bool clampDestinationsToReach = true;

    [Header("Ruta de vuelta al depósito")]
    public float carryHeight = 1.2f;            // altura “segura” para ir en horizontal
    public float carryHeightStepUp = 0.25f;     // cuánto sube si detecta obstáculo
    public int carryHeightMaxTries = 10;        // intentos de elevar
    public float pathArriveThreshold = 0.05f;

    [Header("Obstacle check (para la ruta del target)")]
    public bool avoidObstaclesForTarget = true;
    public LayerMask obstacleLayerForTarget;
    public float targetAvoidRadius = 0.20f;

    [Header("Estado del Nivel")]
    public bool levelComplete = false;
    public float elapsedTime = 0f;

    private bool isMovingTarget = false;

    // Waypoints del target (lista + índice).
    private readonly List<Vector3> path = new List<Vector3>();
    private int pathIndex = 0;

    // Puntos usados en checks de llegada al depósito.
    private Vector3 depositApproachPoint;
    private Vector3 depositLowerPoint;

    void Start()
    {
        Debug.Log("=== NIVEL 1 INICIANDO (CCD + Waypoints) ===");

        if (dataCores == null || dataCores.Length == 0)
            dataCores = FindObjectsOfType<DataCore>();

        totalCores = dataCores.Length;
        coresCollected = 0;
        coresDeposited = 0;

        if (ccdSolver == null)
            ccdSolver = GetComponent<CCDIK>();

        if (depositPoint == null && autoCreateDepositPoint)
            CreateDepositPoint();

        if (target == null && autoCreateTarget)
            CreateTarget();

        if (ccdSolver != null && target != null)
            ccdSolver.target = target;

        // Si no se configuró obstacleLayerForTarget, se copia del CCD para consistencia.
        if (avoidObstaclesForTarget && obstacleLayerForTarget.value == 0 && ccdSolver != null)
            obstacleLayerForTarget = ccdSolver.obstacleLayer;

        if (autoTargetNextCore)
        {
            currentState = State.APPROACHING_CORE;
            MoveToNextCore();
        }

        Debug.Log($"=== NIVEL 1 LISTO: {totalCores} núcleos ===");
    }

    void Update()
    {
        if (levelComplete) return;

        elapsedTime += Time.deltaTime;
        UpdateTargetMovement();
        UpdateStateMachine();
    }

    /// <summary>
    /// Movimiento del target siguiendo los waypoints (MoveTowards).
    /// </summary>
    void UpdateTargetMovement()
    {
        if (!isMovingTarget || target == null) return;
        if (path.Count == 0) { isMovingTarget = false; return; }

        Vector3 dest = path[Mathf.Clamp(pathIndex, 0, path.Count - 1)];
        float distance = Vector3.Distance(target.position, dest);

        if (distance > pathArriveThreshold)
        {
            target.position = Vectors.MoveTowards(target.position, dest, targetMoveSpeed * Time.deltaTime);
        }
        else
        {
            target.position = dest;
            pathIndex++;

            if (pathIndex >= path.Count)
                isMovingTarget = false;
        }
    }

    /// <summary>
    /// Máquina de estados mínima: solo chequea los estados donde se espera “llegar” a algo.
    /// </summary>
    void UpdateStateMachine()
    {
        switch (currentState)
        {
            case State.APPROACHING_CORE:
                CheckIfReachedCore();
                break;

            case State.CARRYING_CORE:
                CheckIfReachedDeposit();
                break;

            case State.LOWERING_CORE:
                CheckIfFinishedLowering();
                break;
        }
    }

    // ---------- Reach helpers ----------
    /// <summary>
    /// Si está activado, clampa destinos a un radio máximo (alcance del brazo - epsilon).
    /// Evita que el target pida posiciones imposibles.
    /// </summary>
    Vector3 ClampToArmReach(Vector3 p)
    {
        if (!clampDestinationsToReach) return p;
        if (ccdSolver == null || ccdSolver.joints == null || ccdSolver.joints.Length < 2) return p;

        Vector3 basePos = ccdSolver.joints[0].position;
        float max = Mathf.Max(0f, ccdSolver.totalReach - ccdSolver.reachEpsilon);

        Vector3 v = p - basePos;
        float dist = v.magnitude;

        if (dist > max && dist > 1e-6f)
            p = basePos + (v / dist) * max;

        return p;
    }

    // ---------- Path helpers ----------
    void ClearPath()
    {
        path.Clear();
        pathIndex = 0;
    }

    /// <summary>
    /// Define el path actual del target con una lista de puntos (waypoints).
    /// </summary>
    void SetPath(params Vector3[] points)
    {
        ClearPath();
        for (int i = 0; i < points.Length; i++)
            path.Add(ClampToArmReach(points[i]));

        isMovingTarget = true;
    }

    /// <summary>
    /// Comprueba si el tramo a->b está bloqueado (SphereCast). Se usa para elegir altura de transporte.
    /// </summary>
    bool SegmentBlocked(Vector3 a, Vector3 b)
    {
        if (!avoidObstaclesForTarget) return false;

        Vector3 d = b - a;
        float dist = d.magnitude;
        if (dist < 1e-4f) return false;
        d /= dist;

        return Physics.SphereCast(a, targetAvoidRadius, d, out _, dist, obstacleLayerForTarget);
    }

    /// <summary>
    /// Calcula una Y de transporte que permita ir horizontal sin chocar:
    /// si el segmento está bloqueado, sube la altura y reintenta.
    /// </summary>
    float ComputeCarryYForReturn(Vector3 start, Vector3 endXZ, float initialY)
    {
        float y = initialY;

        for (int tries = 0; tries < carryHeightMaxTries; tries++)
        {
            Vector3 a = new Vector3(start.x, y, start.z);
            Vector3 b = new Vector3(endXZ.x, y, endXZ.z);

            if (!SegmentBlocked(a, b))
                return y;

            y += carryHeightStepUp;
        }

        return y;
    }

    // ---------- Flow ----------
    void MoveToNextCore()
    {
        if (currentCore == null)
            currentCore = FindNextUncollectedCore();

        if (target == null || currentCore == null)
        {
            Debug.LogWarning("⚠ No hay núcleo disponible");
            return;
        }

        // Camino al core: directo.
        SetPath(currentCore.transform.position);
    }

    void MoveToDeposit()
    {
        if (target == null || depositPoint == null) return;

        // Punto “sobre el depósito” para llegar con margen.
        depositApproachPoint = depositPoint.position + Vector3.up * liftHeight;
        depositApproachPoint = ClampToArmReach(depositApproachPoint);

        // Ruta: subir -> horizontal -> (si hace falta) bajar hacia el punto sobre depósito.
        Vector3 start = target.position;
        Vector3 endXZ = depositApproachPoint;

        float yCarry = ComputeCarryYForReturn(start, endXZ, carryHeight);

        Vector3 wpUp = new Vector3(start.x, yCarry, start.z);
        Vector3 wpOver = new Vector3(endXZ.x, yCarry, endXZ.z);

        SetPath(wpUp, wpOver, depositApproachPoint);
    }

    Vector3 GetEndEffectorPosition()
    {
        if (ccdSolver != null && ccdSolver.joints != null && ccdSolver.joints.Length > 0)
            return ccdSolver.joints[ccdSolver.joints.Length - 1].position;

        return transform.position;
    }

    void CheckIfReachedCore()
    {
        if (ccdSolver == null || currentCore == null) return;

        Vector3 endEffectorPos = GetEndEffectorPosition();
        float distance = Vector3.Distance(endEffectorPos, currentCore.transform.position);

        if (distance < distanceThreshold)
        {
            currentState = State.WAITING_TO_GRAB;
            isMovingTarget = false;
            Invoke(nameof(StartGrabbing), pauseBeforeGrab);
        }
    }

    void StartGrabbing()
    {
        if (currentCore == null) return;

        currentState = State.GRABBING;
        currentCore.Collect();
        Invoke(nameof(AttachCore), grabDuration * 0.5f);
    }

    void AttachCore()
    {
        if (currentCore == null) return;
        if (ccdSolver == null || ccdSolver.joints == null || ccdSolver.joints.Length == 0) return;

        // Parent al end-effector para que “viaje” con el brazo.
        Transform endEffector = ccdSolver.joints[ccdSolver.joints.Length - 1];
        currentCore.transform.SetParent(endEffector);
        currentCore.transform.localPosition = Vector3.zero;

        Invoke(nameof(LiftCore), grabDuration * 0.5f);
    }

    void LiftCore()
    {
        currentState = State.LIFTING_CORE;

        if (currentCore != null)
        {
            Vector3 liftPosition = currentCore.transform.position + Vector3.up * liftHeight;
            SetPath(liftPosition);
        }

        Invoke(nameof(StartCarrying), 0.8f);
    }

    void StartCarrying()
    {
        currentState = State.CARRYING_CORE;
        coresCollected++;
        MoveToDeposit();
    }

    void CheckIfReachedDeposit()
    {
        if (ccdSolver == null || depositPoint == null) return;

        Vector3 endEffectorPos = GetEndEffectorPosition();
        float distance = Vector3.Distance(endEffectorPos, depositApproachPoint);

        if (distance < depositDistance)
        {
            currentState = State.LOWERING_CORE;
            isMovingTarget = false;
            Invoke(nameof(LowerToDeposit), pauseBeforeDeposit);
        }
    }

    void LowerToDeposit()
    {
        if (depositPoint == null) return;

        depositLowerPoint = ClampToArmReach(depositPoint.position);
        SetPath(depositLowerPoint);
    }

    void CheckIfFinishedLowering()
    {
        if (ccdSolver == null || depositPoint == null) return;

        Vector3 endEffectorPos = GetEndEffectorPosition();
        float distance = Vector3.Distance(endEffectorPos, depositLowerPoint);

        if (distance < depositDistance)
            DepositCore();
    }

    void DepositCore()
    {
        if (currentCore == null) return;

        currentState = State.DEPOSITING;
        coresDeposited++;
        isMovingTarget = false;

        // Suelta el core y lo coloca exactamente en el punto de depósito.
        currentCore.transform.SetParent(null);
        currentCore.transform.position = depositPoint.position;
        currentCore.Deposit();

        Invoke(nameof(DeactivateCurrentCore), depositDisplayTime);

        if (coresDeposited >= totalCores)
            Invoke(nameof(CompleteLevel), depositDisplayTime + 0.5f);
        else
            Invoke(nameof(StartNextPickup), depositDisplayTime + 0.5f);
    }

    void DeactivateCurrentCore()
    {
        if (currentCore != null)
        {
            currentCore.gameObject.SetActive(false);
            currentCore = null;
        }
    }

    void StartNextPickup()
    {
        currentState = State.APPROACHING_CORE;

        DataCore nextCore = FindNextUncollectedCore();
        if (nextCore != null)
        {
            currentCore = nextCore;
            MoveToNextCore();
        }
    }

    // ---------- Auto-create ----------
    void CreateTarget()
    {
        GameObject targetObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        targetObj.name = "Auto_Target";
        target = targetObj.transform;
        target.localScale = Vector3.one * 0.1f;

        Renderer renderer = targetObj.GetComponent<Renderer>();
        if (renderer != null) renderer.enabled = false;

        Collider col = targetObj.GetComponent<Collider>();
        if (col != null) Destroy(col);
    }

    void CreateDepositPoint()
    {
        GameObject depositObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        depositObj.name = "DepositPoint";
        depositPoint = depositObj.transform;

        Vector3 basePos = ccdSolver != null && ccdSolver.joints != null && ccdSolver.joints.Length > 0
            ? ccdSolver.joints[0].position
            : transform.position;

        depositPoint.position = basePos + new Vector3(-0.7f, 0.05f, 0f);
        depositPoint.localScale = new Vector3(0.4f, 0.05f, 0.4f);

        Renderer renderer = depositObj.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = new Color(1f, 0.5f, 0f);
            renderer.material = mat;
        }

        Collider col = depositObj.GetComponent<Collider>();
        if (col != null) Destroy(col);
    }

    DataCore FindNextUncollectedCore()
    {
        for (int i = 0; i < dataCores.Length; i++)
        {
            if (dataCores[i] != null && !dataCores[i].isCollected)
                return dataCores[i];
        }
        return null;
    }

    void CompleteLevel()
    {
        if (levelComplete) return;
        levelComplete = true;
        currentState = State.LEVEL_COMPLETE;

        Debug.Log("======================");
        Debug.Log("¡NIVEL 1 COMPLETADO!");
        Debug.Log($"Tiempo: {elapsedTime:F2}s");
        Debug.Log("======================");
    }

    /// <summary>
    /// Callback que se dispara desde DataCore cuando detecta el end-effector en trigger.
    /// </summary>
    public void OnDataCoreCollected(DataCore core)
    {
        if (core == null) return;

        if (currentState != State.APPROACHING_CORE) return;

        if (currentCore == null)
        {
            currentCore = core;
        }

        // isMovingTarget = false; // (comentado en tu código)
    }
}
