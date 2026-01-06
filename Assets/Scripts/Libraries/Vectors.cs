using UnityEngine;

public static class Vectors
{
    // Magnitud cuadrada de un vector (sin hacer sqrt)
    public static float SqrMagnitude(Vector3 v)
    {
        return v.x * v.x + v.y * v.y + v.z * v.z;
    }

    // Magnitud de un vector (longitud)
    public static float Magnitude(Vector3 v)
    {
        return MathLite.Sqrt(SqrMagnitude(v));
    }

    // Distancia (y distancia^2) entre dos puntos
    public static float DistanceSqr(Vector3 a, Vector3 b)
    {
        return SqrMagnitude(b - a);
    }

    public static float Distance(Vector3 a, Vector3 b)
    {
        return MathLite.Sqrt(DistanceSqr(a, b));
    }

    // Normaliza un vector (lo hace unitario)
    public static Vector3 Normalize(Vector3 v)
    {
        float mag = Magnitude(v);
        return (mag > 1e-6f) ? v / mag : new Vector3(0f, 0f, 0f);
    }

    // Producto escalar (dot product)
    public static float DotProduct(Vector3 a, Vector3 b)
    {
        return a.x * b.x + a.y * b.y + a.z * b.z;
    }

    // Producto vectorial (cross product)
    public static Vector3 CrossProduct(Vector3 a, Vector3 b)
    {
        return new Vector3(
            a.y * b.z - a.z * b.y,
            a.z * b.x - a.x * b.z,
            a.x * b.y - a.y * b.x
        );
    }

    // Multiplicación de un vector por un escalar (por si lo prefieres en vez de v * s)
    public static Vector3 ProductByScalar(Vector3 v, float s)
    {
        return new Vector3(v.x * s, v.y * s, v.z * s);
    }

    // MoveTowards (equivalente a Vector3.MoveTowards)
    public static Vector3 MoveTowards(Vector3 current, Vector3 target, float maxDistanceDelta)
    {
        Vector3 to = target - current;
        float dist = Magnitude(to);

        if (dist <= maxDistanceDelta || dist < 1e-6f)
            return target;

        return current + (to / dist) * maxDistanceDelta;
    }

    // Calcula el ángulo entre dos vectores (en grados)
    public static float Angle(Vector3 a, Vector3 b)
    {
        float dot = DotProduct(a, b);
        float mags = Magnitude(a) * Magnitude(b);
        if (mags < 1e-6f) return 0f;

        float cosTheta = MathLite.Clamp(dot / mags, -1f, 1f);
        return MathLite.Acos(cosTheta) * MathLite.Rad2Deg;
    }

    // Direcciones base
    public static Vector3 Forward() => new Vector3(0, 0, 1);
    public static Vector3 Up() => new Vector3(0, 1, 0);
    public static Vector3 Right() => new Vector3(1, 0, 0);


    // Proyección de un vector sobre un plano (equivalente a Vector3.ProjectOnPlane)
    public static Vector3 ProjectOnPlane(Vector3 v, Vector3 planeNormal)
    {
        Vector3 n = Normalize(planeNormal);
        float d = DotProduct(v, n);
        return v - n * d;
    }

}