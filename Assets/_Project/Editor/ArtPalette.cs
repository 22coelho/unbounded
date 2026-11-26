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

        // The cars' glazing and window trim. The vendored glass is
        // semi-transparent, which shows the stripped, hollow cabin behind it,
        // so every car renders opaque dark glass instead. A paint slot on a
        // glazed part falls back to this black gloss rather than the livery:
        // the R35's Windows part and the Ferrari's windscreen both carry the
        // body material on a pane.
        public static readonly Color CarGlassDark = Linear(0x12, 0x16, 0x1B);
        public static readonly Color CarTrimDark = Linear(0x0E, 0x10, 0x13);

        // Light and air. The sun sits low and warm, the sky answers with a
        // cooler zenith, and the fog takes the horizon colour so the ground
        // edge dissolves instead of ending.
        public static readonly Color Sun = Linear(0xFF, 0xDD, 0xB8);
        public static readonly Color Fill = Linear(0x8F, 0xB6, 0xE0);
        public static readonly Color SkyHorizon = Linear(0xF2, 0xBE, 0x8E);
        public static readonly Color SkyZenith = Linear(0x24, 0x4C, 0x78);

        // Averages of the imported panorama's upper half, horizon band, and
        // lower half. They drive a trilight ambient so the built player is lit
        // by the sky it shows instead of a probe that only exists in the editor.
        public static readonly Color AmbientSky = Linear(0x8F, 0x93, 0xBA);
        public static readonly Color AmbientHorizon = Linear(0xBC, 0xAA, 0xBE);
        public static readonly Color AmbientGround = Linear(0x5F, 0x4C, 0x61);
        public static readonly Color Fog = Linear(0xD9, 0xB5, 0x95);
        public static readonly Color NitroFlame = Linear(0xFF, 0x8A, 0x2E);

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

        /// <summary>
        /// The physically based shader. It is the palette default, but the flat
        /// Road and Grass need <see cref="SimpleLitShader"/> instead: in a built
        /// player their perfectly horizontal normals come out black under this
        /// shader while every other Lit surface in the frame is correct.
        /// </summary>
        public const string LitShader = "Universal Render Pipeline/Lit";

        /// <summary>
        /// The Blinn-Phong shader. It still takes the sun, its shadows, and the
        /// trilight ambient, so it is a drop-in for the two flat surfaces.
        /// </summary>
        public const string SimpleLitShader = "Universal Render Pipeline/Simple Lit";

        /// <summary>A URP Lit material, reused across builds so the scene keeps its references.</summary>
        public static Material Lit(
            string name,
            Color color,
            float smoothness,
            float metallic = 0f,
            Texture2D map = null,
            Vector2 tiling = default,
            Vector2 offset = default,
            string shaderName = LitShader)
        {
            Material material = LoadOrCreate(name);
            Shader shader = Shader.Find(shaderName);
            if (shader != null && material.shader != shader)
            {
                material.shader = shader;
            }

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
                material.SetTextureOffset("_BaseMap", offset);
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

        /// <summary>
        /// The Road, Grass, and sky are imported photographs rather than
        /// generated textures, so their import settings are part of the look:
        /// big enough to hold up at speed, repeating where the Track tiles them,
        /// and clamped where the sky must not wrap.
        /// </summary>
        public static void PrepareImports()
        {
            ConfigureTexture(Path.Combine(TexturesFolder, "Asphalt.png"), 2048, TextureWrapMode.Repeat);
            ConfigureTexture(Path.Combine(TexturesFolder, "Grass.png"), 2048, TextureWrapMode.Repeat);
            ConfigureTexture(Path.Combine(SkyFolder, "GoldenHour.png"), 4096, TextureWrapMode.Clamp);
        }

        private static void ConfigureTexture(string path, int maxSize, TextureWrapMode wrap)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                throw new IOException("Missing imported texture at " + path);
            }

            bool dirty = false;
            if (importer.maxTextureSize < maxSize)
            {
                importer.maxTextureSize = maxSize;
                dirty = true;
            }

            if (importer.wrapMode != wrap)
            {
                importer.wrapMode = wrap;
                dirty = true;
            }

            if (!importer.mipmapEnabled)
            {
                importer.mipmapEnabled = true;
                dirty = true;
            }

            if (!importer.sRGBTexture)
            {
                importer.sRGBTexture = true;
                dirty = true;
            }

            if (importer.textureCompression != TextureImporterCompression.CompressedHQ)
            {
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                dirty = true;
            }

            if (dirty)
            {
                importer.SaveAndReimport();
            }
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
        /// The imported sunset panorama, rotated so its painted sun sits on the
        /// same azimuth as the directional light the Track is lit by.
        /// </summary>
        public static Material Sky(Vector3 sunDirection, string name = "GoldenHour")
        {
            EnsureFolders();
            string texturePath = Path.Combine(SkyFolder, name + ".png");
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (texture == null)
            {
                throw new IOException("Missing imported sky at " + texturePath);
            }

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
            material.SetTexture("_MainTex", texture);
            material.SetFloat("_Mapping", 1f);
            material.SetFloat("_ImageType", 0f);
            material.SetFloat("_Exposure", 1f);
            material.SetFloat("_Rotation", SkyRotation(sunDirection));
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// The panorama paints its sun at the centre column, which is the
        /// panoramic shader's zero azimuth, so the light's own azimuth becomes
        /// the sky's rotation and the painted disc sits behind the sun light.
        /// </summary>
        private static float SkyRotation(Vector3 sunDirection)
        {
            return Mathf.Repeat(Mathf.Atan2(sunDirection.x, sunDirection.z) * Mathf.Rad2Deg, 360f);
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
            grade.postExposure.Override(0.6f);
            grade.contrast.Override(4f);
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
            vignette.intensity.Override(0.05f);
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
