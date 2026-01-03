using UnityEngine;

public class level3_MovingTarget : MonoBehaviour
{
    public Transform pointA;
    public Transform pointB;

    [Min(0.01f)] public float speed = 1f;
    public float phaseOffset = 0f;

    static readonly float TAU = 2f * MathLite.PI;
    static float WrapPi(float x) => MathLite.Repeat(x + MathLite.PI, TAU) - MathLite.PI;

    static float SinApprox(float x)
    {
        x = WrapPi(x);
        if (x > MathLite.PI * 0.5f) x = MathLite.PI - x;
        else if (x < -MathLite.PI * 0.5f) x = -MathLite.PI - x;

        float x2 = x * x;
        float x3 = x * x2;
        float x5 = x3 * x2;
        float x7 = x5 * x2;
        return x - x3 * (1f / 6f) + x5 * (1f / 120f) - x7 * (1f / 5040f);
    }

    void Update()
    {
        if (pointA == null || pointB == null) return;

        float t = 0.5f * (1f + SinApprox(Time.time * speed + phaseOffset));
        t = MathLite.Clamp01(t);

        transform.position = pointA.position + (pointB.position - pointA.position) * t;
    }
}
