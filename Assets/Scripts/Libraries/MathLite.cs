using UnityEngine;

public static class MathLite
{
    public const float PI = 3.1415926535897931f;
    public const float Deg2Rad = PI / 180f;
    public const float Rad2Deg = 180f / PI;

    public static float Sin(float x) => Mathf.Sin(x);
    public static float Cos(float x) => Mathf.Cos(x);

    // Raíz cuadrada aproximada con el método de Newton-Raphson
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

    // Limita un valor dentro de un rango
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

    // Asegura que x esté dentro de [-1,1] antes de calcular Acos
    public static float Acos(float x)
    {
        x = Clamp(x, -1f, 1f);
        return Mathf.Acos(x);
    }

    public static float Min(float a, float b) => (a < b) ? a : b;
    public static float Max(float a, float b) => (a > b) ? a : b;

    // Hace que un valor se repita dentro del rango [0, length)
    public static float Repeat(float t, float length)
    {
        return t - Floor(t / length) * length;
    }

    // Redondea hacia abajo
    public static float Floor(float x)
    {
        return (int)(x >= 0 ? x : x - 1);
    }

    // Interpolación lineal entre a y b con t en [0,1]
    public static float Lerp(float a, float b, float t)
    {
        t = Clamp01(t);
        return a + (b - a) * t;
    }

    public static float Exp(float x) => Mathf.Exp(x);
}