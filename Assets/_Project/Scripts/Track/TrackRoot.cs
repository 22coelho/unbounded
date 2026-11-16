using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unbounded.Track
{
    /// <summary>
    /// Owns the generated Track. The authored control points are the only data
    /// saved in the scene; the road, Kerbs, and Barriers are rebuilt from them
    /// whenever the component wakes or its shape changes.
    /// </summary>
    [ExecuteAlways]
    public sealed class TrackRoot : MonoBehaviour
    {
        [SerializeField] private Vector3[] _controlPoints = new Vector3[0];
        [SerializeField] private TrackShape _shape = TrackShape.Default;
        [SerializeField] private Material _roadMaterial;
        [SerializeField] private Material _kerbMaterial;
        [SerializeField] private Material _barrierMaterial;
        [SerializeField] private Material _startLineMaterial;
        [SerializeField] private PhysicsMaterial _surfacePhysicsMaterial;
        [SerializeField] private int _checkpointCount = CheckpointRoute.DefaultCount;

        private readonly List<Mesh> _meshes = new List<Mesh>();
        private Transform _generated;
        private bool _rebuilding;

        public TrackPath Path { get; private set; }

        /// <summary>The AI Racing line, derived from the Centreline and the Road width.</summary>
        public RacingLine RacingLine { get; private set; }

        /// <summary>The ordered Checkpoints of a Lap, derived from the Centreline.</summary>
        public CheckpointRoute Checkpoints { get; private set; }

        public TrackShape Shape => _shape;

        public void Configure(
            Vector3[] controlPoints,
            TrackShape shape,
            Material roadMaterial,
            Material kerbMaterial,
            Material barrierMaterial,
            Material startLineMaterial,
            PhysicsMaterial surfacePhysicsMaterial)
        {
            _controlPoints = controlPoints;
            _shape = shape;
            _roadMaterial = roadMaterial;
            _kerbMaterial = kerbMaterial;
            _barrierMaterial = barrierMaterial;
            _startLineMaterial = startLineMaterial;
            _surfacePhysicsMaterial = surfacePhysicsMaterial;
            Rebuild();
        }

        /// <summary>The position and heading of a car at a distance along the Track.</summary>
        public Pose SamplePose(float distance)
        {
            return SamplePose(distance, 0f);
        }

        /// <summary>
        /// The pose of a car at a distance along the Track and a lateral offset,
        /// which is how the Grid is laid out either side of the Centreline.
        /// </summary>
        public Pose SamplePose(float distance, float lateralOffset)
        {
            if (Path == null)
            {
                throw new InvalidOperationException("The Track has not been built yet.");
            }

            Vector3 local = Path.SamplePosition(distance) + Path.SampleRight(distance) * lateralOffset;
            Vector3 position = transform.TransformPoint(local);
            Vector3 direction = transform.TransformDirection(Path.SampleDirection(distance));
            return new Pose(position, Quaternion.LookRotation(direction, Vector3.up));
        }

        /// <summary>Where a world position sits on the Track.</summary>
        public TrackProjection Project(Vector3 worldPosition)
        {
            if (Path == null)
            {
                throw new InvalidOperationException("The Track has not been built yet.");
            }

            return Path.Project(transform.InverseTransformPoint(worldPosition));
        }

        /// <summary>Which surface lies under a world position.</summary>
        public TrackSurface SurfaceAt(Vector3 worldPosition)
        {
            return TrackSurfaceClassifier.Classify(Project(worldPosition).LateralOffset, _shape);
        }

        public void Rebuild()
        {
            if (_rebuilding)
            {
                return;
            }

            _rebuilding = true;
            try
            {
                DestroyGenerated();

                if (_controlPoints == null || _controlPoints.Length < 3)
                {
                    Path = null;
                    RacingLine = null;
                    Checkpoints = null;
                    return;
                }

                Path = TrackPath.FromControlPoints(_controlPoints);
                RacingLine = Unbounded.Track.RacingLine.FromCentreline(Path, _shape);
                Checkpoints = new CheckpointRoute(Path.Length, Mathf.Max(1, _checkpointCount));

                var generated = new GameObject("Generated") { hideFlags = HideFlags.DontSave };
                generated.transform.SetParent(transform, false);
                _generated = generated.transform;

                AddMesh("Road", TrackMeshBuilder.BuildRoad(Path, _shape), _roadMaterial, true);
                AddMesh("Kerbs", TrackMeshBuilder.BuildKerbs(Path, _shape), _kerbMaterial, true);
                AddMesh("Barriers", TrackMeshBuilder.BuildBarriers(Path, _shape), _barrierMaterial, true);
                AddMesh("Start Line", TrackMeshBuilder.BuildStartLine(Path, _shape), _startLineMaterial, false);
            }
            finally
            {
                _rebuilding = false;
            }
        }

        private void OnEnable()
        {
            Rebuild();
        }

        private void OnValidate()
        {
            if (isActiveAndEnabled && !_rebuilding)
            {
                Rebuild();
            }
        }

        private void AddMesh(string name, TrackMeshData data, Material material, bool withCollider)
        {
            if (data.IsEmpty || _generated == null)
            {
                return;
            }

            var mesh = new Mesh { name = "Track " + name, hideFlags = HideFlags.DontSave };
            mesh.SetVertices(data.Vertices);
            mesh.SetNormals(data.Normals);
            mesh.SetUVs(0, data.Uvs);
            mesh.SetTriangles(data.Triangles, 0);
            mesh.RecalculateBounds();
            _meshes.Add(mesh);

            var part = new GameObject(name) { hideFlags = HideFlags.DontSave };
            part.transform.SetParent(_generated, false);

            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            part.AddComponent<MeshRenderer>().sharedMaterial = material;

            if (!withCollider)
            {
                return;
            }

            var meshCollider = part.AddComponent<MeshCollider>();
            meshCollider.sharedMesh = mesh;
            meshCollider.convex = false;
            if (_surfacePhysicsMaterial != null)
            {
                meshCollider.sharedMaterial = _surfacePhysicsMaterial;
            }
        }

        private void DestroyGenerated()
        {
            Transform generated = _generated != null ? _generated : transform.Find("Generated");
            _generated = null;

            if (generated != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(generated.gameObject);
                }
                else
                {
                    DestroyImmediate(generated.gameObject);
                }
            }

            foreach (Mesh mesh in _meshes)
            {
                if (mesh == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(mesh);
                }
                else
                {
                    DestroyImmediate(mesh);
                }
            }

            _meshes.Clear();
        }
    }
}
