using System.IO;
using Unbounded.App;
using Unbounded.Track;
using Unbounded.Vehicle;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Unbounded.Editor
{
    /// <summary>
    /// Builds the main scene: the generated circuit, one drivable car on the
    /// start line, and a chase camera that keeps the barriers out of the lens.
    /// Run from the menu or in batch mode:
    /// Unity -batchmode -quit -projectPath . -executeMethod Unbounded.Editor.MainSceneBuilder.Build
    /// </summary>
    public static class MainSceneBuilder
    {
        private const string MainScenePath = "Assets/_Project/Scenes/Main.unity";
        private const string DrivingActionsPath = "Assets/_Project/Input/Driving.inputactions";
        private const string MaterialsFolder = "Assets/_Project/Art/Materials";
        private const string TexturesFolder = "Assets/_Project/Art/Textures";
        private const string PhysicsFolder = "Assets/_Project/Art/Physics";

        private const float GroundSize = 600f;
        private static readonly Vector3 GroundCentre = new Vector3(-125f, 0f, 25f);

        /// <summary>How far past the start/finish seam the car begins.</summary>
        private const float SpawnDistance = 1.5f;

        [MenuItem("Unbounded/Build Main Scene")]
        public static void Build()
        {
            InputActionAsset actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(DrivingActionsPath);
            if (actions == null)
            {
                throw new IOException("Missing input actions at " + DrivingActionsPath);
            }

            EnsureFolder(MaterialsFolder);
            EnsureFolder(TexturesFolder);
            EnsureFolder(PhysicsFolder);

            Material grassMaterial = CreateMaterial(
                Path.Combine(MaterialsFolder, "Grass.mat"),
                new Color(0.24f, 0.36f, 0.18f),
                0.02f,
                CreateNoiseTexture(Path.Combine(TexturesFolder, "Grass.png"), new Color(0.16f, 0.27f, 0.12f),
                    new Color(0.31f, 0.43f, 0.21f), 7f),
                new Vector2(GroundSize * 0.1f, GroundSize * 0.1f));
            Material asphaltMaterial = CreateMaterial(
                Path.Combine(MaterialsFolder, "Asphalt.mat"),
                new Color(0.21f, 0.21f, 0.22f),
                0.12f,
                CreateNoiseTexture(Path.Combine(TexturesFolder, "Asphalt.png"), new Color(0.14f, 0.14f, 0.15f),
                    new Color(0.26f, 0.26f, 0.27f), 24f),
                Vector2.one);
            Material kerbMaterial = CreateMaterial(
                Path.Combine(MaterialsFolder, "Kerb.mat"),
                Color.white,
                0.18f,
                CreateStripeTexture(Path.Combine(TexturesFolder, "Kerb.png"),
                    new Color(0.82f, 0.13f, 0.11f), new Color(0.92f, 0.92f, 0.89f), 8),
                Vector2.one);
            Material barrierMaterial = CreateMaterial(
                Path.Combine(MaterialsFolder, "Barrier.mat"),
                new Color(0.82f, 0.82f, 0.80f),
                0.22f,
                CreateStripeTexture(Path.Combine(TexturesFolder, "Barrier.png"),
                    new Color(0.66f, 0.10f, 0.09f), new Color(0.86f, 0.86f, 0.83f), 4),
                Vector2.one);
            Material startLineMaterial = CreateMaterial(
                Path.Combine(MaterialsFolder, "StartLine.mat"),
                new Color(0.9f, 0.9f, 0.85f),
                0.2f,
                null,
                Vector2.one);
            Material bodyMaterial = CreateMaterial(
                Path.Combine(MaterialsFolder, "CarBody.mat"),
                new Color(0.72f, 0.08f, 0.07f),
                0.55f,
                null,
                Vector2.one);
            Material wheelMaterial = CreateMaterial(
                Path.Combine(MaterialsFolder, "CarWheel.mat"),
                new Color(0.035f, 0.035f, 0.04f),
                0.15f,
                null,
                Vector2.one);

            PhysicsMaterial frictionless = CreateFrictionlessMaterial(
                Path.Combine(PhysicsFolder, "Frictionless.physicMaterial"));

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateCameraAndLight();
            CreateGround(grassMaterial, frictionless);
            TrackRoot track = CreateTrack(
                asphaltMaterial,
                kerbMaterial,
                barrierMaterial,
                startLineMaterial,
                frictionless);
            GameObject car = CreateCar(
                bodyMaterial,
                wheelMaterial,
                startLineMaterial,
                actions,
                frictionless,
                track);
            CreateChaseCamera(car.transform);

            if (!EditorSceneManager.SaveScene(scene, MainScenePath))
            {
                throw new IOException("Failed to save " + MainScenePath);
            }

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(MainScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("Built the main scene with a " + track.Path.Length.ToString("0") + " metre circuit.");
        }

        private static void CreateCameraAndLight()
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 2.6f, -7.5f);
            cameraObject.transform.rotation = Quaternion.Euler(8f, 0f, 0f);
            cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            CinemachineBrain brain = cameraObject.AddComponent<CinemachineBrain>();

            // The car is a Rigidbody, so sample the camera on the physics clock:
            // it keeps the rig glued to the car no matter the render rate.
            brain.UpdateMethod = CinemachineBrain.UpdateMethods.FixedUpdate;

            var lightObject = new GameObject("Directional Light");
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.color = new Color(1f, 0.96f, 0.88f);
            light.shadows = LightShadows.Soft;
        }

        private static void CreateGround(Material material, PhysicsMaterial physicMaterial)
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Grass Field";
            ground.transform.position = GroundCentre;
            ground.transform.localScale = new Vector3(GroundSize * 0.1f, 1f, GroundSize * 0.1f);
            ground.GetComponent<MeshRenderer>().sharedMaterial = material;
            ground.GetComponent<Collider>().sharedMaterial = physicMaterial;
        }

        private static TrackRoot CreateTrack(
            Material roadMaterial,
            Material kerbMaterial,
            Material barrierMaterial,
            Material startLineMaterial,
            PhysicsMaterial physicMaterial)
        {
            var trackObject = new GameObject("Track");
            trackObject.transform.position = Vector3.zero;

            var track = trackObject.AddComponent<TrackRoot>();
            track.Configure(
                TrackLayout.Circuit(),
                TrackShape.Default,
                roadMaterial,
                kerbMaterial,
                barrierMaterial,
                startLineMaterial,
                physicMaterial);

            if (track.Path == null)
            {
                throw new IOException("The circuit failed to build from its control points.");
            }

            return track;
        }

        private static GameObject CreateCar(
            Material bodyMaterial,
            Material wheelMaterial,
            Material accentMaterial,
            InputActionAsset actions,
            PhysicsMaterial physicMaterial,
            TrackRoot track)
        {
            Pose spawn = track.SamplePose(SpawnDistance);

            var car = new GameObject("Player Car");
            car.tag = "Player";
            car.transform.SetPositionAndRotation(spawn.position + Vector3.up * 0.05f, spawn.rotation);

            var body = car.AddComponent<Rigidbody>();
            body.mass = 1200f;
            body.linearDamping = 0.02f;
            body.angularDamping = 0.05f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            body.centerOfMass = new Vector3(0f, 0.35f, 0f);
            body.maxAngularVelocity = 20f;

            var collider = car.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.5f, 0f);
            collider.size = new Vector3(1.8f, 1f, 4f);
            collider.sharedMaterial = physicMaterial;

            var controller = car.AddComponent<CarController>();
            controller.Config = HandlingConfig.Default;

            var surface = car.AddComponent<CarSurfaceAdapter>();
            surface.Configure(track);

            AddVisual(car.transform, PrimitiveType.Cube, "Body", new Vector3(0f, 0.5f, 0f),
                new Vector3(1.8f, 1f, 4f), Quaternion.identity, bodyMaterial);
            AddVisual(car.transform, PrimitiveType.Cube, "Cabin", new Vector3(0f, 1.15f, -0.15f),
                new Vector3(1.3f, 0.5f, 1.8f), Quaternion.identity, bodyMaterial);
            AddVisual(car.transform, PrimitiveType.Cube, "Front Marker", new Vector3(0f, 0.75f, 2.03f),
                new Vector3(1.4f, 0.22f, 0.08f), Quaternion.identity, accentMaterial);

            Quaternion wheelRotation = Quaternion.Euler(0f, 0f, 90f);
            AddVisual(car.transform, PrimitiveType.Cylinder, "Wheel FL", new Vector3(-0.9f, 0.35f, 1.25f),
                new Vector3(0.7f, 0.125f, 0.7f), wheelRotation, wheelMaterial);
            AddVisual(car.transform, PrimitiveType.Cylinder, "Wheel FR", new Vector3(0.9f, 0.35f, 1.25f),
                new Vector3(0.7f, 0.125f, 0.7f), wheelRotation, wheelMaterial);
            AddVisual(car.transform, PrimitiveType.Cylinder, "Wheel RL", new Vector3(-0.9f, 0.35f, -1.25f),
                new Vector3(0.7f, 0.125f, 0.7f), wheelRotation, wheelMaterial);
            AddVisual(car.transform, PrimitiveType.Cylinder, "Wheel RR", new Vector3(0.9f, 0.35f, -1.25f),
                new Vector3(0.7f, 0.125f, 0.7f), wheelRotation, wheelMaterial);

            var input = car.AddComponent<PlayerInputReader>();
            input.Configure(actions, controller);
            EditorUtility.SetDirty(input);

            return car;
        }

        private static void CreateChaseCamera(Transform target)
        {
            var cameraObject = new GameObject("Chase Camera");
            var camera = cameraObject.AddComponent<CinemachineCamera>();
            camera.Target = new CameraTarget { TrackingTarget = target };
            camera.Lens.FieldOfView = 62f;
            camera.Priority = 10;

            var follow = cameraObject.AddComponent<CinemachineThirdPersonFollow>();
            follow.CameraDistance = 7.5f;
            follow.ShoulderOffset = new Vector3(0f, 1.4f, 0f);
            follow.VerticalArmLength = 1f;
            follow.CameraSide = 0.5f;
            follow.Damping = new Vector3(0.08f, 0.2f, 0.15f);

            // Pull the camera in instead of letting a Barrier come between it and
            // the car. The car carries the Player tag so it never occludes itself.
            follow.AvoidObstacles = new CinemachineThirdPersonFollow.ObstacleSettings
            {
                Enabled = true,
                CollisionFilter = ~0,
                IgnoreTag = "Player",
                CameraRadius = 0.3f,
                DampingIntoCollision = 0.05f,
                DampingFromCollision = 0.35f
            };
        }

        private static void AddVisual(
            Transform parent,
            PrimitiveType primitive,
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            Quaternion localRotation,
            Material material)
        {
            GameObject visual = GameObject.CreatePrimitive(primitive);
            visual.name = name;
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = localPosition;
            visual.transform.localRotation = localRotation;
            visual.transform.localScale = localScale;
            visual.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static Material CreateMaterial(
            string path,
            Color color,
            float smoothness,
            Texture2D texture,
            Vector2 tiling)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                throw new IOException("URP Lit shader is unavailable.");
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            material.color = color;
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", 0f);

            if (texture != null)
            {
                material.SetTexture("_BaseMap", texture);
                material.SetTextureScale("_BaseMap", tiling);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static PhysicsMaterial CreateFrictionlessMaterial(string path)
        {
            var material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (material == null)
            {
                material = new PhysicsMaterial("Frictionless");
                AssetDatabase.CreateAsset(material, path);
            }

            material.dynamicFriction = 0f;
            material.staticFriction = 0f;
            material.bounciness = 0f;
            material.frictionCombine = PhysicsMaterialCombine.Minimum;
            material.bounceCombine = PhysicsMaterialCombine.Minimum;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Texture2D CreateNoiseTexture(string path, Color low, Color high, float frequency)
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float offset = 0f;
            foreach (char character in path)
            {
                offset += character;
            }

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = offset + (float)x / size * frequency;
                    float v = offset + (float)y / size * frequency;
                    float noise = Mathf.PerlinNoise(u, v);
                    texture.SetPixel(x, y, Color.Lerp(low, high, noise));
                }
            }

            texture.Apply();
            return SaveTexture(path, texture);
        }

        private static Texture2D CreateStripeTexture(string path, Color stripe, Color baseColor, int stripeCount)
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool painted = (x * stripeCount / size) % 2 == 0;
                    texture.SetPixel(x, y, painted ? stripe : baseColor);
                }
            }

            texture.Apply();
            return SaveTexture(path, texture);
        }

        private static Texture2D SaveTexture(string path, Texture2D texture)
        {
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static void EnsureFolder(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                Directory.CreateDirectory(path);
                AssetDatabase.Refresh();
            }
        }
    }
}
