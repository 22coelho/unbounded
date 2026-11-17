using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Unbounded.Editor
{
    /// <summary>
    /// Brings the free Kenney kits into the scene. Both kits are CC0; the FBX
    /// files live under Art/Kits and the importer is configured once, from code,
    /// so a fresh clone gets the same flat-shaded meshes without hand setup.
    /// </summary>
    internal static class KitAssets
    {
        public const string Racing = "Assets/_Project/Art/Kits/Racing/Models";
        public const string Nature = "Assets/_Project/Art/Kits/Nature/Models";
        public const string RacingTextures = "Assets/_Project/Art/Kits/Racing/Textures";

        public static void PrepareImports()
        {
            ConfigureModels(Racing);
            ConfigureModels(Nature);

            ConfigureTexture(Path.Combine(RacingTextures, "checkers.png"), TextureWrapMode.Clamp);
            ConfigureTexture(Path.Combine(RacingTextures, "tankco.png"), TextureWrapMode.Clamp);
            ConfigureTexture(Path.Combine(RacingTextures, "net.png"), TextureWrapMode.Repeat);
        }

        public static GameObject Load(string folder, string model)
        {
            string path = Path.Combine(folder, model + ".fbx");
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null)
            {
                throw new IOException("Missing Kit model at " + path);
            }

            return asset;
        }

        /// <summary>
        /// Instantiates a Kit model and replaces the imported materials with the
        /// palette's. The imported materials are only used for their names, which
        /// tells the palette which part of the model each submesh is.
        /// </summary>
        public static GameObject Spawn(
            string folder,
            string model,
            KitPalette palette,
            Transform parent,
            Vector3 position,
            Quaternion rotation,
            Vector3 scale,
            Material bodyOverride = null)
        {
            var instance = (GameObject)Object.Instantiate(Load(folder, model));
            instance.name = model;

            foreach (MeshRenderer renderer in instance.GetComponentsInChildren<MeshRenderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    materials[i] = palette.Resolve(Key(materials[i]), bodyOverride);
                }

                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }

            return Place(instance, parent, position, rotation, scale);
        }

        public static GameObject Spawn(
            string folder,
            string model,
            KitPalette palette,
            Transform parent,
            Vector3 position,
            Quaternion rotation,
            float scale,
            Material bodyOverride = null)
        {
            return Spawn(folder, model, palette, parent, position, rotation, Vector3.one * scale, bodyOverride);
        }

        /// <summary>
        /// Seeds an instance on a point: centred on it left to right and front
        /// to back, and resting its lowest vertex on it.
        /// </summary>
        public static GameObject Place(
            GameObject instance,
            Transform parent,
            Vector3 position,
            Quaternion rotation,
            Vector3 scale)
        {
            instance.transform.SetParent(parent, false);
            instance.transform.localRotation = rotation;
            instance.transform.localScale = scale;
            instance.transform.localPosition = position;

            // Bounds are measured in the parent's space and `position` is
            // expressed in the same space, so the correction has to include it:
            // the model's own pivot is rarely on its footprint.
            Bounds bounds = BoundsIn(instance, parent);
            instance.transform.localPosition += new Vector3(
                position.x - bounds.center.x,
                position.y - bounds.min.y,
                position.z - bounds.center.z);
            return instance;
        }

        /// <summary>The bounds of every renderer under a root, expressed in a space.</summary>
        public static Bounds BoundsIn(GameObject root, Transform space)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            bool any = false;
            Bounds total = default;

            foreach (Renderer renderer in renderers)
            {
                Bounds world = renderer.bounds;
                var corners = new Vector3[8];
                for (int i = 0; i < 8; i++)
                {
                    corners[i] = new Vector3(
                        (i & 1) == 0 ? world.min.x : world.max.x,
                        (i & 2) == 0 ? world.min.y : world.max.y,
                        (i & 4) == 0 ? world.min.z : world.max.z);
                    if (space != null)
                    {
                        corners[i] = space.InverseTransformPoint(corners[i]);
                    }
                }

                var bounds = new Bounds(corners[0], Vector3.zero);
                for (int i = 1; i < 8; i++)
                {
                    bounds.Encapsulate(corners[i]);
                }

                if (any)
                {
                    total.Encapsulate(bounds);
                }
                else
                {
                    total = bounds;
                    any = true;
                }
            }

            return total;
        }

        private static string Key(Material material)
        {
            if (material == null)
            {
                return "grey";
            }

            string name = material.name;
            int suffix = name.IndexOf(" (Instance)", System.StringComparison.Ordinal);
            if (suffix >= 0)
            {
                name = name.Substring(0, suffix);
            }

            int separator = name.LastIndexOf(':');
            if (separator >= 0)
            {
                name = name.Substring(separator + 1);
            }

            return name;
        }

        private static void ConfigureModels(string folder)
        {
            foreach (string path in Directory.GetFiles(folder, "*.fbx"))
            {
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null)
                {
                    continue;
                }

                bool dirty = importer.globalScale != 1f
                             || !importer.useFileScale
                             || importer.importAnimation
                             || importer.animationType != ModelImporterAnimationType.None
                             || importer.importBlendShapes
                             || importer.addCollider
                             || importer.generateSecondaryUV
                             || importer.meshCompression != ModelImporterMeshCompression.Off
                             || importer.materialImportMode != ModelImporterMaterialImportMode.ImportViaMaterialDescription
                             || importer.materialLocation != ModelImporterMaterialLocation.InPrefab;

                if (!dirty)
                {
                    continue;
                }

                importer.globalScale = 1f;
                importer.useFileScale = true;
                importer.importAnimation = false;
                importer.animationType = ModelImporterAnimationType.None;
                importer.importBlendShapes = false;
                importer.addCollider = false;
                importer.generateSecondaryUV = false;
                importer.meshCompression = ModelImporterMeshCompression.Off;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
                importer.SaveAndReimport();
            }
        }

        private static void ConfigureTexture(string path, TextureWrapMode wrap)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            if (importer.wrapMode == wrap && importer.mipmapEnabled && importer.alphaIsTransparency)
            {
                return;
            }

            importer.wrapMode = wrap;
            importer.mipmapEnabled = true;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }
    }

    /// <summary>The Kit's material names mapped onto the palette's shared materials.</summary>
    internal sealed class KitPalette
    {
        private readonly Dictionary<string, Material> _materials = new Dictionary<string, Material>();

        public void Add(string key, Material material)
        {
            _materials[key] = material;
        }

        public Material Resolve(string key, Material bodyOverride = null)
        {
            if (bodyOverride != null && key == "red")
            {
                return bodyOverride;
            }

            if (_materials.TryGetValue(key, out Material material))
            {
                return material;
            }

            throw new KeyNotFoundException(
                "The Kit uses a material the palette does not know: '" + key + "'. Add it to ArtPalette.");
        }
    }
}
