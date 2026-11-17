using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Unbounded.Editor
{
    /// <summary>
    /// The Race's palette and its post-processing. Every material the build
    /// creates comes from here, so the track, the kit props, and the cars share
    /// one set of colours instead of drifting apart asset by asset. Colours are
    /// authored in sRGB and converted to linear once, which is what makes the
    /// palette on screen match the palette in this file.
    /// </summary>
    internal static class ArtPalette
    {
        public const string MaterialsFolder = "Assets/_Project/Art/Materials";
        public const string TexturesFolder = "Assets/_Project/Art/Textures";
        public const string SkyFolder = "Assets/_Project/Art/Sky";
        public const string PostFolder = "Assets/_Project/Art/PostProcessing";

        // Track. The road is a cool, dark surface so the warm Kerbs and the
        // cars painted in the accents below read against it from a distance.
        public static readonly Color Asphalt = Linear(0x2C, 0x31, 0x38);
        public static readonly Color AsphaltLight = Linear(0x3A, 0x40, 0x48);
        public static readonly Color KerbRed = Linear(0xD9, 0x35, 0x28);
        public static readonly Color KerbWhite = Linear(0xF2, 0xEE, 0xE2);
        public static readonly Color BarrierWhite = Linear(0xE8, 0xE3, 0xD6);
        public static readonly Color BarrierRed = Linear(0xC8, 0x33, 0x29);
        public static readonly Color EdgeWhite = Linear(0xF4, 0xF1, 0xE7);
        public static readonly Color Grass = Linear(0x4C, 0x6B, 0x33);
        public static readonly Color GrassDark = Linear(0x39, 0x53, 0x26);

        // Kit props. The Racing Kit and the Nature Kit both name their materials,
        // so the mapping below is the one place their flat colours are decided.
        public static readonly Color KitWhite = Linear(0xE9, 0xE5, 0xDA);
        public static readonly Color KitRed = Linear(0xD9, 0x37, 0x2A);
        public static readonly Color KitOrange = Linear(0xE7, 0x8B, 0x2E);
        public static readonly Color KitGlass = Linear(0x2E, 0x44, 0x55);
        public static readonly Color KitBark = Linear(0x6B, 0x4B, 0x33);
        public static readonly Color KitDirt = Linear(0x8A, 0x7A, 0x5F);
        public static readonly Color TreeTrunk = Linear(0x5C, 0x40, 0x2C);
        public static readonly Color TreeTrunkDark = Linear(0x46, 0x32, 0x23);
        public static readonly Color LeavesGreen = Linear(0x4E, 0x7A, 0x36);
        public static readonly Color LeavesDark = Linear(0x35, 0x5B, 0x2E);
        public static readonly Color Tyre = Linear(0x1B, 0x1C, 0x1F);

        // Cars. The player is the one teal car on the Grid, so their silhouette
        // separates from the field and from the red Kerbs at a glance.
        public static readonly Color PlayerPaint = Linear(0x1F, 0xBE, 0xB0);
        public static readonly Color[] OpponentPaint =
        {
            Linear(0xE0, 0x43, 0x30),
            Linear(0xEF, 0xB1, 0x3B),
            Linear(0xEC, 0xE4, 0xD2),
            Linear(0x6F, 0xBF, 0x5A),
            Linear(0x9B, 0x6C, 0xD0)
        };

        // Light and air. The sun sits low and warm, the sky answers with a
        // cooler zenith, and the fog takes the horizon colour so the ground
        // edge dissolves instead of ending.
        public static readonly Color Sun = Linear(0xFF, 0xDD, 0xB8);
        public static readonly Color Fill = Linear(0x8F, 0xB6, 0xE0);
        public static readonly Color SkyHorizon = Linear(0xF2, 0xBE, 0x8E);
        public static readonly Color SkyZenith = Linear(0x24, 0x4C, 0x78);
        public static readonly Color Fog = Linear(0xD9, 0xB5, 0x95);
        public static readonly Color NitroFlame = Linear(0xFF, 0x8A, 0x2E);

        private static Material _litShaderProbe;

        public static Color Linear(byte r, byte g, byte b) =>
            new Color(r / 255f, g / 255f, b / 255f, 1f).linear;

        public static Color Srgb(byte r, byte g, byte b) =>
            new Color(r / 255f, g / 255f, b / 255f, 1f);

        public static void EnsureFolders()
        {
            EnsureFolder(MaterialsFolder);
            EnsureFolder(TexturesFolder);
            EnsureFolder(SkyFolder);
            EnsureFolder(PostFolder);
        }

        /// <summary>A URP Lit material, reused across builds so the scene keeps its references.</summary>
        public static Material Lit(
            string name,
            Color color,
            float smoothness,
            float metallic = 0f,
            Texture2D map = null,
            Vector2 tiling = default)
        {
            Material material = LoadOrCreate(name);

            material.color = map != null ? Color.white : color;
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Surface", 0f);
            material.SetFloat("_AlphaClip", 0f);
            material.DisableKeyword("_ALPHATEST_ON");
            material.renderQueue = -1;

            if (map != null)
            {
                material.SetTexture("_BaseMap", map);
                material.SetTextureScale("_BaseMap", tiling == default ? Vector2.one : tiling);
            }
            else
            {
                material.SetTexture("_BaseMap", null);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>A Lit material that punches through the tonemap, for Nitro and marker lights.</summary>
        public static Material Emissive(string name, Color color, float intensity)
        {
            Material material = LoadOrCreate(name);

            material.color = color;
            material.SetFloat("_Smoothness", 0.1f);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Surface", 0f);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * intensity);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>An alpha-clipped material, for the Kit's checkered flags and mesh fencing.</summary>
        public static Material Cutout(string name, Color tint, Texture2D map)
        {
            Material material = Lit(name, Color.white, 0.25f, 0f, map);
            material.color = tint;
            material.SetFloat("_AlphaClip", 1f);
            material.SetFloat("_Cutoff", 0.35f);
            material.EnableKeyword("_ALPHATEST_ON");
            material.renderQueue = 2450;
            EditorUtility.SetDirty(material);
            return material;
        }

        public static Texture2D Noise(string name, Color low, Color high, float frequency, int size = 256)
        {
            EnsureFolder(TexturesFolder);
            string path = Path.Combine(TexturesFolder, name + ".png");

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float offset = 0f;
            foreach (char character in name)
            {
                offset += character * 7;
            }

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = offset + (float)x / size * frequency;
                    float v = offset + (float)y / size * frequency;
                    float noise = Mathf.PerlinNoise(u, v) * 0.65f + Mathf.PerlinNoise(u * 3.7f, v * 3.7f) * 0.35f;
                    texture.SetPixel(x, y, Color.Lerp(low, high, noise));
                }
            }

            texture.Apply();
            return Save(texture, path);
        }

        /// <summary>Stripes that run around the Track, which is how a Kerb reads at speed.</summary>
        public static Texture2D Stripes(string name, Color stripe, Color body, int count)
        {
            EnsureFolder(TexturesFolder);
            string path = Path.Combine(TexturesFolder, name + ".png");

            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool painted = (x * count / size) % 2 == 0;
                    texture.SetPixel(x, y, painted ? stripe : body);
                }
            }

            texture.Apply();
            return Save(texture, path);
        }

        /// <summary>The start/finish line: two rows of squares across the Road.</summary>
        public static Texture2D Checkers(string name, int columns, int rows)
        {
            EnsureFolder(TexturesFolder);
            string path = Path.Combine(TexturesFolder, name + ".png");

            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool painted = (x * columns / size + y * rows / size) % 2 == 0;
                    texture.SetPixel(x, y, painted ? Srgb(0x12, 0x12, 0x14) : Srgb(0xF2, 0xEE, 0xE2));
                }
            }

            texture.Apply();
            return Save(texture, path);
        }

        /// <summary>
        /// A latitude-longitude sky with a painted sun and cloud bands, so the
        /// sky, the sun light, and the fog all come from one golden-hour plan.
        /// </summary>
        public static Material Sky(Vector3 sunDirection, string name = "GoldenHour")
        {
            EnsureFolders();
            const int width = 1024;
            const int height = 512;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Vector3 sun = new Vector3(sunDirection.x, Mathf.Max(0.02f, sunDirection.y), sunDirection.z).normalized;

            Color horizon = Srgb(0xF7, 0xC4, 0x93);
            Color mid = Srgb(0x9D, 0xBA, 0xD2);
            Color zenith = Srgb(0x1F, 0x44, 0x70);
            Color below = Srgb(0x9A, 0x8A, 0x78);
            Color cloudLit = Srgb(0xFA, 0xD3, 0xA8);
            Color cloudShade = Srgb(0x8E, 0x9A, 0xAD);
            Color sunCore = Srgb(0xFF, 0xF4, 0xD6);

            for (int y = 0; y < height; y++)
            {
                // Texture rows run bottom to top, so the top row has to be the
                // zenith for the panoramic shader to put the sky overhead.
                float v = (y + 0.5f) / height;
                float elevation = (v - 0.5f) * Mathf.PI;

                for (int x = 0; x < width; x++)
                {
                    float u = (x + 0.5f) / width;
                    float azimuth = (u - 0.5f) * Mathf.PI * 2f;
                    var direction = new Vector3(
                        Mathf.Cos(elevation) * Mathf.Sin(azimuth),
                        Mathf.Sin(elevation),
                        Mathf.Cos(elevation) * Mathf.Cos(azimuth));

                    Color sky;
                    if (elevation >= 0f)
                    {
                        float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 1.35f, elevation));
                        sky = Color.Lerp(horizon, mid, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.2f, elevation)));
                        sky = Color.Lerp(sky, zenith, t);
                    }
                    else
                    {
                        sky = Color.Lerp(horizon, below, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.5f, -elevation)));
                    }

                    // Cloud bands, stretched along the horizon and thinning
                    // towards the zenith, so the sky has direction.
                    float band = Mathf.InverseLerp(0.04f, 0.35f, elevation) * Mathf.InverseLerp(1.1f, 0.45f, elevation);
                    if (band > 0f)
                    {
                        float cloud = Mathf.PerlinNoise(u * 7f + 11f, v * 26f) * 0.6f
                                      + Mathf.PerlinNoise(u * 17f + 4f, v * 52f) * 0.4f;
                        float mask = Mathf.SmoothStep(0.52f, 0.78f, cloud) * band * 0.85f;
                        float lit = Mathf.InverseLerp(0.5f, 0.9f, cloud);
                        sky = Color.Lerp(sky, Color.Lerp(cloudShade, cloudLit, lit), mask);
                    }

                    float sunDot = Mathf.Max(0f, Vector3.Dot(direction, sun));
                    sky += sunCore * Mathf.Pow(sunDot, 220f) * 1.2f;
                    sky += new Color(1f, 0.72f, 0.44f) * Mathf.Pow(sunDot, 8f) * 0.22f;
                    sky.a = 1f;
                    texture.SetPixel(x, y, sky);
                }
            }

            texture.Apply();
            string texturePath = Path.Combine(SkyFolder, name + ".png");
            Save(texture, texturePath, wrap: TextureWrapMode.Clamp);

            Shader shader = Shader.Find("Skybox/Panoramic");
            if (shader == null)
            {
                throw new IOException("The panoramic skybox shader is unavailable.");
            }

            string materialPath = Path.Combine(SkyFolder, name + ".mat");
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, materialPath);
            }

            material.shader = shader;
            material.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            material.SetFloat("_Mapping", 1f);
            material.SetFloat("_ImageType", 0f);
            material.SetFloat("_Exposure", 1.15f);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// The global Volume: bloom for the Kerbs and paint, a light camera
        /// motion blur for speed, and a warm/cool grade that ties the frame
        /// together. Weak by design; the Race has to stay readable at speed.
        /// </summary>
        public static VolumeProfile PostProcessing(string name = "Race Volume")
        {
            EnsureFolder(PostFolder);
            string path = Path.Combine(PostFolder, name + ".asset");
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }

            // Components have to become sub-assets, or the profile saves a list
            // of nulls and the scene renders with no post-processing at all.
            profile.components.RemoveAll(component => component == null);

            Tonemapping tonemapping = AddVolumeComponent<Tonemapping>(profile);
            tonemapping.mode.Override(TonemappingMode.ACES);

            Bloom bloom = AddVolumeComponent<Bloom>(profile);
            bloom.threshold.Override(0.95f);
            bloom.intensity.Override(0.55f);
            bloom.scatter.Override(0.68f);
            bloom.tint.Override(Srgb(0xFF, 0xE4, 0xC4));
            bloom.highQualityFiltering.Override(true);

            MotionBlur motionBlur = AddVolumeComponent<MotionBlur>(profile);
            motionBlur.mode.Override(MotionBlurMode.CameraOnly);
            motionBlur.quality.Override(MotionBlurQuality.Medium);
            motionBlur.intensity.Override(0.35f);

            ColorAdjustments grade = AddVolumeComponent<ColorAdjustments>(profile);
            grade.postExposure.Override(0.45f);
            grade.contrast.Override(10f);
            grade.saturation.Override(12f);
            grade.colorFilter.Override(Srgb(0xFF, 0xF0, 0xE0));

            WhiteBalance balance = AddVolumeComponent<WhiteBalance>(profile);
            balance.temperature.Override(7f);
            balance.tint.Override(-3f);

            SplitToning split = AddVolumeComponent<SplitToning>(profile);
            split.shadows.Override(Srgb(0x4C, 0x7A, 0x94));
            split.highlights.Override(Srgb(0xFF, 0xD9, 0xB0));
            split.balance.Override(-8f);

            Vignette vignette = AddVolumeComponent<Vignette>(profile);
            vignette.color.Override(Srgb(0x0B, 0x0D, 0x12));
            vignette.intensity.Override(0.18f);
            vignette.smoothness.Override(0.5f);
            vignette.rounded.Override(false);

            ChromaticAberration aberration = AddVolumeComponent<ChromaticAberration>(profile);
            aberration.intensity.Override(0.06f);

            FilmGrain grain = AddVolumeComponent<FilmGrain>(profile);
            grain.type.Override(FilmGrainLookup.Medium2);
            grain.intensity.Override(0.12f);
            grain.response.Override(0.75f);

            EditorUtility.SetDirty(profile);
            return profile;
        }

        /// <summary>
        /// Finds a component on the profile or creates it as a persisted
        /// sub-asset, so rebuilding the scene does not stack duplicates or
        /// lose the overrides when the editor reloads.
        /// </summary>
        private static T AddVolumeComponent<T>(VolumeProfile profile)
            where T : VolumeComponent
        {
            foreach (VolumeComponent component in profile.components)
            {
                if (component is T existing)
                {
                    return existing;
                }
            }

            T created = profile.Add<T>();
            AssetDatabase.AddObjectToAsset(created, profile);
            return created;
        }

        private static Material LoadOrCreate(string name)
        {
            EnsureFolder(MaterialsFolder);
            string path = Path.Combine(MaterialsFolder, name + ".mat");

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
            return material;
        }

        private static Texture2D Save(Texture2D texture, string path, TextureWrapMode wrap = TextureWrapMode.Repeat)
        {
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode = wrap;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = true;
            importer.anisoLevel = 4;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        public static void EnsureFolder(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                Directory.CreateDirectory(path);
                AssetDatabase.Refresh();
            }
        }
    }
}
