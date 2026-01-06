using UnityEngine;

public static class Vectors
{
    public static float SqrMagnitude(Vector3 v)
    {
        return v.x * v.x + v.y * v.y + v.z * v.z;
    }

    public static float Magnitude(Vector3 v)
    {
        return MathLite.Sqrt(SqrMagnitude(v));
    }

    public static float DistanceSqr(Vector3 a, Vector3 b)
    {
        return SqrMagnitude(b - a);
    }

    public static float Distance(Vector3 a, Vector3 b)
    {
        return MathLite.Sqrt(DistanceSqr(a, b));
    }

    public static Vector3 Normalize(Vector3 v)
    {
        float mag = Magnitude(v);
        return (mag > 1e-6f) ? v / mag : new Vector3(0f, 0f, 0f);
    }

    public static float DotProduct(Vector3 a, Vector3 b)
    {
        return a.x * b.x + a.y * b.y + a.z * b.z;
    }

    public static Vector3 CrossProduct(Vector3 a, Vector3 b)
    {
        return new Vector3(
            a.y * b.z - a.z * b.y,
            a.z * b.x - a.x * b.z,
            a.x * b.y - a.y * b.x
        );
    }

    public static Vector3 ProductByScalar(Vector3 v, float s)
    {
        return new Vector3(v.x * s, v.y * s, v.z * s);
    }

    public static Vector3 MoveTowards(Vector3 current, Vector3 target, float maxDistanceDelta)
    {
        Vector3 to = target - current;
        float dist = Magnitude(to);

        if (dist <= maxDistanceDelta || dist < 1e-6f)
            return target;

        return current + (to / dist) * maxDistanceDelta;
    }

    public static float Angle(Vector3 a, Vector3 b)
    {
        float dot = DotProduct(a, b);
        float mags = Magnitude(a) * Magnitude(b);
        if (mags < 1e-6f) return 0f;

        float cosTheta = MathLite.Clamp(dot / mags, -1f, 1f);
        return MathLite.Acos(cosTheta) * MathLite.Rad2Deg;
    }

    public static Vector3 Forward() => new Vector3(0, 0, 1);
    public static Vector3 Up() => new Vector3(0, 1, 0);
    public static Vector3 Right() => new Vector3(1, 0, 0);

    /// <summary>
    /// Devuelve 'point' clamped a una distancia máxima desde 'origin'.
    /// Si ya está dentro, se devuelve tal cual.
    /// </summary>
    public static Vector3 ClampToMaxDistance(Vector3 origin, Vector3 point, float maxDistance)
    {
        if (maxDistance <= 0f) return origin;

        Vector3 v = point - origin;
        float dist = Magnitude(v);

        if (dist > maxDistance && dist > 1e-6f)
            return origin + (v / dist) * maxDistance;

        return point;
    }
}
