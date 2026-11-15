using UnityEngine;

namespace Unbounded.Track
{
    /// <summary>
    /// Raw mesh arrays produced from a TrackPath. Kept free of Unity objects so
    /// the geometry can be built anywhere and handed to a MeshFilter.
    /// </summary>
    public readonly struct TrackMeshData
    {
        public TrackMeshData(Vector3[] vertices, Vector3[] normals, Vector2[] uvs, int[] triangles)
        {
            Vertices = vertices;
            Normals = normals;
            Uvs = uvs;
            Triangles = triangles;
        }

        public readonly Vector3[] Vertices;
        public readonly Vector3[] Normals;
        public readonly Vector2[] Uvs;
        public readonly int[] Triangles;

        public bool IsEmpty => Vertices == null || Vertices.Length == 0;
    }
}
