using System.IO;
using Unbounded.App;
using Unbounded.Vehicle;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Unbounded.Editor
{
    /// <summary>
    /// Builds the milestone-1 test plane: a flat ground, one drivable car, and
    /// a Cinemachine chase camera. Run from the menu or in batch mode:
    /// Unity -batchmode -quit -projectPath . -executeMethod Unbounded.Editor.TestPlaneSceneBuilder.Build
    /// </summary>
    public static class TestPlaneSceneBuilder
    {
        private const string MainScenePath = "Assets/_Project/Scenes/Main.unity";
        private const string DrivingActionsPath = "Assets/_Project/Input/Driving.inputactions";
        private const string MaterialsFolder = "Assets/_Project/Art/Materials";
        private const string TexturesFolder = "Assets/_Project/Art/Textures";
        private const string PhysicsFolder = "Assets/_Project/Art/Physics";

        [MenuItem("Unbounded/Build Test Plane Scene")]
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

            Texture2D checker = CreateCheckerTexture(Path.Combine(TexturesFolder, "TestPlaneChecker.png"));
            PhysicsMaterial frictionless = CreateFrictionlessMaterial(
                Path.Combine(PhysicsFolder, "Frictionless.physicMaterial"));
            Material groundMaterial = CreateMaterial(
                Path.Combine(MaterialsFolder, "TestPlane.mat"),
                new Color(0.32f, 0.34f, 0.37f),
                0.05f,
                checker);
            Material bodyMaterial = CreateMaterial(
                Path.Combine(MaterialsFolder, "CarBody.mat"),
                new Color(0.72f, 0.08f, 0.07f),
                0.55f,
                null);
            Material wheelMaterial = CreateMaterial(
                Path.Combine(MaterialsFolder, "CarWheel.mat"),
                new Color(0.035f, 0.035f, 0.04f),
                0.15f,
                null);
            Material lineMaterial = CreateMaterial(
                Path.Combine(MaterialsFolder, "StartLine.mat"),
                new Color(0.9f, 0.9f, 0.85f),
                0.2f,
                null);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateLighting();
            CreateGround(groundMaterial, frictionless);
            CreateStartLine(lineMaterial);
            GameObject car = CreateCar(bodyMaterial, wheelMaterial, lineMaterial, actions, frictionless);
            CreateChaseCamera(car.transform);

            if (!EditorSceneManager.SaveScene(scene, MainScenePath))
            {
                throw new IOException("Failed to save " + MainScenePath);
            }

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(MainScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("Built the test plane scene.");
        }

        private static void CreateLighting()
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 2.6f, -7.5f);
            cameraObject.transform.rotation = Quaternion.Euler(8f, 0f, 0f);
            cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<CinemachineBrain>();

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
            ground.name = "Test Plane";
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(20f, 1f, 20f);
            ground.GetComponent<MeshRenderer>().sharedMaterial = material;
            ground.GetComponent<Collider>().sharedMaterial = physicMaterial;
        }

        private static void CreateStartLine(Material material)
        {
            GameObject line = GameObject.CreatePrimitive(PrimitiveType.Cube);
            line.name = "Start Line";
            UnityEngine.Object.DestroyImmediate(line.GetComponent<Collider>());
            line.transform.position = new Vector3(0f, 0.02f, 4f);
            line.transform.localScale = new Vector3(12f, 0.02f, 0.4f);
            line.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static GameObject CreateCar(
            Material bodyMaterial,
            Material wheelMaterial,
            Material accentMaterial,
            InputActionAsset actions,
            PhysicsMaterial physicMaterial)
        {
            var car = new GameObject("Player Car");
            car.transform.position = new Vector3(0f, 0.05f, 0f);

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
            UnityEngine.Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = localPosition;
            visual.transform.localRotation = localRotation;
            visual.transform.localScale = localScale;
            visual.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static Material CreateMaterial(string path, Color color, float smoothness, Texture2D texture)
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
                material.SetTextureScale("_BaseMap", new Vector2(10f, 10f));
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

        private static Texture2D CreateCheckerTexture(string path)
        {
            const int size = 64;
            const int cell = 8;
            var light = new Color(0.31f, 0.33f, 0.36f);
            var dark = new Color(0.23f, 0.25f, 0.28f);

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    texture.SetPixel(x, y, ((x / cell) + (y / cell)) % 2 == 0 ? light : dark);
                }
            }

            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);

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
