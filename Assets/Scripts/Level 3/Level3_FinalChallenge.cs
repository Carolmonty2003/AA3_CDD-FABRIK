using UnityEngine;

public class level3_FinalChallenge : MonoBehaviour
{
    [Header("Left Arm (CCD)")]
    public level3_CCDIK leftCCD;
    public Transform leftButton;

    [Header("Right Arm (FABRIK)")]
    public level3_FABRIKIK rightFABRIK;
    public Transform rightButton;

    [Header("Press Settings")]
    [Min(0.001f)] public float pressRadius = 0.10f;
    [Min(0f)] public float requiredHoldSeconds = 0.25f;

    float holdTimer = 0f;
    bool won = false;

    void Start()
    {
        if (leftCCD != null) leftCCD.target = leftButton;
        if (rightFABRIK != null) rightFABRIK.target = rightButton;
    }

    void Update()
    {
        if (won) return;

        Transform leftEnd = GetEndEffector(leftCCD);
        Transform rightEnd = GetEndEffector(rightFABRIK);

        bool leftPressed = leftEnd != null && leftButton != null &&
                           Vectors.Distance(leftEnd.position, leftButton.position) <= pressRadius;

        bool rightPressed = rightEnd != null && rightButton != null &&
                            Vectors.Distance(rightEnd.position, rightButton.position) <= pressRadius;

        if (leftPressed && rightPressed)
        {
            holdTimer += Time.deltaTime;
            if (holdTimer >= requiredHoldSeconds) Win();
        }
        else holdTimer = 0f;
    }

    void Win()
    {
        won = true;
        Debug.Log("LEVEL 3 COMPLETADO: ambos botones pulsados a la vez.");
    }

    Transform GetEndEffector(level3_CCDIK solver)
    {
        if (solver == null || solver.joints == null || solver.joints.Length == 0) return null;
        return solver.joints[solver.joints.Length - 1];
    }

    Transform GetEndEffector(level3_FABRIKIK solver)
    {
        if (solver == null || solver.joints == null || solver.joints.Length == 0) return null;
        return solver.joints[solver.joints.Length - 1];
    }

    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(12, 12, 360, 220), GUI.skin.box);

        GUILayout.Label("<b>LEVEL 3 - Debug IK</b>", new GUIStyle(GUI.skin.label) { richText = true });
        GUILayout.Space(6);

        if (leftCCD != null)
        {
            GUILayout.Label("Brazo Izquierdo: CCD");
            GUILayout.Label("Iteraciones (último frame): " + leftCCD.lastIterationsUsed);
            GUILayout.Label("Distancia al target: " + leftCCD.currentDistance.ToString("F4"));
        }
        else GUILayout.Label("Brazo Izquierdo: (sin referencia)");

        GUILayout.Space(8);

        if (rightFABRIK != null)
        {
            GUILayout.Label("Brazo Derecho: FABRIK");
            GUILayout.Label("Iteraciones (último frame): " + rightFABRIK.lastIterationsUsed);
            GUILayout.Label("Distancia al target: " + rightFABRIK.lastDistanceToTarget.ToString("F4"));
        }
        else GUILayout.Label("Brazo Derecho: (sin referencia)");

        GUILayout.Space(8);
        GUILayout.Label("Hold simultáneo: " + holdTimer.ToString("F2") + " / " + requiredHoldSeconds.ToString("F2"));
        GUILayout.Label("Estado: " + (won ? "COMPLETADO" : "EN PROGRESO"));

        GUILayout.EndArea();
    }
}
