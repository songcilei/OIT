using UnityEngine;

namespace CameraReverseTool.Editor
{
    internal enum TwoPlaneSharedEdgeMode
    {
        Vertical,
        Horizontal
    }

    internal readonly struct CameraReverseParameters
    {
        public CameraReverseParameters(Vector3 position, Quaternion rotation, float verticalFov)
        {
            Position = position;
            Rotation = rotation;
            VerticalFov = Mathf.Clamp(verticalFov, 10f, 120f);
        }

        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
        public float VerticalFov { get; }
    }

    internal static class CameraReverseGeometry
    {
        public static Vector3[] CreateTwoPlanePoints(float firstPlaneWidth, float sharedHeight, float secondPlaneDepth, TwoPlaneSharedEdgeMode sharedEdgeMode)
        {
            firstPlaneWidth = Mathf.Max(0.0001f, firstPlaneWidth);
            sharedHeight = Mathf.Max(0.0001f, sharedHeight);
            secondPlaneDepth = Mathf.Max(0.0001f, secondPlaneDepth);

            if (sharedEdgeMode == TwoPlaneSharedEdgeMode.Horizontal)
            {
                float x = firstPlaneWidth * 0.5f;
                return new[]
                {
                    new Vector3(-x, 0f, 0f),
                    new Vector3(x, 0f, 0f),
                    new Vector3(x, sharedHeight, 0f),
                    new Vector3(-x, sharedHeight, 0f),
                    new Vector3(-x, 0f, 0f),
                    new Vector3(x, 0f, 0f),
                    new Vector3(x, 0f, secondPlaneDepth),
                    new Vector3(-x, 0f, secondPlaneDepth)
                };
            }

            float y = sharedHeight * 0.5f;
            return new[]
            {
                new Vector3(0f, -y, 0f),
                new Vector3(firstPlaneWidth, -y, 0f),
                new Vector3(firstPlaneWidth, y, 0f),
                new Vector3(0f, y, 0f),
                new Vector3(0f, -y, 0f),
                new Vector3(0f, -y, secondPlaneDepth),
                new Vector3(0f, y, secondPlaneDepth),
                new Vector3(0f, y, 0f)
            };
        }

        public static Vector3[] CreateCubePoints(float width, float height, float depth)
        {
            width = Mathf.Max(0.0001f, width);
            height = Mathf.Max(0.0001f, height);
            depth = Mathf.Max(0.0001f, depth);
            float x = width * 0.5f;
            float y = height * 0.5f;
            float z = depth * 0.5f;
            return new[]
            {
                new Vector3(-x, -y, -z),
                new Vector3(x, -y, -z),
                new Vector3(x, y, -z),
                new Vector3(-x, y, -z),
                new Vector3(-x, -y, z),
                new Vector3(x, -y, z),
                new Vector3(x, y, z),
                new Vector3(-x, y, z)
            };
        }
    }

    internal static class CameraReverseProjection
    {
        public static bool TryProject(Vector3 worldPoint, CameraReverseParameters parameters, float aspect, out Vector2 normalized)
        {
            aspect = Mathf.Max(0.0001f, aspect);
            Vector3 cameraSpace = Quaternion.Inverse(parameters.Rotation) * (worldPoint - parameters.Position);
            if (cameraSpace.z <= 0.0001f)
            {
                normalized = default;
                return false;
            }

            float vertical = Mathf.Tan(parameters.VerticalFov * Mathf.Deg2Rad * 0.5f);
            float horizontal = vertical * aspect;
            normalized = new Vector2(
                0.5f + cameraSpace.x / (cameraSpace.z * horizontal * 2f),
                0.5f + cameraSpace.y / (cameraSpace.z * vertical * 2f));
            return true;
        }
    }

    internal readonly struct CameraReverseSolveResult
    {
        public CameraReverseSolveResult(CameraReverseParameters parameters, float averageNormalizedError, float maxNormalizedError, bool success)
        {
            Parameters = parameters;
            AverageNormalizedError = averageNormalizedError;
            MaxNormalizedError = maxNormalizedError;
            Success = success;
        }

        public CameraReverseParameters Parameters { get; }
        public float AverageNormalizedError { get; }
        public float MaxNormalizedError { get; }
        public bool Success { get; }
    }

    internal static class CameraReverseSolver
    {
        public static CameraReverseSolveResult Solve(Vector3[] worldPoints, Vector2[] imagePoints, CameraReverseParameters initial, float aspect)
        {
            if (worldPoints == null || imagePoints == null || worldPoints.Length != imagePoints.Length || worldPoints.Length < 4)
            {
                return new CameraReverseSolveResult(initial, float.PositiveInfinity, float.PositiveInfinity, false);
            }

            CameraReverseParameters best = initial;
            float bestError = ComputeAverageError(worldPoints, imagePoints, best, aspect, out float bestMax);
            float[] positionSteps = { 2f, 1f, 0.5f, 0.25f, 0.1f, 0.05f, 0.02f };
            float[] rotationSteps = { 12f, 6f, 3f, 1.5f, 0.75f, 0.25f };
            float[] fovSteps = { 12f, 6f, 3f, 1.5f, 0.75f, 0.25f };

            for (int pass = 0; pass < 8; pass++)
            {
                bool improved = false;
                improved |= ImprovePosition(worldPoints, imagePoints, aspect, ref best, ref bestError, ref bestMax, positionSteps[Mathf.Min(pass, positionSteps.Length - 1)]);
                improved |= ImproveRotation(worldPoints, imagePoints, aspect, ref best, ref bestError, ref bestMax, rotationSteps[Mathf.Min(pass, rotationSteps.Length - 1)]);
                improved |= ImproveFov(worldPoints, imagePoints, aspect, ref best, ref bestError, ref bestMax, fovSteps[Mathf.Min(pass, fovSteps.Length - 1)]);
                if (!improved && pass >= positionSteps.Length - 1)
                {
                    break;
                }
            }

            return new CameraReverseSolveResult(best, bestError, bestMax, float.IsFinite(bestError));
        }

        public static float ComputeAverageError(Vector3[] worldPoints, Vector2[] imagePoints, CameraReverseParameters parameters, float aspect, out float maxError)
        {
            float total = 0f;
            maxError = 0f;
            for (int i = 0; i < worldPoints.Length; i++)
            {
                float error = 10f;
                if (CameraReverseProjection.TryProject(worldPoints[i], parameters, aspect, out Vector2 projected))
                {
                    error = Vector2.Distance(projected, imagePoints[i]);
                }

                total += error;
                maxError = Mathf.Max(maxError, error);
            }

            return total / Mathf.Max(1, worldPoints.Length);
        }

        private static bool ImprovePosition(Vector3[] worldPoints, Vector2[] imagePoints, float aspect, ref CameraReverseParameters best, ref float bestError, ref float bestMax, float step)
        {
            bool improved = false;
            Vector3[] directions = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            foreach (Vector3 direction in directions)
            {
                var candidate = new CameraReverseParameters(best.Position + direction * step, best.Rotation, best.VerticalFov);
                improved |= TryAccept(worldPoints, imagePoints, aspect, candidate, ref best, ref bestError, ref bestMax);
            }

            return improved;
        }

        private static bool ImproveRotation(Vector3[] worldPoints, Vector2[] imagePoints, float aspect, ref CameraReverseParameters best, ref float bestError, ref float bestMax, float step)
        {
            bool improved = false;
            Vector3 euler = best.Rotation.eulerAngles;
            Vector3[] deltas =
            {
                new Vector3(step, 0f, 0f), new Vector3(-step, 0f, 0f),
                new Vector3(0f, step, 0f), new Vector3(0f, -step, 0f),
                new Vector3(0f, 0f, step), new Vector3(0f, 0f, -step)
            };

            foreach (Vector3 delta in deltas)
            {
                var candidate = new CameraReverseParameters(best.Position, Quaternion.Euler(euler + delta), best.VerticalFov);
                improved |= TryAccept(worldPoints, imagePoints, aspect, candidate, ref best, ref bestError, ref bestMax);
            }

            return improved;
        }

        private static bool ImproveFov(Vector3[] worldPoints, Vector2[] imagePoints, float aspect, ref CameraReverseParameters best, ref float bestError, ref float bestMax, float step)
        {
            bool improved = false;
            var lower = new CameraReverseParameters(best.Position, best.Rotation, best.VerticalFov - step);
            var higher = new CameraReverseParameters(best.Position, best.Rotation, best.VerticalFov + step);
            improved |= TryAccept(worldPoints, imagePoints, aspect, lower, ref best, ref bestError, ref bestMax);
            improved |= TryAccept(worldPoints, imagePoints, aspect, higher, ref best, ref bestError, ref bestMax);
            return improved;
        }

        private static bool TryAccept(Vector3[] worldPoints, Vector2[] imagePoints, float aspect, CameraReverseParameters candidate, ref CameraReverseParameters best, ref float bestError, ref float bestMax)
        {
            float error = ComputeAverageError(worldPoints, imagePoints, candidate, aspect, out float max);
            if (error >= bestError)
            {
                return false;
            }

            best = candidate;
            bestError = error;
            bestMax = max;
            return true;
        }
    }
}
