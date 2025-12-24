using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Lerp : MonoBehaviour
{
    // Interpolación lineal de Vector3
    public static Vector3 Lerpp(Vector3 a, Vector3 b, float t)
    {
        t = MathLite.Clamp01(t);
        return a + (b - a) * t;
    }

    // Interpolación esférica de Vector3
    public static Vector3 SLerp(Vector3 a, Vector3 b, float t)
    {
        t = MathLite.Clamp01(t);
        float dot = Vectors.DotProduct(Vectors.Normalize(a), Vectors.Normalize(b));
        dot = MathLite.Clamp(dot, -1f, 1f);
        float theta = MathLite.Acos(dot) * t;

        Vector3 relativeVec = b - a * dot;
        relativeVec = Vectors.Normalize(relativeVec);

        Vector3 result = a * MathLite.Cos(theta) + relativeVec * MathLite.Sin(theta);
        return result * Lerpp(Vectors.Magnitude(a), Vectors.Magnitude(b), t);
    }

    // Lerp entre dos valores float (para compatibilidad)
    public static float Lerpp(float a, float b, float t)
    {
        t = MathLite.Clamp01(t);
        return a + (b - a) * t;
    }

    // Lerp para Quaternions
    public static Quaternion Lerpp(Quaternion a, Quaternion b, float t)
    {
        t = MathLite.Clamp01(t);
        float dot = a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w;
        if (dot < 0) b = new Quaternion(-b.x, -b.y, -b.z, -b.w);

        Quaternion result = new Quaternion(
            a.x + (b.x - a.x) * t,
            a.y + (b.y - a.y) * t,
            a.z + (b.z - a.z) * t,
            a.w + (b.w - a.w) * t
        );

        return Quaternions.Normalize(result);
    }

    // SLerp para Quaternions
    public static Quaternion SLerp(Quaternion a, Quaternion b, float t)
    {
        t = MathLite.Clamp01(t);
        float dot = a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w;

        if (dot < 0)
        {
            dot = -dot;
            b = new Quaternion(-b.x, -b.y, -b.z, -b.w);
        }

        float theta = MathLite.Acos(dot);
        float sinTheta = MathLite.Sin(theta);

        if (sinTheta > 0.001f)
        {
            float ratioA = MathLite.Sin((1 - t) * theta) / sinTheta;
            float ratioB = MathLite.Sin(t * theta) / sinTheta;

            return new Quaternion(
                a.x * ratioA + b.x * ratioB,
                a.y * ratioA + b.y * ratioB,
                a.z * ratioA + b.z * ratioB,
                a.w * ratioA + b.w * ratioB
            );
        }
        else return Lerpp(a, b, t);
    }
}
