using System.Collections.Generic;
using UnityEngine;

namespace Unbounded.Track
{
    /// <summary>
    /// Builds the road, Kerbs, Barriers, and start line by sweeping a
    /// cross-section along the Centreline. Every sweep closes on itself, so the
    /// road has no seam at the start/finish line.
    /// </summary>
    public static class TrackMeshBuilder
    {
        private const float RoadUvScale = 8f;
        private const float KerbUvScale = 4f;
        private const float BarrierUvScale = 4f;
        private const float StartLineLength = 1.5f;

        private readonly struct ProfilePoint
        {
            public ProfilePoint(float lateral, float height, float normalLateral, float normalUp, float v)
            {
                Lateral = lateral;
                Height = height;
                NormalLateral = normalLateral;
                NormalUp = normalUp;
                V = v;
            }

            public readonly float Lateral;
            public readonly float Height;
            public readonly float NormalLateral;
            public readonly float NormalUp;
            public readonly float V;
        }

        public static TrackMeshData BuildRoad(TrackPath path, TrackShape shape)
        {
            var profile = new[]
            {
                new ProfilePoint(-shape.HalfWidth, shape.RoadHeight, 0f, 1f, 0f),
                new ProfilePoint(shape.HalfWidth, shape.RoadHeight, 0f, 1f, 1f)
            };

            return Build(path, shape.MeshSpacing, RoadUvScale, (profile, false));
        }

        public static TrackMeshData BuildKerbs(TrackPath path, TrackShape shape)
        {
            float length = Mathf.Sqrt(shape.KerbWidth * shape.KerbWidth + shape.KerbHeight * shape.KerbHeight);
            float lateral = shape.KerbHeight / length;
            float up = shape.KerbWidth / length;

            var left = new[]
            {
                new ProfilePoint(-shape.HalfWidth, shape.RoadHeight, lateral, up, 0f),
                new ProfilePoint(-shape.EdgeOffset, shape.RoadHeight + shape.KerbHeight, lateral, up, 1f)
            };
            var right = new[]
            {
                new ProfilePoint(shape.HalfWidth, shape.RoadHeight, -lateral, up, 0f),
                new ProfilePoint(shape.EdgeOffset, shape.RoadHeight + shape.KerbHeight, -lateral, up, 1f)
            };

            return Build(path, shape.MeshSpacing, KerbUvScale, (left, true), (right, false));
        }

        public static TrackMeshData BuildBarriers(TrackPath path, TrackShape shape)
        {
            float depth = shape.BarrierHeight * 0.25f;
            float inner = shape.BarrierOffset;
            float outer = inner + shape.BarrierThickness;

            var left = new[]
            {
                new ProfilePoint(-inner, -depth, 1f, 0f, 0f),
                new ProfilePoint(-inner, shape.BarrierHeight, 1f, 0f, 1f),
                new ProfilePoint(-outer, shape.BarrierHeight, 0f, 1f, 1f),
                new ProfilePoint(-outer, -depth, -1f, 0f, 0f)
            };
            var right = new[]
            {
                new ProfilePoint(inner, -depth, -1f, 0f, 0f),
                new ProfilePoint(inner, shape.BarrierHeight, -1f, 0f, 1f),
                new ProfilePoint(outer, shape.BarrierHeight, 0f, 1f, 1f),
                new ProfilePoint(outer, -depth, 1f, 0f, 0f)
            };

            return Build(path, shape.MeshSpacing, BarrierUvScale, (left, true), (right, false));
        }

        public static TrackMeshData BuildStartLine(TrackPath path, TrackShape shape)
        {
            Vector3 forward = path.SampleDirection(0f);
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            float halfLength = StartLineLength * 0.5f;
            float height = shape.RoadHeight + 0.004f;

            Vector3 near = path.SamplePosition(-halfLength) + Vector3.up * height;
            Vector3 far = path.SamplePosition(halfLength) + Vector3.up * height;

            var vertices = new[]
            {
                near - right * shape.HalfWidth,
                near + right * shape.HalfWidth,
                far - right * shape.HalfWidth,
                far + right * shape.HalfWidth
            };
            var normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            var uvs = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(shape.HalfWidth * 2f, 0f),
                new Vector2(0f, StartLineLength),
                new Vector2(shape.HalfWidth * 2f, StartLineLength)
            };
            var triangles = new[] { 0, 2, 1, 1, 2, 3 };

            return new TrackMeshData(vertices, normals, uvs, triangles);
        }

        private static TrackMeshData Build(
            TrackPath path,
            float spacing,
            float uvScale,
            params (ProfilePoint[] Profile, bool FlipWinding)[] ribbons)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            foreach ((ProfilePoint[] profile, bool flipWinding) in ribbons)
            {
                AppendRibbon(vertices, normals, uvs, triangles, path, spacing, uvScale, profile, flipWinding);
            }

            return new TrackMeshData(vertices.ToArray(), normals.ToArray(), uvs.ToArray(), triangles.ToArray());
        }

        private static void AppendRibbon(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<int> triangles,
            TrackPath path,
            float spacing,
            float uvScale,
            ProfilePoint[] profile,
            bool flipWinding)
        {
            int ringCount = Mathf.Max(4, Mathf.CeilToInt(path.Length / Mathf.Max(0.5f, spacing)));
            float ringStep = path.Length / ringCount;
            int profileCount = profile.Length;
            int vertexOffset = vertices.Count;

            for (int ring = 0; ring <= ringCount; ring++)
            {
                float distance = ring == ringCount ? path.Length : ring * ringStep;
                Vector3 centre = path.SamplePosition(distance);
                Vector3 right = path.SampleRight(distance);
                float u = distance / uvScale;

                for (int i = 0; i < profileCount; i++)
                {
                    ProfilePoint point = profile[i];
                    vertices.Add(centre + right * point.Lateral + Vector3.up * point.Height);
                    normals.Add((right * point.NormalLateral + Vector3.up * point.NormalUp).normalized);
                    uvs.Add(new Vector2(u, point.V));
                }
            }

            for (int ring = 0; ring < ringCount; ring++)
            {
                for (int edge = 0; edge < profileCount - 1; edge++)
                {
                    int a = vertexOffset + ring * profileCount + edge;
                    int b = vertexOffset + (ring + 1) * profileCount + edge;
                    int c = vertexOffset + (ring + 1) * profileCount + edge + 1;
                    int d = vertexOffset + ring * profileCount + edge + 1;

                    if (flipWinding)
                    {
                        triangles.Add(a);
                        triangles.Add(c);
                        triangles.Add(b);
                        triangles.Add(a);
                        triangles.Add(d);
                        triangles.Add(c);
                    }
                    else
                    {
                        triangles.Add(a);
                        triangles.Add(b);
                        triangles.Add(c);
                        triangles.Add(a);
                        triangles.Add(c);
                        triangles.Add(d);
                    }
                }
            }
        }
    }
}
