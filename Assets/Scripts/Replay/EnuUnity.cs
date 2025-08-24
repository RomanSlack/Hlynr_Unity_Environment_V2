using UnityEngine;

/// Utilities for ENU <-> Unity conversion and robust quaternion/basis handling.
namespace Replay
{
    public static class EnuUnity
    {
        // ENU (x,y,z) -> Unity (x,z,y)
        public static Vector3 ENUtoUnity(Vector3 v) => new Vector3(v.x, v.z, v.y);

        // Unity (x,y,z) -> ENU (x,z,y)
        public static Vector3 UnityToENU(Vector3 v) => new Vector3(v.x, v.z, v.y);

        // Build a Unity quaternion from three orthonormal axes (columns) in Unity coords.
        public static Quaternion QuaternionFromBasis(Vector3 right, Vector3 up, Vector3 fwd)
        {
            // Ensure orthonormal-ish
            right.Normalize(); up.Normalize(); fwd.Normalize();
            // 3x3 to quaternion (standard algorithm)
            float m00 = right.x, m01 = up.x, m02 = fwd.x;
            float m10 = right.y, m11 = up.y, m12 = fwd.y;
            float m20 = right.z, m21 = up.z, m22 = fwd.z;

            float trace = m00 + m11 + m22;
            float w, x, y, z;
            if (trace > 0f)
            {
                float s = Mathf.Sqrt(trace + 1f) * 2f;
                w = 0.25f * s;
                x = (m21 - m12) / s;
                y = (m02 - m20) / s;
                z = (m10 - m01) / s;
            }
            else if (m00 > m11 && m00 > m22)
            {
                float s = Mathf.Sqrt(1f + m00 - m11 - m22) * 2f;
                w = (m21 - m12) / s;
                x = 0.25f * s;
                y = (m01 + m10) / s;
                z = (m02 + m20) / s;
            }
            else if (m11 > m22)
            {
                float s = Mathf.Sqrt(1f + m11 - m00 - m22) * 2f;
                w = (m02 - m20) / s;
                x = (m01 + m10) / s;
                y = 0.25f * s;
                z = (m12 + m21) / s;
            }
            else
            {
                float s = Mathf.Sqrt(1f + m22 - m00 - m11) * 2f;
                w = (m10 - m01) / s;
                x = (m02 + m20) / s;
                y = (m12 + m21) / s;
                z = 0.25f * s;
            }
            var q = new Quaternion(x, y, z, w);
            q.Normalize();
            return q;
        }

        // Convert WORLD->BODY quaternion in ENU ([w,x,y,z]) to a Unity rotation (BODY->WORLD).
        // Steps: q_wb_ENU -> conj = q_bw_ENU. Rotate ENU axes by q_bw to get body axes in ENU.
        // Map those axes to Unity, build Unity quaternion from basis columns [right, up, forward].
        public static Quaternion UnityRotationFromEnuWorldToBody(float w, float x, float y, float z)
        {
            // ENU quaternion (as numbers); we will use conjugate to get body->world
            var q_wb = new Quaternion(x, y, z, w);
            var q_bw = new Quaternion(-q_wb.x, -q_wb.y, -q_wb.z, q_wb.w); // conjugate

            // Rotate ENU basis vectors by q_bw (body axes expressed in ENU/world)
            Vector3 ex = Rotate(q_bw, new Vector3(1, 0, 0)); // body right in ENU
            Vector3 ey = Rotate(q_bw, new Vector3(0, 1, 0)); // body up    in ENU
            Vector3 ez = Rotate(q_bw, new Vector3(0, 0, 1)); // body fwd   in ENU

            // Map ENU vectors to Unity
            Vector3 rightU = ENUtoUnity(ex);
            Vector3 upU    = ENUtoUnity(ey);
            Vector3 fwdU   = ENUtoUnity(ez);

            return QuaternionFromBasis(rightU, upU, fwdU);
        }

        // Rotate vector by quaternion: q * v * q^-1
        public static Vector3 Rotate(Quaternion q, Vector3 v)
        {
            // Using Unity's operator is fine; it's basis-agnostic math.
            return q * v;
        }

        public static bool IsFinite(Vector3 v) =>
            !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) ||
              float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));

        public static Quaternion Sane(Quaternion q)
        {
            if (float.IsNaN(q.x) || float.IsNaN(q.y) || float.IsNaN(q.z) || float.IsNaN(q.w)) return Quaternion.identity;
            float m = Mathf.Sqrt(q.x*q.x + q.y*q.y + q.z*q.z + q.w*q.w);
            if (m < 1e-8f) return Quaternion.identity;
            q.x /= m; q.y /= m; q.z /= m; q.w /= m;
            return q;
        }
    }
}
