using UnityEngine;

public class SmoothIKTargetProxy : MonoBehaviour
{
    [Header("IK")]
    public level3_CCDIK ik;

    [Header("Desired target (the one triggers set)")]
    public Transform desiredTarget;

    [Header("Smoothing")]
    [Tooltip("Tiempo de suavizado (segundos). 0 = instantáneo.")]
    public float smoothTime = 0.15f;

    [Tooltip("Si true, también suaviza rotación del proxy (el CCD usa posición, pero puede venir bien).")]
    public bool smoothRotation = false;

    [Header("Behaviour")]
    [Tooltip("Mantiene ik.target apuntando siempre al proxy (evita que otro script lo sobreescriba).")]
    public bool forceProxyAsIKTarget = true;

    Transform proxy;

    public bool ikEnabled = true;


    void Awake()
    {
        GameObject go = new GameObject("IK_TargetProxy_Smooth");
        proxy = go.transform;

        if (ik != null && ik.target != null)
        {
            proxy.position = ik.target.position;
            proxy.rotation = ik.target.rotation;
        }
        else
        {
            proxy.position = transform.position;
            proxy.rotation = transform.rotation;
        }

        if (desiredTarget != null)
        {
            proxy.position = desiredTarget.position;
            proxy.rotation = desiredTarget.rotation;
        }

        if (ik != null)
            ik.target = proxy;
    }

    void LateUpdate()
    {
        if (proxy == null) return;

        if (ik == null) return;

        if (!ikEnabled)
        {
            if (ik.target != null) ik.target = null;
            return;
        }

        if (forceProxyAsIKTarget && ik.target != proxy)
            ik.target = proxy;

        if (desiredTarget == null) return;

        float dt = Time.deltaTime;

        proxy.position = Lerp.ExpSmooth(proxy.position, desiredTarget.position, smoothTime, dt);

        if (smoothRotation)
        {
            float t = ExpSmoothT(smoothTime, dt);
            proxy.rotation = Lerp.SLerp(proxy.rotation, desiredTarget.rotation, t);
        }
    }

    public void EnableIK(bool enabled)
    {
        ikEnabled = enabled;

        if (ik == null) return;

        if (!enabled)
            ik.target = null;
        else
            ik.target = proxy;
    }


    static float ExpSmoothT(float st, float dt)
    {
        if (st <= 1e-6f) return 1f;
        if (dt <= 0f) return 0f;
        return 1f - MathLite.Exp(-dt / st);
    }

    public void SetDesiredTarget(Transform t)
    {
        desiredTarget = t;
    }

    public Transform GetProxy() => proxy;
}
