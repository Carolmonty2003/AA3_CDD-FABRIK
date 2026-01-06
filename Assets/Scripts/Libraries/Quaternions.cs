using UnityEngine;

public static class Quaternions
{
    public static float Magnitude(Quaternion q)
    {
        return MathLite.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
    }

    public static Quaternion Normalize(Quaternion q)
    {
        float mag = Magnitude(q);
        return (mag > 1e-6f) ? new Quaternion(q.x / mag, q.y / mag, q.z / mag, q.w / mag) : Quaternion.identity;
    }

    public static Quaternion Conjugate(Quaternion q)
    {
        return new Quaternion(-q.x, -q.y, -q.z, q.w);
    }

    public static Quaternion Multiply(Quaternion a, Quaternion b)
    {
        return new Quaternion(
            a.w * b.x + a.x * b.w + a.y * b.z - a.z * b.y,
            a.w * b.y - a.x * b.z + a.y * b.w + a.z * b.x,
            a.w * b.z + a.x * b.y - a.y * b.x + a.z * b.w,
            a.w * b.w - a.x * b.x - a.y * b.y - a.z * b.z
        );
    }

    public static Vector3 Rotate3D(Vector3 v, Quaternion q)
    {
        Quaternion vQ = new Quaternion(v.x, v.y, v.z, 0);
        Quaternion result = Multiply(Multiply(q, vQ), Conjugate(q));
        return new Vector3(result.x, result.y, result.z);
    }

    public static Quaternion LookRotationCustom(Vector3 forward, Vector3 up)
    {
        // Igual que Quaternion.LookRotation (Unity): construimos una base ortonormal
        // con forward como eje Z (transform.forward) y up como eje Y.
        Vector3 f = Vectors.Normalize(forward);
        Vector3 u = Vectors.Normalize(up);

        // Si forward es casi cero, no podemos construir una rotación válida
        if (Vectors.SqrMagnitude(f) < 1e-12f) return Quaternion.identity;

        // right = up x forward (Unity)
        Vector3 r = Vectors.CrossProduct(u, f);
        if (Vectors.SqrMagnitude(r) < 1e-12f)
        {
            // up y forward están casi paralelos: buscamos un up alternativo
            u = (MathLite.Abs(f.y) < 0.999f) ? Vectors.Up() : Vectors.Right();
            r = Vectors.CrossProduct(u, f);
        }

        r = Vectors.Normalize(r);
        u = Vectors.Normalize(Vectors.CrossProduct(f, r));

        // Matriz de rotación con los ejes como COLUMNAS: [r u f]
        float m00 = r.x, m01 = u.x, m02 = f.x;
        float m10 = r.y, m11 = u.y, m12 = f.y;
        float m20 = r.z, m21 = u.z, m22 = f.z;

        float tr = m00 + m11 + m22;
        Quaternion q;

        if (tr > 0f)
        {
            float s = MathLite.Sqrt(tr + 1.0f) * 2f;
            q = new Quaternion(
                (m21 - m12) / s,
                (m02 - m20) / s,
                (m10 - m01) / s,
                0.25f * s
            );
        }
        else if (m00 > m11 && m00 > m22)
        {
            float s = MathLite.Sqrt(1.0f + m00 - m11 - m22) * 2f;
            q = new Quaternion(
                0.25f * s,
                (m01 + m10) / s,
                (m02 + m20) / s,
                (m21 - m12) / s
            );
        }
        else if (m11 > m22)
        {
            float s = MathLite.Sqrt(1.0f + m11 - m00 - m22) * 2f;
            q = new Quaternion(
                (m01 + m10) / s,
                0.25f * s,
                (m12 + m21) / s,
                (m02 - m20) / s
            );
        }
        else
        {
            float s = MathLite.Sqrt(1.0f + m22 - m00 - m11) * 2f;
            q = new Quaternion(
                (m02 + m20) / s,
                (m12 + m21) / s,
                0.25f * s,
                (m10 - m01) / s
            );
        }

        return Normalize(q);
    }

    public static Quaternion FromToRotation(Vector3 from, Vector3 to)
    {
        Vector3 f = Vectors.Normalize(from);
        Vector3 t = Vectors.Normalize(to);
        float dot = Vectors.DotProduct(f, t);

        if (dot > 0.999999f) return Quaternion.identity;
        if (dot < -0.999999f)
        {
            Vector3 ortho = MathLite.Abs(f.x) < 0.1f ? new Vector3(1, 0, 0) : new Vector3(0, 1, 0);
            Vector3 Axis = Vectors.Normalize(Vectors.CrossProduct(f, ortho));
            float s = MathLite.Sin(MathLite.PI * 0.5f);
            float c = MathLite.Cos(MathLite.PI * 0.5f);
            return Normalize(new Quaternion(Axis.x * s, Axis.y * s, Axis.z * s, c));
        }

        Vector3 axis = Vectors.CrossProduct(f, t);
        float s2 = MathLite.Sqrt((1f + dot) * 2f);
        float invs2 = 1f / s2;
        Quaternion q = new Quaternion(axis.x * invs2, axis.y * invs2, axis.z * invs2, s2 * 0.5f);
        return Normalize(q);
    }

    public static Quaternion AxisAngle(Vector3 axis, float angleRad)
    {
        Vector3 ax = Vectors.Normalize(axis);
        float half = angleRad * 0.5f;
        float s = MathLite.Sin(half);
        float c = MathLite.Cos(half);
        return Normalize(new Quaternion(ax.x * s, ax.y * s, ax.z * s, c));
    }

    public static Quaternion Yaw(float yawRad) => AxisAngle(Vectors.Up(), yawRad);
    public static Quaternion Pitch(float pitchRad) => AxisAngle(Vectors.Right(), pitchRad);
    public static Quaternion Roll(float rollRad) => AxisAngle(Vectors.Forward(), rollRad);

    public static Quaternion YawPitchRoll(float yawRad, float pitchRad, float rollRad)
    {
        Quaternion qy = Yaw(yawRad);
        Quaternion qx = Pitch(pitchRad);
        Quaternion qz = Roll(rollRad);
        return Multiply(Multiply(qy, qx), qz);
    }

    public static Quaternion EulerDegToQuat(Vector3 eulerDeg)
    {
        float yaw = eulerDeg.y * MathLite.Deg2Rad;
        float pitch = eulerDeg.x * MathLite.Deg2Rad;
        float roll = eulerDeg.z * MathLite.Deg2Rad;
        return YawPitchRoll(yaw, pitch, roll);
    }

    // Extensión para Quaternion dot product
    public static float DotProduct(Quaternion a, Quaternion b)
    {
        return a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w;
    }


}
