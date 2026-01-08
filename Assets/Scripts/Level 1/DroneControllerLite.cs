using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class DroneControllerLite : MonoBehaviour
{
    [Header("Velocidades")]
    public float moveSpeed = 8f;        // W/S
    public float verticalSpeed = 6f;    // Q/E
    public float yawSpeedDeg = 120f;    // A/D (grados por segundo)

    [Header("Suavizado")]
    public float acceleration = 20f;    // cambio m�x de velocidad por segundo

    [Header("Opciones")]
    public bool useLocalUp = true;      // subir/bajar seg�n el "up" del dron
    public bool yawUsesLocalUp = true;  // yaw alrededor del up local (si no, up global)

    Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;

        // Deja girar en Y pero bloquea vuelcos (X/Z)
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        // Inputs
        float z = Input.GetAxisRaw("Vertical");    // W/S
        float yawInput = Input.GetAxisRaw("Horizontal"); // A/D

        float y = 0f;
        if (Input.GetKey(KeyCode.E)) y += 1f; // subir
        if (Input.GetKey(KeyCode.Q)) y -= 1f; // bajar

        z = MathLite.Clamp(z, -1f, 1f);
        y = MathLite.Clamp(y, -1f, 1f);
        yawInput = MathLite.Clamp(yawInput, -1f, 1f);

        // --- ROTACI�N (A/D) ---
        Quaternion currentRot = rb.rotation;

        Vector3 yawAxis = yawUsesLocalUp
            ? Quaternions.Rotate3D(Vectors.Up(), currentRot)
            : Vectors.Up();

        float yawRad = (yawInput * yawSpeedDeg) * MathLite.Deg2Rad * dt;
        Quaternion deltaYaw = Quaternions.AxisAngle(yawAxis, yawRad);

        Quaternion newRot = Quaternions.Multiply(deltaYaw, currentRot);
        rb.MoveRotation(newRot);

        // --- MOVIMIENTO (W/S + Q/E) usando la nueva rotaci�n ---
        Vector3 forward = Quaternions.Rotate3D(Vectors.Forward(), newRot);
        Vector3 upDir = useLocalUp ? Quaternions.Rotate3D(Vectors.Up(), newRot) : Vectors.Up();

        Vector3 targetPlanarVel = forward * (z * moveSpeed);
        Vector3 targetVerticalVel = upDir * (y * verticalSpeed);

        Vector3 targetVelocity = targetPlanarVel + targetVerticalVel;

        float maxDelta = acceleration * dt;
        rb.linearVelocity = Vectors.MoveTowards(rb.linearVelocity, targetVelocity, maxDelta);
    }
}