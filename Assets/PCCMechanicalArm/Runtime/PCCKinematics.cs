using UnityEngine;

namespace PCCMechanicalArm
{
    /// <summary>Piecewise constant curvature with +Z as the straight backbone axis.</summary>
    public static class PCCKinematics
    {
        /// <param name="bend">Integrated curvature (kappaX * length, kappaY * length), radians.</param>
        /// <param name="t">Normalized arc coordinate in [0,1].</param>
        public static void EvaluateArc(Vector2 bend, float length, float t,
            out Vector3 position, out Quaternion rotation)
        {
            float angle = bend.magnitude * t;
            float a2 = angle * angle;
            float sinc, cosc, halfSinc;
            if (a2 < 1e-4f)
            {
                // Analytic limits: no division by curvature, stable at a straight pose.
                sinc = 1f - a2 / 6f + a2 * a2 / 120f;
                cosc = 0.5f - a2 / 24f + a2 * a2 / 720f;
                halfSinc = 0.5f - a2 / 48f + a2 * a2 / 3840f;
            }
            else
            {
                sinc = Mathf.Sin(angle) / angle;
                // 1-cos(a) = 2 sin(a/2)^2 avoids subtractive cancellation.
                float sh = Mathf.Sin(angle * 0.5f);
                cosc = 2f * sh * sh / a2;
                halfSinc = sh / angle;
            }
            float lateral = length * t * t * cosc;
            position = new Vector3(bend.x * lateral, bend.y * lateral, length * t * sinc);
            float qScale = t * halfSinc;
            rotation = new Quaternion(-bend.y * qScale, bend.x * qScale, 0f,
                Mathf.Cos(angle * 0.5f));
        }

        internal static bool SolveSymmetric3x3(
            float a, float b, float c, float d, float e, float f,
            Vector3 rhs, out Vector3 result)
        {
            // [a b c; b d e; c e f]. Double intermediates reduce cancellation.
            double aa = (double)d * f - (double)e * e;
            double bb = (double)c * e - (double)b * f;
            double cc = (double)b * e - (double)c * d;
            double dd = (double)a * f - (double)c * c;
            double ee = (double)b * c - (double)a * e;
            double ff = (double)a * d - (double)b * b;
            double det = a * aa + b * bb + c * cc;
            if (System.Math.Abs(det) < 1e-20)
            {
                result = Vector3.zero;
                return false;
            }
            result = new Vector3(
                (float)((aa * rhs.x + bb * rhs.y + cc * rhs.z) / det),
                (float)((bb * rhs.x + dd * rhs.y + ee * rhs.z) / det),
                (float)((cc * rhs.x + ee * rhs.y + ff * rhs.z) / det));
            return true;
        }
    }

    [System.Serializable]
    public sealed class PCCSection
    {
        [Min(0.02f)] public float minLength = 0.55f;
        [Min(0.02f)] public float maxLength = 1.65f;
        [Min(0.02f)] public float initialLength = 1.05f;
        [Range(1f, 175f)] public float maxBendDegrees = 140f;
        public Vector2 initialBendDegrees;

        public PCCSection() { }
        public PCCSection(float min, float max, float initial, Vector2 bend)
        {
            minLength = min; maxLength = max;
            initialLength = initial; initialBendDegrees = bend;
        }

        internal void Sanitize()
        {
            minLength = Mathf.Max(0.02f, minLength);
            maxLength = Mathf.Max(minLength, maxLength);
            initialLength = Mathf.Clamp(initialLength, minLength, maxLength);
            maxBendDegrees = Mathf.Clamp(maxBendDegrees, 1f, 175f);
            initialBendDegrees = Vector2.ClampMagnitude(initialBendDegrees, maxBendDegrees);
        }
    }

    /// <summary>
    /// Position-only, bounded DLS IK. All coordinates/lengths are arm-local.
    /// One section has two normalized bending parameters plus normalized extension.
    /// Warm-started; no managed allocations inside Solve/Advance/Evaluate.
    /// </summary>
    public sealed class PCCSolver
    {
        private readonly int count;
        private readonly float[] minLength, lengthRange, maxBend;
        private readonly float[] goal, current, initial, candidate, delta;
        private readonly Vector3[] jacobian;
        private readonly float characteristicLength;

        public int SectionCount => count;
        public float MaximumLength => characteristicLength;
        public float LastGoalError { get; private set; }
        public int LastIterations { get; private set; }

        public PCCSolver(PCCSection[] sections)
        {
            if (sections == null || sections.Length == 0)
                throw new System.ArgumentException("At least one PCC section is required.");
            count = sections.Length;
            minLength = new float[count]; lengthRange = new float[count]; maxBend = new float[count];
            goal = new float[count * 3]; current = new float[count * 3];
            initial = new float[count * 3]; candidate = new float[count * 3];
            delta = new float[count * 3]; jacobian = new Vector3[count * 3];
            float total = 0f;
            for (int i = 0; i < count; i++)
            {
                if (sections[i] == null) throw new System.ArgumentException("Null PCC section.");
                sections[i].Sanitize();
                PCCSection s = sections[i];
                minLength[i] = s.minLength;
                lengthRange[i] = s.maxLength - s.minLength;
                maxBend[i] = s.maxBendDegrees * Mathf.Deg2Rad;
                goal[i * 3] = s.initialBendDegrees.x / s.maxBendDegrees;
                goal[i * 3 + 1] = s.initialBendDegrees.y / s.maxBendDegrees;
                goal[i * 3 + 2] = lengthRange[i] > 1e-6f
                    ? (s.initialLength - s.minLength) / lengthRange[i] : 0f;
                total += s.maxLength;
            }
            characteristicLength = Mathf.Max(total, 0.01f);
            Copy(goal, current); Copy(goal, initial);
        }

        public void ResetPose()
        {
            Copy(initial, goal); Copy(initial, current);
            LastGoalError = 0f; LastIterations = 0;
        }

        public void SetSectionPose(int index, Vector2 bendRadians, float length, bool immediate = true)
        {
            if (index < 0 || index >= count) throw new System.ArgumentOutOfRangeException(nameof(index));
            int j = index * 3;
            goal[j] = bendRadians.x / maxBend[index];
            goal[j + 1] = bendRadians.y / maxBend[index];
            goal[j + 2] = lengthRange[index] > 1e-6f
                ? (length - minLength[index]) / lengthRange[index] : 0f;
            Project(goal);
            if (immediate)
                for (int k = 0; k < 3; k++) current[j + k] = goal[j + k];
        }

        public void GetSectionPose(int index, out Vector2 bendRadians, out float length)
        {
            int j = index * 3;
            bendRadians = new Vector2(current[j], current[j + 1]) * maxBend[index];
            length = minLength[index] + lengthRange[index] * current[j + 2];
        }

        public Vector3 EvaluateTip(bool useGoal = false)
        {
            Quaternion unused;
            return Evaluate(useGoal ? goal : current, count - 1, 1f, out unused);
        }

        public void EvaluatePose(int sectionIndex, float t, out Vector3 position, out Quaternion rotation)
        {
            if (sectionIndex < 0 || sectionIndex >= count)
                throw new System.ArgumentOutOfRangeException(nameof(sectionIndex));
            position = Evaluate(current, sectionIndex, Mathf.Clamp01(t), out rotation);
        }

        private Vector3 Evaluate(float[] q, int sectionIndex, float t, out Quaternion rotation)
        {
            Vector3 p = Vector3.zero;
            Quaternion r = Quaternion.identity;
            for (int i = 0; i <= sectionIndex; i++)
            {
                int j = i * 3;
                Vector2 bend = new Vector2(q[j], q[j + 1]) * maxBend[i];
                float length = minLength[i] + lengthRange[i] * q[j + 2];
                Vector3 dp; Quaternion dr;
                PCCKinematics.EvaluateArc(bend, length, i == sectionIndex ? t : 1f, out dp, out dr);
                p += r * dp;
                r = r * dr;
            }
            rotation = r;
            return p;
        }

        private Vector3 GoalTip()
        {
            Quaternion unused;
            return Evaluate(goal, count - 1, 1f, out unused);
        }

        public void Solve(Vector3 target, int iterations, float tolerance, float damping,
            float maxParameterStep, bool allowExtension)
        {
            if (!allowExtension)
                for (int i = 0; i < count; i++) goal[i * 3 + 2] = current[i * 3 + 2];
            const float h = 0.001f;
            float invScale = 1f / characteristicLength;
            float tol2 = tolerance * tolerance;
            damping = Mathf.Max(0.001f, damping);
            LastIterations = 0;
            for (int iteration = 0; iteration < iterations; iteration++)
            {
                Vector3 p = GoalTip();
                Vector3 error = target - p;
                float oldCost = error.sqrMagnitude;
                if (oldCost <= tol2) break;
                LastIterations++;
                for (int j = 0; j < goal.Length; j++)
                {
                    if (j % 3 == 2 && (!allowExtension || lengthRange[j / 3] < 1e-6f))
                    {
                        jacobian[j] = Vector3.zero;
                        continue;
                    }
                    // Unconstrained central differences; constraints are applied to trial poses.
                    float save = goal[j];
                    goal[j] = save + h; Vector3 plus = GoalTip();
                    goal[j] = save - h; Vector3 minus = GoalTip();
                    goal[j] = save;
                    jacobian[j] = (plus - minus) * (invScale / (2f * h));
                }
                float a = damping * damping, d = a, f = a, b = 0f, c = 0f, e = 0f;
                for (int j = 0; j < jacobian.Length; j++)
                {
                    Vector3 v = jacobian[j];
                    a += v.x * v.x; b += v.x * v.y; c += v.x * v.z;
                    d += v.y * v.y; e += v.y * v.z; f += v.z * v.z;
                }
                Vector3 z;
                if (!PCCKinematics.SolveSymmetric3x3(a, b, c, d, e, f, error * invScale, out z)) break;
                float largest = 0f;
                for (int j = 0; j < delta.Length; j++)
                {
                    delta[j] = Vector3.Dot(jacobian[j], z);
                    largest = Mathf.Max(largest, Mathf.Abs(delta[j]));
                }
                float step = largest > maxParameterStep ? maxParameterStep / largest : 1f;
                bool accepted = false;
                for (int attempt = 0; attempt < 6; attempt++)
                {
                    for (int j = 0; j < goal.Length; j++) candidate[j] = goal[j] + delta[j] * step;
                    Project(candidate);
                    Quaternion unused;
                    float cost = (target - Evaluate(candidate, count - 1, 1f, out unused)).sqrMagnitude;
                    if (cost < oldCost - 1e-10f)
                    {
                        Copy(candidate, goal); accepted = true; break;
                    }
                    step *= 0.5f;
                }
                // A straight, minimum-length arm can have a zero first-order bending gradient.
                // A bounded deterministic perturbation provides a finite-curvature escape.
                if (!accepted && !TryEscape(target, oldCost)) break;
            }
            LastGoalError = Vector3.Distance(target, GoalTip());
        }

        private bool TryEscape(Vector3 target, float oldCost)
        {
            float bestCost = oldCost;
            int bestAxis = -1, bestSign = 0, bestPattern = 0;
            for (int axis = 0; axis < 2; axis++)
            for (int sign = -1; sign <= 1; sign += 2)
            for (int pattern = 0; pattern < 2; pattern++)
            {
                Copy(goal, candidate);
                ApplyEscape(candidate, axis, sign, pattern);
                Project(candidate);
                Quaternion unused;
                float cost = (target - Evaluate(candidate, count - 1, 1f, out unused)).sqrMagnitude;
                if (cost < bestCost - 1e-9f)
                {
                    bestCost = cost; bestAxis = axis; bestSign = sign; bestPattern = pattern;
                }
            }
            if (bestAxis < 0) return false;
            ApplyEscape(goal, bestAxis, bestSign, bestPattern);
            Project(goal);
            return true;
        }

        private void ApplyEscape(float[] q, int axis, int sign, int pattern)
        {
            for (int i = 0; i < count; i++)
            {
                float coefficient = pattern == 0 ? 1f : (i % 2 == 0 ? 1f : -2f);
                q[i * 3 + axis] += sign * 0.08f * coefficient;
            }
        }

        public void Advance(float dt, float bendSpeedRadians, float extensionSpeed)
        {
            dt = Mathf.Max(0f, dt);
            for (int i = 0; i < count; i++)
            {
                int j = i * 3;
                Vector2 bend = new Vector2(current[j], current[j + 1]);
                Vector2 wanted = new Vector2(goal[j], goal[j + 1]);
                bend = Vector2.MoveTowards(bend, wanted, bendSpeedRadians * dt / maxBend[i]);
                current[j] = bend.x; current[j + 1] = bend.y;
                float speed = lengthRange[i] > 1e-6f ? extensionSpeed / lengthRange[i] : 0f;
                current[j + 2] = Mathf.MoveTowards(current[j + 2], goal[j + 2], speed * dt);
            }
        }

        private void Project(float[] q)
        {
            for (int i = 0; i < count; i++)
            {
                int j = i * 3;
                float r2 = q[j] * q[j] + q[j + 1] * q[j + 1];
                if (r2 > 1f)
                {
                    float inverse = 1f / Mathf.Sqrt(r2);
                    q[j] *= inverse; q[j + 1] *= inverse;
                }
                q[j + 2] = lengthRange[i] < 1e-6f ? 0f : Mathf.Clamp01(q[j + 2]);
            }
        }

        private static void Copy(float[] source, float[] destination)
        {
            for (int i = 0; i < source.Length; i++) destination[i] = source[i];
        }
    }
}

