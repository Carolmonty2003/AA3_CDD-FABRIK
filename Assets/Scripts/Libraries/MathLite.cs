using UnityEngine;

public static class MathLite
{
    public const float PI = 3.1415926535897931f;
    public const float Deg2Rad = PI / 180f;
    public const float Rad2Deg = 180f / PI;

    public static float Sin(float x) => Mathf.Sin(x);
    public static float Cos(float x) => Mathf.Cos(x);

    public static float Sqrt(float x)
    {
        if (x <= 0f) return 0f;
        float guess = x * 0.5f;
        for (int i = 0; i < 5; i++)
            guess = 0.5f * (guess + x / guess);
        return guess;
    }

    public static float Abs(float x) => (x >= 0f) ? x : -x;
    public static float Sign(float x) => x > 0 ? 1f : (x < 0 ? -1f : 0f);

    public static float Clamp(float v, float mn, float mx)
    {
        if (v < mn) return mn;
        if (v > mx) return mx;
        return v;
    }

    public static int ClampInt(int v, int mn, int mx)
    {
        if (v < mn) return mn;
        if (v > mx) return mx;
        return v;
    }

    public static float Clamp01(float v) => Clamp(v, 0f, 1f);

    public static float Acos(float x)
    {
        x = Clamp(x, -1f, 1f);
        return Mathf.Acos(x);
    }

    public static float Min(float a, float b) => (a < b) ? a : b;
    public static float Max(float a, float b) => (a > b) ? a : b;

    public static float Repeat(float t, float length)
    {
        return t - Floor(t / length) * length;
    }

    public static float Floor(float x)
    {
        return (int)(x >= 0 ? x : x - 1);
    }

    public static float Lerp(float a, float b, float t)
    {
        t = Clamp01(t);
        return a + (b - a) * t;
    }

    public static float Exp(float x) => Mathf.Exp(x);

    /// <summary>
    /// Devuelve el "t" típico de amortiguación exponencial: t = 1 - exp(-damping * dt)
    /// Útil para smoothing estable frame-rate independent.
    /// </summary>
    public static float ExpDampT(float damping, float dt)
    {
        if (damping <= 0f) return 1f;
        if (dt <= 0f) return 0f;
        return 1f - Exp(-damping * dt);
    }
}
