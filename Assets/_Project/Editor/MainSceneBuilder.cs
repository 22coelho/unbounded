using System.IO;
using Unbounded.AI;
using Unbounded.App;
using Unbounded.Track;
using Unbounded.UI;
using Unbounded.Vehicle;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Unbounded.Editor
{
    /// <summary>
    /// Builds the main scene: the generated circuit dressed with the free
    /// Racing and Nature kits, one drivable car on the start line, and a chase
    /// camera that keeps the barriers out of the lens. Every material, the sky,
    /// and the post-processing stack come from <see cref="ArtPalette"/>, so the
    /// look is one deliberate choice rather than a pile of asset defaults.
    /// Run from the menu or in batch mode:
    /// Unity -batchmode -quit -projectPath . -executeMethod Unbounded.Editor.MainSceneBuilder.Build
    /// </summary>
    public static class MainSceneBuilder
    {
        private const string MainScenePath = "Assets/_Project/Scenes/Main.unity";
        private const string DrivingActionsPath = "Assets/_Project/Input/Driving.inputactions";
        private const string PhysicsFolder = "Assets/_Project/Art/Physics";
        private const string MeshFolder = "Assets/_Project/Art/Meshes";

        private const float GroundSize = 700f;
        private static readonly Vector3 GroundCentre = new Vector3(-125f, 0f, 25f);

        /// <summary>The car's painted body is this long, against a 4 m collider.</summary>
        private const float CarVisualLength = 3.55f;

        [MenuItem("Unbounded/Build Main Scene")]
        public static void Build()
        {
            InputActionAsset actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(DrivingActionsPath);
            if (actions == null)
            {
                throw new IOException("Missing input actions at " + DrivingActionsPath);
            }

            ArtPalette.EnsureFolders();
            ArtPalette.EnsureFolder(PhysicsFolder);
            ArtPalette.EnsureFolder(MeshFolder);
            KitAssets.PrepareImports();

            Materials materials = CreateMaterials();
            KitPalette palette = CreateKitPalette(materials);

            PhysicsMaterial frictionless = CreateFrictionlessMaterial(
                Path.Combine(PhysicsFolder, "Frictionless.physicMaterial"));

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateLighting();
            CreateGround(materials.Grass, frictionless);
            TrackRoot track = CreateTrack(
                materials.Asphalt,
                materials.Kerb,
                materials.Barrier,
                materials.StartLine,
                materials.EdgeLine,
                frictionless);
            CreateEnvironment(track, palette);

            GameObject car = CreatePlayerCar(
                materials,
                palette,
                actions,
                frictionless,
                track);
            CreateChaseCamera(car.transform);

            var lapProgress = car.GetComponent<LapProgressTracker>();
            var input = car.GetComponent<PlayerInputReader>();
            RaceHud hud = CreateHud(lapProgress, car.GetComponent<CarController>());
            AiDriver[] opponents = CreateOpponents(
                materials,
                palette,
                frictionless,
                track,
                lapProgress);

            var raceObject = new GameObject("Race Controller");
            var race = raceObject.AddComponent<RaceController>();
            race.Configure(lapProgress, input, hud, opponents: opponents);
            EditorUtility.SetDirty(race);

            if (!EditorSceneManager.SaveScene(scene, MainScenePath))
            {
                throw new IOException("Failed to save " + MainScenePath);
            }

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(MainScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("Built the main scene with a " + track.Path.Length.ToString("0") +
                      " metre circuit under a golden-hour sky.");
        }

        private sealed class Materials
        {
            public Material Asphalt;
            public Material Kerb;
            public Material Barrier;
            public Material StartLine;
            public Material EdgeLine;
            public Material Grass;
            public Material Tyre;
            public Material Trim;
            public Material Glass;
            public Material Nitro;
            public Material PlayerPaint;
            public Material[] OpponentPaint;
        }

        private static Materials CreateMaterials()
        {
            Texture2D asphaltNoise = ArtPalette.Noise(
                "Asphalt", ArtPalette.Srgb(0x32, 0x38, 0x40), ArtPalette.Srgb(0x46, 0x4C, 0x55), 26f);
            Texture2D grassNoise = ArtPalette.Noise(
                "Grass", ArtPalette.Srgb(0x4C, 0x6B, 0x36), ArtPalette.Srgb(0x63, 0x84, 0x42), 14f);
            Texture2D kerbStripes = ArtPalette.Stripes(
                "Kerb", ArtPalette.Srgb(0xD9, 0x35, 0x28), ArtPalette.Srgb(0xF2, 0xEE, 0xE2), 8);
            Texture2D barrierStripes = ArtPalette.Stripes(
                "Barrier", ArtPalette.Srgb(0xC8, 0x33, 0x29), ArtPalette.Srgb(0xE8, 0xE3, 0xD6), 4);
            Texture2D startLineCheckers = ArtPalette.Checkers("StartLine", 16, 2);

            var materials = new Materials
            {
                Asphalt = ArtPalette.Lit("Asphalt", ArtPalette.Asphalt, 0.18f, map: asphaltNoise, tiling: Vector2.one),
                Kerb = ArtPalette.Lit("Kerb", Color.white, 0.22f, map: kerbStripes),
                Barrier = ArtPalette.Lit("Barrier", Color.white, 0.2f, map: barrierStripes),
                StartLine = ArtPalette.Lit("StartLine", Color.white, 0.25f, map: startLineCheckers,
                    tiling: new Vector2(1f / (TrackShape.Default.HalfWidth * 2f), 1f / 1.5f)),
                EdgeLine = ArtPalette.Lit("EdgeLine", ArtPalette.EdgeWhite, 0.3f),
                Grass = ArtPalette.Lit("Grass", ArtPalette.Grass, 0.06f, map: grassNoise,
                    tiling: new Vector2(GroundSize / 12f, GroundSize / 12f)),
                Tyre = ArtPalette.Lit("CarTyre", ArtPalette.Tyre, 0.12f),
                Trim = ArtPalette.Lit("CarTrim", ArtPalette.KitWhite, 0.45f, 0.1f),
                Glass = ArtPalette.Lit("CarGlass", ArtPalette.KitGlass, 0.9f, 0.35f),
                Nitro = ArtPalette.Emissive("NitroFlame", ArtPalette.NitroFlame, 5f),
                PlayerPaint = ArtPalette.Lit("CarPaintPlayer", ArtPalette.PlayerPaint, 0.72f, 0.2f),
                OpponentPaint = new Material[ArtPalette.OpponentPaint.Length]
            };

            for (int i = 0; i < materials.OpponentPaint.Length; i++)
            {
                materials.OpponentPaint[i] = ArtPalette.Lit(
                    "CarPaintOpponent" + (i + 1), ArtPalette.OpponentPaint[i], 0.72f, 0.2f);
            }

            return materials;
        }

        /// <summary>
        /// Both kits name their materials, so one dictionary decides every flat
        /// colour in the environment: props, trees, rocks, and signage.
        /// </summary>
        private static KitPalette CreateKitPalette(Materials materials)
        {
            Texture2D tankco = AssetDatabase.LoadAssetAtPath<Texture2D>(
                Path.Combine(KitAssets.RacingTextures, "tankco.png"));
            Texture2D checkers = AssetDatabase.LoadAssetAtPath<Texture2D>(
                Path.Combine(KitAssets.RacingTextures, "checkers.png"));
            Texture2D net = AssetDatabase.LoadAssetAtPath<Texture2D>(
                Path.Combine(KitAssets.RacingTextures, "net.png"));

            var palette = new KitPalette();
            palette.Add("grey", ArtPalette.Lit("KitWhite", ArtPalette.KitWhite, 0.35f));
            palette.Add("_defaultMat", ArtPalette.Lit("KitWhite", ArtPalette.KitWhite, 0.35f));
            palette.Add("red", ArtPalette.Lit("KitRed", ArtPalette.KitRed, 0.4f));
            palette.Add("road", materials.Asphalt);
            palette.Add("glass", materials.Glass);
            palette.Add("carTire", materials.Tyre);
            palette.Add("pylon", ArtPalette.Lit("KitOrange", ArtPalette.KitOrange, 0.4f));
            palette.Add("tankco", ArtPalette.Lit("KitTankco", Color.white, 0.35f, map: tankco));
            palette.Add("checkers", ArtPalette.Cutout("KitCheckers", Color.white, checkers));
            palette.Add("net", ArtPalette.Cutout("KitNet", Color.white, net));
            palette.Add("bark", ArtPalette.Lit("KitBark", ArtPalette.KitBark, 0.2f));
            palette.Add("grass", ArtPalette.Lit("KitGrass", ArtPalette.Grass, 0.12f));
            palette.Add("dirt", ArtPalette.Lit("KitDirt", ArtPalette.KitDirt, 0.15f));
            palette.Add("woodBark", ArtPalette.Lit("TreeTrunk", ArtPalette.TreeTrunk, 0.12f));
            palette.Add("woodBarkDark", ArtPalette.Lit("TreeTrunkDark", ArtPalette.TreeTrunkDark, 0.12f));
            palette.Add("leafsGreen", ArtPalette.Lit("LeavesGreen", ArtPalette.LeavesGreen, 0.1f));
            palette.Add("leafsDark", ArtPalette.Lit("LeavesDark", ArtPalette.LeavesDark, 0.1f));
            return palette;
        }

        private static void CreateLighting()
        {
            var lightObject = new GameObject("Sun");
            lightObject.transform.rotation = Quaternion.Euler(9f, -24f, 0f);
            var sun = lightObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = ArtPalette.Sun;
            sun.intensity = 2.4f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.88f;
            sun.shadowBias = 0.04f;
            sun.shadowNormalBias = 0.35f;

            // A cool fill from the opposite side keeps the shadowed flanks of
            // the cars and the barriers from going black.
            var fillObject = new GameObject("Fill Light");
            fillObject.transform.rotation = Quaternion.Euler(34f, 156f, 0f);
            var fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = ArtPalette.Fill;
            fill.intensity = 0.38f;
            fill.shadows = LightShadows.None;

            Vector3 sunDirection = -(lightObject.transform.rotation * Vector3.forward);
            RenderSettings.skybox = ArtPalette.Sky(sunDirection);
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1.3f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = 1f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0032f;
            RenderSettings.fogColor = ArtPalette.Fog;

            var volumeObject = new GameObject("Post Processing");
            Volume volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 0f;
            volume.sharedProfile = ArtPalette.PostProcessing();
        }

        /// <summary>
        /// The grass the whole circuit sits on. It is a generated mesh, not a
        /// primitive, so the ground can carry the palette's tiling and an exact
        /// collider without a scale trick.
        /// </summary>
        private static void CreateGround(Material material, PhysicsMaterial physicMaterial)
        {
            GameObject ground = new GameObject("Ground");
            ground.transform.position = GroundCentre;

            Mesh mesh = CreateGroundMesh();
            ground.AddComponent<MeshFilter>().sharedMesh = mesh;
            ground.AddComponent<MeshRenderer>().sharedMaterial = material;

            var collider = ground.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            collider.sharedMaterial = physicMaterial;

            GameObjectUtility.SetStaticEditorFlags(ground,
                StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
        }

        private static Mesh CreateGroundMesh()
        {
            float half = GroundSize * 0.5f;
            var vertices = new[]
            {
                new Vector3(-half, 0f, -half),
                new Vector3(-half, 0f, half),
                new Vector3(half, 0f, half),
                new Vector3(half, 0f, -half)
            };
            var normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            var uvs = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 0f)
            };
            var triangles = new[] { 0, 1, 2, 0, 2, 3 };

            return SaveMesh("Ground", vertices, normals, uvs, triangles);
        }

        private static TrackRoot CreateTrack(
            Material roadMaterial,
            Material kerbMaterial,
            Material barrierMaterial,
            Material startLineMaterial,
            Material edgeLineMaterial,
            PhysicsMaterial physicMaterial)
        {
            var trackObject = new GameObject("Track");
            trackObject.transform.position = Vector3.zero;

            var track = trackObject.AddComponent<TrackRoot>();
            track.Configure(
                TrackLayout.Default(),
                TrackShape.Default,
                roadMaterial,
                kerbMaterial,
                barrierMaterial,
                startLineMaterial,
                physicMaterial,
                edgeLineMaterial);

            if (track.Path == null)
            {
                throw new IOException("The circuit failed to build from its control points.");
            }

            return track;
        }

        /// <summary>
        /// Dresses the circuit from the kits: a start gantry and flags, a
        /// grandstand and pit lane along the opening straight, light posts
        /// around the lap, signage at the corners, and the treeline beyond.
        /// Everything sits outside the Barriers so the Race never drives
        /// through scenery.
        /// </summary>
        private static void CreateEnvironment(TrackRoot track, KitPalette palette)
        {
            var furniture = new GameObject("Circuit").transform;
            TrackShape shape = track.Shape;

            // The gantry spans the opening straight just past the Grid, so the
            // field launches under it instead of the chase camera starting
            // inside its frame.
            Pose gantry = track.SamplePose(26f);
            KitAssets.Spawn(KitAssets.Racing, "overhead", palette, furniture, gantry.position, gantry.rotation,
                new Vector3(1.65f, 1.05f, 1f));

            foreach (int side in new[] { -1, 1 })
            {
                Pose flag = track.SamplePose(26f, side * (shape.BarrierOffset + 2.2f));
                KitAssets.Spawn(KitAssets.Racing, "flagCheckers", palette, furniture, flag.position,
                    flag.rotation * Quaternion.Euler(0f, side > 0 ? 90f : -90f, 0f), 0.72f);
            }

            // The pits and grandstands run down the right of the opening
            // straight, outside the Barriers.
            for (int i = 0; i < 5; i++)
            {
                Pose stand = track.SamplePose(40f + i * 12f, 19f);
                KitAssets.Spawn(KitAssets.Racing, i % 2 == 0 ? "grandStand" : "grandStandCovered", palette,
                    furniture, stand.position, stand.rotation * Quaternion.Euler(0f, 90f, 0f), 1.1f);
            }

            for (int i = 0; i < 7; i++)
            {
                Pose garage = track.SamplePose(16f + i * 11.5f, 28f);
                KitAssets.Spawn(KitAssets.Racing, "pitsGarage", palette, furniture, garage.position,
                    garage.rotation * Quaternion.Euler(0f, 90f, 0f), 1.05f);
            }

            Pose office = track.SamplePose(96f, 29f);
            KitAssets.Spawn(KitAssets.Racing, "pitsOffice", palette, furniture, office.position,
                office.rotation * Quaternion.Euler(0f, 90f, 0f), 1.05f);

            for (int i = 0; i < 3; i++)
            {
                Pose tent = track.SamplePose(30f + i * 24f, 38f);
                KitAssets.Spawn(KitAssets.Racing, "tentLong", palette, furniture, tent.position,
                    tent.rotation * Quaternion.Euler(0f, 90f, 0f), 1f);
            }

            foreach (int side in new[] { -1, 1 })
            {
                Pose tower = track.SamplePose(118f, side * 17f);
                KitAssets.Spawn(KitAssets.Racing, "bannerTowerRed", palette, furniture, tower.position,
                    tower.rotation * Quaternion.Euler(0f, side > 0 ? 90f : -90f, 0f), 0.8f);
            }

            // Light posts march down both sides of the whole lap.
            float step = 74f;
            for (float distance = 20f; distance < track.Path.Length; distance += step)
            {
                foreach (int side in new[] { -1, 1 })
                {
                    Pose post = track.SamplePose(distance + (side > 0 ? 0f : step * 0.5f),
                        side * (shape.BarrierOffset + 2.2f));
                    KitAssets.Spawn(KitAssets.Racing, "lightPostModern", palette, furniture, post.position,
                        post.rotation * Quaternion.Euler(0f, side > 0 ? 90f : -90f, 0f), 0.9f);
                }
            }

            // Billboards face the approach to the three big corners.
            float[] corners = { 205f, 470f, 745f };
            for (int i = 0; i < corners.Length; i++)
            {
                int side = i % 2 == 0 ? 1 : -1;
                Pose board = track.SamplePose(corners[i], side * (shape.EdgeOffset + 6f));
                KitAssets.Spawn(KitAssets.Racing, "billboard", palette, furniture, board.position,
                    board.rotation * Quaternion.Euler(0f, side > 0 ? -90f : 90f, 0f), 1f);
            }

            // A handful of pylons along the verge, where a marshal would drop
            // them, so the edge of the Road has something human at ground level.
            for (int i = 0; i < 14; i++)
            {
                float distance = 40f + i * 88f;
                int side = i % 2 == 0 ? -1 : 1;
                Pose cone = track.SamplePose(distance, side * (shape.EdgeOffset + 0.9f));
                KitAssets.Spawn(KitAssets.Racing, "pylon", palette, furniture, cone.position,
                    cone.rotation, 1.1f);
            }

            for (int i = 0; i < 6; i++)
            {
                Pose fence = track.SamplePose(150f + i * 10f, shape.BarrierOffset + 1.4f);
                KitAssets.Spawn(KitAssets.Racing, "fenceStraight", palette, furniture, fence.position,
                    fence.rotation, 1f);
            }

            ScatterNature(track, furniture, palette);
        }

        /// <summary>
        /// The treeline and the rocks. Placement is seeded, so the same
        /// circuit comes back every build, and every sample is kept clear of the
        /// Road, the verge, and the paddock.
        /// </summary>
        private static void ScatterNature(TrackRoot track, Transform parent, KitPalette palette)
        {
            var random = new System.Random(4177);
            TrackShape shape = track.Shape;
            float length = track.Path.Length;

            string[] trees = { "tree_pineTallA", "tree_pineTallB", "tree_default", "tree_detailed", "tree_fat", "tree_cone" };
            string[] rocks = { "rock_largeA", "rock_largeB", "rock_tallC", "rock_smallA", "rock_smallC" };

            int planted = 0;
            int attempts = 0;
            while (planted < 190 && attempts < 4000)
            {
                attempts++;
                float distance = (float)random.NextDouble() * length;
                int side = random.Next(2) == 0 ? -1 : 1;
                float lateral = side * (20f + (float)random.NextDouble() * 75f);

                // Leave room for the paddock on the right of the opening straight.
                if (side > 0 && distance < 150f && lateral < 46f)
                {
                    continue;
                }

                Pose pose = track.SamplePose(distance, lateral);
                float scale = 4.2f + (float)random.NextDouble() * 4.2f;
                Quaternion rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
                string tree = trees[random.Next(trees.Length)];
                KitAssets.Spawn(KitAssets.Nature, tree, palette, parent,
                    new Vector3(pose.position.x, 0f, pose.position.z), rotation, scale);
                planted++;
            }

            // A fuller belt far out, so the horizon reads as forest under the fog.
            for (int i = 0; i < 80; i++)
            {
                float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                float radius = 230f + (float)random.NextDouble() * 90f;
                var point = new Vector3(
                    GroundCentre.x + Mathf.Cos(angle) * radius,
                    0f,
                    GroundCentre.z + Mathf.Sin(angle) * radius);

                if (Mathf.Abs(track.Project(point).LateralOffset) < 55f)
                {
                    continue;
                }

                float scale = 8f + (float)random.NextDouble() * 5f;
                KitAssets.Spawn(KitAssets.Nature, trees[random.Next(trees.Length)], palette, parent, point,
                    Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f), scale);
            }

            for (int i = 0; i < 60; i++)
            {
                float distance = (float)random.NextDouble() * length;
                int side = random.Next(2) == 0 ? -1 : 1;
                float lateral = side * (14f + (float)random.NextDouble() * 26f);
                if (side > 0 && distance < 150f && lateral < 46f)
                {
                    continue;
                }

                Pose pose = track.SamplePose(distance, lateral);
                float scale = 3f + (float)random.NextDouble() * 5f;
                KitAssets.Spawn(KitAssets.Nature, rocks[random.Next(rocks.Length)], palette, parent,
                    new Vector3(pose.position.x, 0f, pose.position.z),
                    Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f), scale);
            }

            for (int i = 0; i < 50; i++)
            {
                float distance = (float)random.NextDouble() * length;
                int side = random.Next(2) == 0 ? -1 : 1;
                float lateral = side * (12.5f + (float)random.NextDouble() * 18f);
                if (side > 0 && distance < 150f && lateral < 46f)
                {
                    continue;
                }

                Pose pose = track.SamplePose(distance, lateral);
                KitAssets.Spawn(KitAssets.Nature,
                    random.Next(2) == 0 ? "plant_bushLarge" : "plant_bushDetailed", palette, parent,
                    new Vector3(pose.position.x, 0f, pose.position.z),
                    Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f), 3.5f);
            }
        }

        private static GameObject CreatePlayerCar(
            Materials materials,
            KitPalette palette,
            InputActionAsset actions,
            PhysicsMaterial physicMaterial,
            TrackRoot track)
        {
            GameObject car = CreateCar(
                "Player Car",
                track.SamplePose(LapProgressTracker.StartDistance),
                palette,
                materials.PlayerPaint,
                physicMaterial,
                track,
                out CarController controller);
            car.tag = "Player";
            AddNitroEffect(car, materials.Nitro, isPlayer: true);
            AddCarAudio(car, isPlayer: true);

            var lapProgress = car.AddComponent<LapProgressTracker>();
            lapProgress.Configure(track, car.GetComponent<Rigidbody>());

            var input = car.AddComponent<PlayerInputReader>();
            input.Configure(actions, controller, lapProgress);
            EditorUtility.SetDirty(input);

            return car;
        }

        /// <summary>
        /// Fills the Grid ahead of the player with opponents. Each has a small
        /// pace offset and its own livery, so the field spreads over a Race
        /// instead of forming one train, and stays readable in the mirror.
        /// </summary>
        private static AiDriver[] CreateOpponents(
            Materials materials,
            KitPalette palette,
            PhysicsMaterial physicMaterial,
            TrackRoot track,
            LapProgressTracker player)
        {
            var opponents = new AiDriver[RaceGrid.OpponentCount];

            for (int slot = 0; slot < opponents.Length; slot++)
            {
                float distance = RaceGrid.DistanceFor(slot);
                float lateral = RaceGrid.LateralFor(slot);

                GameObject car = CreateCar(
                    "Opponent " + (slot + 1),
                    track.SamplePose(distance, lateral),
                    palette,
                    materials.OpponentPaint[slot],
                    physicMaterial,
                    track,
                    out CarController controller);

                AddNitroEffect(car, materials.Nitro, isPlayer: false);
                AddCarAudio(car, isPlayer: false);

                AiConfig config = AiConfig.Default;
                config.Pace = 0.93f + slot * 0.015f;

                var driver = car.AddComponent<AiDriver>();
                driver.Configure(track, controller, car.GetComponent<Rigidbody>(), player, config, distance, lateral);
                EditorUtility.SetDirty(driver);

                opponents[slot] = driver;
            }

            return opponents;
        }

        private static GameObject CreateCar(
            string name,
            Pose spawn,
            KitPalette palette,
            Material paint,
            PhysicsMaterial physicMaterial,
            TrackRoot track,
            out CarController controller)
        {
            var car = new GameObject(name);
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

            controller = car.AddComponent<CarController>();
            controller.Config = HandlingConfig.Default;

            var surface = car.AddComponent<CarSurfaceAdapter>();
            surface.Configure(track);

            AddCarVisual(car, palette, paint);

            return car;
        }

        /// <summary>
        /// The Kit's race car, scaled to the collider and laid on the Track.
        /// The body material follows the car's livery; the Kit's trim, glass,
        /// and tyres stay shared. The Kit models its cars nose-down its own
        /// -Z while the car root drives along +Z, so the visual is yawed to
        /// face the way the car travels.
        /// </summary>
        private static void AddCarVisual(GameObject car, KitPalette palette, Material paint)
        {
            GameObject visual = KitAssets.Spawn(
                KitAssets.Racing,
                "raceCarRed",
                palette,
                car.transform,
                Vector3.zero,
                Quaternion.Euler(0f, 180f, 0f),
                Vector3.one,
                paint);

            Bounds bounds = KitAssets.BoundsIn(visual, car.transform);
            float scale = bounds.size.z > 0.001f ? CarVisualLength / bounds.size.z : 1f;
            visual.transform.localScale = Vector3.one * scale;

            Bounds scaled = KitAssets.BoundsIn(visual, car.transform);
            visual.transform.localPosition -= new Vector3(scaled.center.x, scaled.min.y, scaled.center.z);
        }

        /// <summary>
        /// Two emissive exhaust jets and a glow light that switch on with the
        /// burst, plus a looping hiss. The player hears their own Nitro dry;
        /// opponents are placed in the world.
        /// </summary>
        private static void AddNitroEffect(GameObject car, Material flameMaterial, bool isPlayer)
        {
            Mesh jet = CreateJetMesh();
            var flames = new GameObject("Nitro Flames");
            flames.transform.SetParent(car.transform, false);
            AddJet(flames.transform, "Flame L", new Vector3(-0.42f, 0.46f, -1.72f), jet, flameMaterial);
            AddJet(flames.transform, "Flame R", new Vector3(0.42f, 0.46f, -1.72f), jet, flameMaterial);
            flames.SetActive(false);

            var glowObject = new GameObject("Nitro Glow");
            glowObject.transform.SetParent(car.transform, false);
            glowObject.transform.localPosition = new Vector3(0f, 0.55f, -2.2f);
            var glow = glowObject.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.5f, 0.12f);
            glow.range = 7f;
            glow.intensity = 0f;
            glow.shadows = LightShadows.None;
            glow.enabled = false;

            var audio = car.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.loop = true;
            audio.spatialBlend = isPlayer ? 0f : 0.7f;
            audio.rolloffMode = AudioRolloffMode.Logarithmic;
            audio.minDistance = 3f;
            audio.maxDistance = 60f;

            var effect = car.AddComponent<NitroBoostEffect>();
            effect.Configure(car.GetComponent<CarController>(), flames, glow, audio);
        }

        private static void AddJet(Transform parent, string name, Vector3 localPosition, Mesh mesh, Material material)
        {
            var jet = new GameObject(name);
            jet.transform.SetParent(parent, false);
            jet.transform.localPosition = localPosition;
            jet.AddComponent<MeshFilter>().sharedMesh = mesh;
            jet.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        /// <summary>A tapered jet that points down the car's -Z, built once and shared.</summary>
        private static Mesh CreateJetMesh()
        {
            const int segments = 8;
            const float radius = 0.17f;
            const float length = 0.95f;

            var vertices = new Vector3[segments + 2];
            vertices[0] = Vector3.zero;
            vertices[segments + 1] = new Vector3(0f, 0f, -length);
            for (int i = 0; i < segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
            }

            var triangles = new System.Collections.Generic.List<int>();
            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                triangles.Add(0);
                triangles.Add(i + 2);
                triangles.Add(i + 1);
                triangles.Add(i + 1);
                triangles.Add(i + 2);
                triangles.Add(segments + 1);
            }

            var mesh = new Mesh { name = "Nitro Jet" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            string path = Path.Combine(MeshFolder, "NitroJet.asset");
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(mesh, existing);
                Object.DestroyImmediate(mesh);
                return existing;
            }

            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private static Mesh SaveMesh(
            string name,
            Vector3[] vertices,
            Vector3[] normals,
            Vector2[] uvs,
            int[] triangles)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();

            string path = Path.Combine(MeshFolder, name + ".asset");
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(mesh, existing);
                Object.DestroyImmediate(mesh);
                return existing;
            }

            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        /// <summary>
        /// Gives a car its engine, tyre, and impact sources. The player's own
        /// car is centred; opponents are fully spatialised with a low-pass
        /// filter that closes as they get further from the listener, so five
        /// engines around the Track stay a field rather than a wall of noise.
        /// </summary>
        private static void AddCarAudio(GameObject car, bool isPlayer)
        {
            AudioSource engine = CreateCarAudioSource(
                car.transform,
                "Engine Audio",
                loop: true,
                spatialBlend: isPlayer ? 0f : 1f,
                priority: isPlayer ? 0 : 160,
                out AudioLowPassFilter engineFilter);
            AudioSource tyre = CreateCarAudioSource(
                car.transform,
                "Tyre Audio",
                loop: true,
                spatialBlend: isPlayer ? 0f : 1f,
                priority: isPlayer ? 32 : 180,
                out AudioLowPassFilter tyreFilter);
            AudioSource impact = CreateCarAudioSource(
                car.transform,
                "Impact Audio",
                loop: false,
                spatialBlend: isPlayer ? 0.35f : 1f,
                priority: isPlayer ? 16 : 170,
                out AudioLowPassFilter impactFilter);

            var audio = car.AddComponent<CarAudio>();
            audio.Configure(
                car.GetComponent<CarController>(),
                car.GetComponent<Rigidbody>(),
                engine,
                tyre,
                impact,
                new[] { engineFilter, tyreFilter, impactFilter },
                isPlayer);
            EditorUtility.SetDirty(audio);
        }

        private static AudioSource CreateCarAudioSource(
            Transform parent,
            string name,
            bool loop,
            float spatialBlend,
            int priority,
            out AudioLowPassFilter distanceFilter)
        {
            var audioObject = new GameObject(name);
            audioObject.transform.SetParent(parent, false);
            audioObject.transform.localPosition = new Vector3(0f, 0.6f, 0f);

            var source = audioObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = spatialBlend;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = 3f;
            source.maxDistance = 60f;
            source.dopplerLevel = 0f;
            source.priority = priority;
            source.volume = loop ? 0f : 1f;

            distanceFilter = audioObject.AddComponent<AudioLowPassFilter>();
            distanceFilter.cutoffFrequency = 22000f;

            return source;
        }

        private static void CreateChaseCamera(Transform target)
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                var cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                cameraObject.transform.position = new Vector3(0f, 2.6f, -7.5f);
                cameraObject.transform.rotation = Quaternion.Euler(8f, 0f, 0f);
                camera = cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
                CinemachineBrain brain = cameraObject.AddComponent<CinemachineBrain>();

                // The car is a Rigidbody, so sample the camera on the physics clock:
                // it keeps the rig glued to the car no matter the render rate.
                brain.UpdateMethod = CinemachineBrain.UpdateMethods.FixedUpdate;
            }

            camera.fieldOfView = 62f;
            camera.nearClipPlane = 0.25f;
            camera.farClipPlane = 900f;
            camera.allowHDR = true;
            camera.allowMSAA = true;

            UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.None;
            data.volumeLayerMask = ~0;
            data.volumeTrigger = camera.transform;

            var cameraObjectRoot = new GameObject("Chase Camera");
            var virtualCamera = cameraObjectRoot.AddComponent<CinemachineCamera>();
            virtualCamera.Target = new CameraTarget
            {
                TrackingTarget = target,
                LookAtTarget = target
            };
            virtualCamera.Lens.FieldOfView = 62f;
            virtualCamera.Priority = 10;

            // The body keeps its distance; the composer is what actually points
            // the lens at the car, so the view swings through the corners.
            var aim = cameraObjectRoot.AddComponent<CinemachineRotationComposer>();
            aim.TargetOffset = new Vector3(0f, 0.55f, 0f);
            aim.Damping = new Vector2(0.12f, 0.22f);

            var follow = cameraObjectRoot.AddComponent<CinemachineThirdPersonFollow>();
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

        private static RaceHud CreateHud(LapProgressTracker lapProgress, CarController car)
        {
            var hudObject = new GameObject("HUD");
            var canvas = hudObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = hudObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                throw new IOException("The legacy UI font is unavailable.");
            }

            Text lap = CreateText(hudObject.transform, "Lap Counter", font, 36, TextAnchor.MiddleLeft);
            RectTransform lapRect = lap.rectTransform;
            lapRect.anchorMin = new Vector2(0f, 1f);
            lapRect.anchorMax = new Vector2(0f, 1f);
            lapRect.pivot = new Vector2(0f, 1f);
            lapRect.anchoredPosition = new Vector2(36f, -28f);
            lapRect.sizeDelta = new Vector2(420f, 64f);
            lap.text = "LAP 1/3";

            Text position = CreateText(hudObject.transform, "Position", font, 30, TextAnchor.MiddleLeft);
            RectTransform positionRect = position.rectTransform;
            positionRect.anchorMin = new Vector2(0f, 1f);
            positionRect.anchorMax = new Vector2(0f, 1f);
            positionRect.pivot = new Vector2(0f, 1f);
            positionRect.anchoredPosition = new Vector2(36f, -92f);
            positionRect.sizeDelta = new Vector2(420f, 56f);
            position.text = "POS 6/6";

            Text timer = CreateText(hudObject.transform, "Race Timer", font, 34, TextAnchor.MiddleRight);
            RectTransform timerRect = timer.rectTransform;
            timerRect.anchorMin = new Vector2(1f, 1f);
            timerRect.anchorMax = new Vector2(1f, 1f);
            timerRect.pivot = new Vector2(1f, 1f);
            timerRect.anchoredPosition = new Vector2(-36f, -28f);
            timerRect.sizeDelta = new Vector2(420f, 64f);
            timer.text = "0:00.00";

            Text bestLap = CreateText(hudObject.transform, "Best Lap", font, 26, TextAnchor.MiddleRight);
            RectTransform bestLapRect = bestLap.rectTransform;
            bestLapRect.anchorMin = new Vector2(1f, 1f);
            bestLapRect.anchorMax = new Vector2(1f, 1f);
            bestLapRect.pivot = new Vector2(1f, 1f);
            bestLapRect.anchoredPosition = new Vector2(-36f, -92f);
            bestLapRect.sizeDelta = new Vector2(420f, 44f);
            bestLap.text = "BEST --:--.--";
            bestLap.color = new Color(1f, 0.93f, 0.55f);

            CreateNitroMeter(hudObject.transform, font, out RectTransform nitroFill,
                out Image nitroFillImage, out Text nitroLabel);

            Text wrongWay = CreateText(hudObject.transform, "Wrong Way", font, 44, TextAnchor.MiddleCenter);
            RectTransform wrongWayRect = wrongWay.rectTransform;
            wrongWayRect.anchorMin = new Vector2(0.5f, 1f);
            wrongWayRect.anchorMax = new Vector2(0.5f, 1f);
            wrongWayRect.pivot = new Vector2(0.5f, 1f);
            wrongWayRect.anchoredPosition = new Vector2(0f, -96f);
            wrongWayRect.sizeDelta = new Vector2(640f, 64f);
            wrongWay.text = "WRONG WAY";
            wrongWay.fontStyle = FontStyle.Bold;
            wrongWay.color = new Color(1f, 0.27f, 0.22f);
            wrongWay.gameObject.SetActive(false);

            Text countdown = CreateText(hudObject.transform, "Countdown", font, 150, TextAnchor.MiddleCenter);
            RectTransform countdownRect = countdown.rectTransform;
            countdownRect.anchorMin = new Vector2(0.5f, 0.5f);
            countdownRect.anchorMax = new Vector2(0.5f, 0.5f);
            countdownRect.pivot = new Vector2(0.5f, 0.5f);
            countdownRect.anchoredPosition = new Vector2(0f, 130f);
            countdownRect.sizeDelta = new Vector2(720f, 260f);
            countdown.text = "3";
            countdown.fontStyle = FontStyle.Bold;
            countdown.color = new Color(1f, 0.93f, 0.55f);
            countdown.gameObject.SetActive(false);

            GameObject finishScreen = CreatePanel(hudObject.transform, "Finish Screen", font,
                "RACE COMPLETE", 86, out Text finishSummary);
            finishSummary.text = "TIME   0:00.00\n\nPRESS R / Y TO RESTART";

            GameObject pauseScreen = CreatePanel(hudObject.transform, "Pause Screen", font,
                "PAUSED", 86, out Text pauseSummary);
            pauseSummary.text =
                "  MASTER VOLUME   [########--]   80%\n" +
                "> STEERING        [#####-----]   1.00x\n\n" +
                "BEST LAP   --:--.--\n\n" +
                "UP / DOWN SELECT    LEFT / RIGHT ADJUST\n" +
                "BACKSPACE / X CLEAR BEST LAP\n" +
                "ESC / START RESUME    Q / SELECT QUIT";

            var hud = hudObject.AddComponent<RaceHud>();
            hud.Configure(lap, position, wrongWay);
            hud.ConfigureRace(countdown, timer, finishScreen, finishSummary, pauseScreen, pauseSummary);
            hud.ConfigureNitro(nitroFill, nitroFillImage, nitroLabel, car);
            hud.ConfigureBestLap(bestLap);
            hud.Bind(lapProgress);
            return hud;
        }

        /// <summary>
        /// The Nitro meter: a label over a dark bar whose fill is anchored
        /// across the charge left. Anchoring avoids needing a sprite for a
        /// filled image, so the whole HUD stays generated from code.
        /// </summary>
        private static void CreateNitroMeter(
            Transform parent,
            Font font,
            out RectTransform fill,
            out Image fillImage,
            out Text label)
        {
            label = CreateText(parent, "Nitro Label", font, 26, TextAnchor.LowerLeft);
            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.zero;
            labelRect.pivot = Vector2.zero;
            labelRect.anchoredPosition = new Vector2(36f, 56f);
            labelRect.sizeDelta = new Vector2(340f, 34f);
            label.text = "NITRO";
            label.color = new Color(0.28f, 0.82f, 1f);

            var bar = new GameObject("Nitro Meter", typeof(RectTransform));
            bar.transform.SetParent(parent, false);
            var barRect = bar.GetComponent<RectTransform>();
            barRect.anchorMin = Vector2.zero;
            barRect.anchorMax = Vector2.zero;
            barRect.pivot = Vector2.zero;
            barRect.anchoredPosition = new Vector2(36f, 24f);
            barRect.sizeDelta = new Vector2(340f, 26f);

            var background = bar.AddComponent<Image>();
            background.color = new Color(0.02f, 0.03f, 0.05f, 0.8f);
            background.raycastTarget = false;

            var fillObject = new GameObject("Fill", typeof(RectTransform));
            fillObject.transform.SetParent(bar.transform, false);
            fill = fillObject.GetComponent<RectTransform>();
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.offsetMin = new Vector2(3f, 3f);
            fill.offsetMax = new Vector2(-3f, -3f);

            fillImage = fillObject.AddComponent<Image>();
            fillImage.color = new Color(0.28f, 0.82f, 1f);
            fillImage.raycastTarget = false;
        }

        /// <summary>
        /// A full-screen dark panel with a title and a body, for the finish and
        /// pause screens. It blocks nothing: there is no EventSystem, and the
        /// panels are toggled from the Race flow.
        /// </summary>
        private static GameObject CreatePanel(
            Transform parent,
            string name,
            Font font,
            string titleText,
            int titleSize,
            out Text body)
        {
            var panel = new GameObject(name, typeof(RectTransform));
            panel.transform.SetParent(parent, false);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var background = panel.AddComponent<Image>();
            background.color = new Color(0.01f, 0.01f, 0.02f, 0.82f);
            background.raycastTarget = false;

            Text title = CreateText(panel.transform, "Title", font, titleSize, TextAnchor.MiddleCenter);
            RectTransform titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0.5f, 0.5f);
            titleRect.anchorMax = new Vector2(0.5f, 0.5f);
            titleRect.pivot = new Vector2(0.5f, 0.5f);
            titleRect.anchoredPosition = new Vector2(0f, 110f);
            titleRect.sizeDelta = new Vector2(1400f, 180f);
            title.text = titleText;
            title.fontStyle = FontStyle.Bold;
            title.color = new Color(1f, 0.93f, 0.55f);

            body = CreateText(panel.transform, "Summary", font, 36, TextAnchor.UpperCenter);
            RectTransform bodyRect = body.rectTransform;
            bodyRect.anchorMin = new Vector2(0.5f, 0.5f);
            bodyRect.anchorMax = new Vector2(0.5f, 0.5f);
            bodyRect.pivot = new Vector2(0.5f, 1f);
            bodyRect.anchoredPosition = new Vector2(0f, -10f);
            bodyRect.sizeDelta = new Vector2(1400f, 520f);

            panel.SetActive(false);
            return panel;
        }

        private static Text CreateText(
            Transform parent,
            string name,
            Font font,
            int size,
            TextAnchor alignment)
        {
            var textObject = new GameObject(name, typeof(RectTransform));
            textObject.transform.SetParent(parent, false);

            var text = textObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;

            var outline = textObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            outline.effectDistance = new Vector2(2f, -2f);

            return text;
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
    }
}
