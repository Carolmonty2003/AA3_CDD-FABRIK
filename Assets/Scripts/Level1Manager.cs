using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Level1Manager con ESQUIVA MEJORADA
/// El target ESPERA a que el brazo llegue antes de avanzar al siguiente waypoint
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

    [Header("NAVEGACIÓN Y OBSTÁCULOS")]
    [Tooltip("Obstáculos que el brazo debe esquivar")]
    public GameObject[] obstacles;

    [Tooltip("Altura a la que se eleva para esquivar obstáculos")]
    public float avoidanceHeight = 0.8f;

    [Tooltip("Detectar obstáculos automáticamente")]
    public bool autoDetectObstacles = true;

    [Tooltip("Layer de obstáculos (opcional)")]
    public LayerMask obstacleLayer = -1;

    [Header("Control del Flujo")]
    public bool autoTargetNextCore = true;
    private int currentTargetIndex = 0;

    private enum State
    {
        APPROACHING_CORE,
        AVOIDING_OBSTACLE,
        WAITING_TO_GRAB,
        GRABBING,
        LIFTING_CORE,
        CARRYING_CORE,
        AVOIDING_RETURN,
        LOWERING_CORE,
        DEPOSITING,
        LEVEL_COMPLETE
    }

    private State currentState = State.APPROACHING_CORE;
    private DataCore currentCore = null;

    [Header("Configuración de Movimiento")]
    public float targetMoveSpeed = 5.0f;

    [Tooltip("Distancia para considerar que el brazo llegó al waypoint")]
    public float waypointArrivalDistance = 2.0f;

    public float pauseBeforeGrab = 0.3f;
    public float grabDuration = 0.5f;
    public float liftHeight = 0.3f;
    public float pauseBeforeDeposit = 0.3f;
    public float depositDisplayTime = 1.0f;

    [Header("Configuración Técnica")]
    public float distanceThreshold = 0.15f;
    public float depositDistance = 0.25f;

    [Header("Estado del Nivel")]
    public bool levelComplete = false;
    public float elapsedTime = 0f;

    // Sistema de waypoints mejorado
    private Queue<Vector3> waypointQueue = new Queue<Vector3>();
    private Vector3 currentWaypoint;
    private bool hasCurrentWaypoint = false;
    private bool waitingForArmToArrive = false;

    void Start()
    {
        Debug.Log("=== NIVEL 1 INICIANDO (Esquiva Mejorada) ===");

        if (dataCores == null || dataCores.Length == 0)
        {
            dataCores = FindObjectsOfType<DataCore>();
            Debug.Log($"Auto-encontrados {dataCores.Length} DataCores");
        }

        totalCores = dataCores.Length;
        coresCollected = 0;
        coresDeposited = 0;

        if ((obstacles == null || obstacles.Length == 0) && autoDetectObstacles)
        {
            FindObstacles();
        }

        if (depositPoint == null && autoCreateDepositPoint)
        {
            CreateDepositPoint();
        }

        if (target == null && autoCreateTarget)
        {
            CreateTarget();
        }

        if (ccdSolver == null)
        {
            ccdSolver = GetComponent<CCDIK>();
        }

        if (ccdSolver != null && target != null)
        {
            ccdSolver.target = target;
            Debug.Log("✓ Target asignado al CCD");
        }

        if (autoTargetNextCore)
        {
            currentState = State.APPROACHING_CORE;
            MoveToNextCoreWithAvoidance();
        }

        Debug.Log($"=== NIVEL 1 LISTO: {totalCores} núcleos | {obstacles?.Length ?? 0} obstáculos ===");
    }

    void Update()
    {
        if (!levelComplete)
        {
            elapsedTime += Time.deltaTime;
            UpdateWaypointNavigation();
            UpdateStateMachine();
        }
    }

    void FindObstacles()
    {
        List<GameObject> foundObstacles = new List<GameObject>();

        GameObject[] tagged1 = GameObject.FindGameObjectsWithTag("Obstacle");
        GameObject[] tagged2 = GameObject.FindGameObjectsWithTag("Column");

        foundObstacles.AddRange(tagged1);
        foundObstacles.AddRange(tagged2);

        if (foundObstacles.Count == 0)
        {
            GameObject[] allObjects = FindObjectsOfType<GameObject>();
            foreach (GameObject obj in allObjects)
            {
                if (obj.name.ToLower().Contains("column") ||
                    obj.name.ToLower().Contains("columna") ||
                    obj.name.ToLower().Contains("servidor") ||
                    obj.name.ToLower().Contains("obstacle"))
                {
                    foundObstacles.Add(obj);
                }
            }
        }

        obstacles = foundObstacles.ToArray();
        Debug.Log($"Auto-detectados {obstacles.Length} obstáculos");
    }

    /// <summary>
    /// Sistema de navegación mejorado:
    /// El target se mueve hacia el waypoint actual
    /// ESPERA a que el brazo llegue cerca antes de avanzar al siguiente
    /// </summary>
    void UpdateWaypointNavigation()
    {
        if (!hasCurrentWaypoint || target == null) return;

        // Mover target hacia el waypoint actual
        float distanceToWaypoint = Vectors.Distance(target.position, currentWaypoint);

        if (distanceToWaypoint > 0.01f)
        {
            target.position = Vectors.MoveTowards(
                target.position,
                currentWaypoint,
                targetMoveSpeed * Time.deltaTime
            );
        }
        else
        {
            // Target llegó al waypoint
            target.position = currentWaypoint;

            if (!waitingForArmToArrive)
            {
                waitingForArmToArrive = true;
                Debug.Log($"✓ Target llegó a waypoint, esperando al brazo...");
            }
        }

        // Verificar si el BRAZO llegó cerca del waypoint actual
        if (waitingForArmToArrive)
        {
            Vector3 armPos = GetEndEffectorPosition();
            float armDistance = Vectors.Distance(armPos, currentWaypoint);

            if (armDistance < waypointArrivalDistance)
            {
                Debug.Log($"✓ Brazo llegó al waypoint (distancia: {armDistance:F2}m)");
                waitingForArmToArrive = false;
                AdvanceToNextWaypoint();
            }
        }
    }

    void AdvanceToNextWaypoint()
    {
        if (waypointQueue.Count > 0)
        {
            currentWaypoint = waypointQueue.Dequeue();
            hasCurrentWaypoint = true;
            Debug.Log($"→ Siguiente waypoint: {currentWaypoint}");
        }
        else
        {
            hasCurrentWaypoint = false;
            Debug.Log("✓ Ruta completada");
        }
    }

    void UpdateStateMachine()
    {
        switch (currentState)
        {
            case State.APPROACHING_CORE:
            case State.AVOIDING_OBSTACLE:
                CheckIfReachedCore();
                break;

            case State.CARRYING_CORE:
            case State.AVOIDING_RETURN:
                CheckIfReachedDeposit();
                break;

            case State.LOWERING_CORE:
                CheckIfFinishedLowering();
                break;
        }
    }

    bool IsPathBlocked(Vector3 from, Vector3 to)
    {
        if (!autoDetectObstacles || obstacles == null || obstacles.Length == 0)
            return false;

        Vector3 direction = to - from;
        float distance = Vectors.Magnitude(direction);

        RaycastHit hit;
        if (Physics.Raycast(from, direction, out hit, distance, obstacleLayer))
        {
            foreach (GameObject obstacle in obstacles)
            {
                if (obstacle != null && hit.collider.gameObject == obstacle)
                {
                    Debug.Log($"⚠ Obstáculo detectado: {obstacle.name}");
                    return true;
                }
            }
        }

        return false;
    }

    void MoveToNextCoreWithAvoidance()
    {
        if (currentCore == null)
        {
            currentCore = FindNextUncollectedCore();
        }

        if (target == null || currentCore == null)
        {
            Debug.LogWarning("⚠ No hay núcleo disponible");
            return;
        }

        Debug.Log($">>> Yendo hacia: {currentCore.gameObject.name}");

        waypointQueue.Clear();
        waitingForArmToArrive = false;

        Vector3 startPos = GetArmBasePosition();
        Vector3 endPos = currentCore.transform.position;

        if (IsPathBlocked(startPos, endPos))
        {
            Debug.Log("¡Obstáculo detectado! Calculando ruta alternativa...");
            currentState = State.AVOIDING_OBSTACLE;

            Vector3 aboveObstacle = new Vector3(
                (startPos.x + endPos.x) / 2f,
                Mathf.Max(startPos.y, endPos.y) + avoidanceHeight,
                (startPos.z + endPos.z) / 2f
            );

            // Añadir waypoints a la cola
            waypointQueue.Enqueue(aboveObstacle);
            waypointQueue.Enqueue(endPos);

            Debug.Log($"Ruta: {startPos} → {aboveObstacle} (elevado) → {endPos}");
        }
        else
        {
            waypointQueue.Enqueue(endPos);
        }

        // Empezar con el primer waypoint
        AdvanceToNextWaypoint();
    }

    void MoveToDepositWithAvoidance()
    {
        if (target == null || depositPoint == null) return;

        waypointQueue.Clear();
        waitingForArmToArrive = false;

        Vector3 startPos = GetEndEffectorPosition();
        Vector3 aboveDeposit = depositPoint.position + Vector3.up * liftHeight;

        if (IsPathBlocked(startPos, aboveDeposit))
        {
            Debug.Log("Obstáculo en el camino de vuelta, esquivando...");
            currentState = State.AVOIDING_RETURN;

            Vector3 avoidPoint = new Vector3(
                (startPos.x + aboveDeposit.x) / 2f,
                Mathf.Max(startPos.y, aboveDeposit.y) + avoidanceHeight,
                (startPos.z + aboveDeposit.z) / 2f
            );

            waypointQueue.Enqueue(avoidPoint);
            waypointQueue.Enqueue(aboveDeposit);
        }
        else
        {
            waypointQueue.Enqueue(aboveDeposit);
        }

        AdvanceToNextWaypoint();
    }

    Vector3 GetArmBasePosition()
    {
        if (ccdSolver != null && ccdSolver.joints != null && ccdSolver.joints.Length > 0)
        {
            return ccdSolver.joints[0].position;
        }
        return transform.position;
    }

    Vector3 GetEndEffectorPosition()
    {
        if (ccdSolver != null && ccdSolver.joints != null && ccdSolver.joints.Length > 0)
        {
            return ccdSolver.joints[ccdSolver.joints.Length - 1].position;
        }
        return transform.position;
    }

    void CheckIfReachedCore()
    {
        if (ccdSolver == null || currentCore == null) return;

        Vector3 endEffectorPos = GetEndEffectorPosition();
        float distance = Vectors.Distance(endEffectorPos, currentCore.transform.position);

        if (distance < distanceThreshold && !hasCurrentWaypoint)
        {
            Debug.Log("Brazo llegó al núcleo");
            currentState = State.WAITING_TO_GRAB;
            Invoke("StartGrabbing", pauseBeforeGrab);
        }
    }

    void StartGrabbing()
    {
        if (currentCore == null) return;
        currentState = State.GRABBING;
        currentCore.Collect();
        Invoke("AttachCore", grabDuration * 0.5f);
    }

    void AttachCore()
    {
        if (currentCore == null) return;

        Transform endEffector = ccdSolver.joints[ccdSolver.joints.Length - 1];
        currentCore.transform.SetParent(endEffector);
        currentCore.transform.localPosition = Vector3.zero;

        Invoke("LiftCore", grabDuration * 0.5f);
    }

    void LiftCore()
    {
        currentState = State.LIFTING_CORE;

        if (currentCore != null)
        {
            Vector3 liftPosition = currentCore.transform.position + Vector3.up * liftHeight;

            waypointQueue.Clear();
            waypointQueue.Enqueue(liftPosition);
            waitingForArmToArrive = false;

            AdvanceToNextWaypoint();
        }

        Invoke("StartCarrying", 0.8f);
    }

    void StartCarrying()
    {
        Debug.Log("Llevando núcleo al depósito (con esquiva)...");
        currentState = State.CARRYING_CORE;
        coresCollected++;

        MoveToDepositWithAvoidance();
    }

    void CheckIfReachedDeposit()
    {
        if (ccdSolver == null || depositPoint == null) return;

        Vector3 endEffectorPos = GetEndEffectorPosition();
        Vector3 aboveDeposit = depositPoint.position + Vector3.up * liftHeight;
        float distance = Vectors.Distance(endEffectorPos, aboveDeposit);

        if (distance < depositDistance && !hasCurrentWaypoint)
        {
            currentState = State.LOWERING_CORE;
            Invoke("LowerToDeposit", pauseBeforeDeposit);
        }
    }

    void LowerToDeposit()
    {
        waypointQueue.Clear();
        waypointQueue.Enqueue(depositPoint.position);
        waitingForArmToArrive = false;
        AdvanceToNextWaypoint();
    }

    void CheckIfFinishedLowering()
    {
        if (ccdSolver == null || depositPoint == null) return;

        Vector3 endEffectorPos = GetEndEffectorPosition();
        float distance = Vectors.Distance(endEffectorPos, depositPoint.position);

        if (distance < depositDistance)
        {
            DepositCore();
        }
    }

    void DepositCore()
    {
        if (currentCore == null) return;

        currentState = State.DEPOSITING;
        coresDeposited++;

        currentCore.transform.SetParent(null);
        currentCore.transform.position = depositPoint.position;
        currentCore.Deposit();

        Invoke("DeactivateCurrentCore", depositDisplayTime);

        if (coresDeposited >= totalCores)
        {
            Invoke("CompleteLevel", depositDisplayTime + 0.5f);
        }
        else
        {
            Invoke("StartNextPickup", depositDisplayTime + 0.5f);
        }
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
            MoveToNextCoreWithAvoidance();
        }
    }

    public void OnDataCoreCollected(DataCore core)
    {
        if (currentCore == null && currentState == State.APPROACHING_CORE)
        {
            currentCore = core;
        }
    }

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

        Vector3 basePos = GetArmBasePosition();
        depositPoint.position = basePos + new Vector3(-0.7f, 0.05f, 0);
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
            {
                return dataCores[i];
            }
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

    void OnDrawGizmos()
    {
        if (obstacles != null)
        {
            Gizmos.color = Color.red;
            foreach (GameObject obstacle in obstacles)
            {
                if (obstacle != null)
                {
                    Gizmos.DrawWireCube(obstacle.transform.position, obstacle.transform.localScale);
                }
            }
        }

        if (Application.isPlaying && hasCurrentWaypoint)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(currentWaypoint, 0.2f);
            Gizmos.DrawLine(target.position, currentWaypoint);
        }

        if (Application.isPlaying && waypointQueue != null && waypointQueue.Count > 0)
        {
            Gizmos.color = Color.cyan;
            Vector3 prev = hasCurrentWaypoint ? currentWaypoint : (target != null ? target.position : Vector3.zero);
            foreach (Vector3 waypoint in waypointQueue)
            {
                Gizmos.DrawLine(prev, waypoint);
                Gizmos.DrawWireSphere(waypoint, 0.15f);
                prev = waypoint;
            }
        }
    }
}