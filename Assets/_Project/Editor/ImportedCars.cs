using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unbounded.Editor
{
    /// <summary>
    /// The imported car bodies. Each FBX carries geometry and material names but
    /// not the per-part materials: the source project assigned those from its
    /// prefabs, so that assignment is vendored beside each model as
    /// <c>PartMaterials.json</c>. Spawning strips the parts a chase camera never
    /// sees, restores the imported materials by object name, paints the body
    /// panels in the car's livery, and gives every car the same opaque glass.
    /// </summary>
    internal static class ImportedCars
    {
        internal sealed class CarBody
        {
            public readonly string Name;
            public readonly string AssetPath;

            /// <summary>The imported material that covers the paintable panels.</summary>
            public readonly string PaintMaterial;

            /// <summary>
            /// Parts that share the paint material in the source project but are
            /// not body panels, so they keep the imported material.
            /// </summary>
            public readonly string[] KeepImported;

            public CarBody(string name, string assetPath, string paintMaterial, params string[] keepImported)
            {
                Name = name;
                AssetPath = assetPath;
                PaintMaterial = paintMaterial;
                KeepImported = keepImported;
            }
        }

        private const string CarFolder = "Assets/_Project/Art/Imported/Cars";

        /// <summary>The player's car. Its body panels are the Testarossa's paint.</summary>
        public static readonly CarBody Ferrari = new CarBody(
            "Ferrari",
            Path.Combine(CarFolder, "Ferrari/Ferrari.fbx"),
            "primary");

        /// <summary>A wide, fully textured opponent.</summary>
        public static readonly CarBody R35 = new CarBody(
            "R35",
            Path.Combine(CarFolder, "R35/R35.fbx"),
            "body");

        /// <summary>The second opponent body: one flat material from nose to tail.</summary>
        public static readonly CarBody C63 = new CarBody(
            "C63",
            Path.Combine(CarFolder, "C63/C63.fbx"),
            "C63",
            // The C63's single material also covers its chrome, lamp housings,
            // and window trim. Those stay black gloss instead of the livery.
            // "st_" is the source's steklo (glass) prefix on its side windows.
            "chrome", "lamp", "windows_line", "st_");

        // Whole objects that a chase camera never sees, or that the source
        // project carried as GTA collision and shadow meshes. Matching is by
        // name fragment because every car numbers its parts differently.
        private static readonly string[] HiddenParts =
        {
            "interior", "cabin", "dashboard", "dash", "seat", "steer",
            "gauges", "gauge", "leather", "fablic", "torpeda", "engine",
            "colmesh", "car_col", "cheetah_col", "shadow"
        };

        // Glazed parts. Their paint slot is trim, never the livery, and every
        // transparent slot becomes the opaque car glass.
        private static readonly string[] GlassParts =
        {
            "window", "windscreen", "glass"
        };

        /// <summary>The imported car's four wheel anchors: front-left, front-right, rear-left, rear-right.</summary>
        internal static readonly string[] WheelDummies =
        {
            "wheel_lf_dummy", "wheel_rf_dummy", "wheel_lb_dummy", "wheel_rb_dummy"
        };

        /// <summary>The one dummy the source model actually ships a wheel mesh under.</summary>
        internal const string WheelSourceDummy = "wheel_rf_dummy";

        private static readonly Dictionary<string, Dictionary<string, string[]>> PartMaps =
            new Dictionary<string, Dictionary<string, string[]>>();

        private static readonly HashSet<string> Warned = new HashSet<string>();

        private static readonly HashSet<string> Logged = new HashSet<string>();

        public static GameObject Spawn(
            CarBody body,
            Transform parent,
            Material paint,
            Material glass,
            Material trim)
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(body.AssetPath);
            if (asset == null)
            {
                throw new IOException("Missing imported car at " + body.AssetPath);
            }

            GameObject instance = UnityEngine.Object.Instantiate(asset, parent, false);
            instance.name = "Visual";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localScale = Vector3.one;

            // The source bodies model their nose down -Z, while the car root
            // drives along +Z, so every body is yawed to face the way it travels.
            instance.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            // Cars are longer than they are wide. If the source model's long
            // axis came in on X, turn it onto Z so the scene builder's length
            // normalisation and the nitro jets line up with the body.
            Bounds bounds = KitAssets.BoundsIn(instance, parent);
            if (bounds.size.x > bounds.size.z)
            {
                instance.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            }

            StripHiddenParts(instance);
            ImportMaterials(instance, body, trim);
            PaintBody(instance, body, paint, trim);
            ReplaceGlass(instance, body, glass);
            VerifyMaterials(instance, body, paint);
            CompleteWheels(instance, body);
            return instance;
        }

        private static void StripHiddenParts(GameObject instance)
        {
            var doomed = new List<GameObject>();
            foreach (Transform child in instance.GetComponentsInChildren<Transform>(true))
            {
                if (child == instance.transform)
                {
                    continue;
                }

                if (ContainsAny(child.name, HiddenParts))
                {
                    doomed.Add(child.gameObject);
                }
            }

            foreach (GameObject child in doomed)
            {
                // A doomed parent takes its children with it, so the list can
                // hold objects that a previous DestroyImmediate already removed.
                if (child != null)
                {
                    UnityEngine.Object.DestroyImmediate(child);
                }
            }
        }

        /// <summary>
        /// Restores the source project's per-part materials, which the FBX only
        /// names. Glass alpha, tyre rubber, chrome, and lamp textures all live
        /// in those materials; without them the bodies would render as white
        /// URP defaults. A part the source prefab never assigned falls back to
        /// its own vendored materials, or to trim: the FBX leaves those slots
        /// null, and a null slot renders Unity's magenta error material.
        /// </summary>
        private static void ImportMaterials(GameObject instance, CarBody body, Material trim)
        {
            Dictionary<string, string[]> parts = LoadPartMap(body);
            Dictionary<string, Material> imported = LoadMaterials(body);
            var unmatched = new List<string>();

            EditMaterialSlots(instance, (renderer, slots) =>
            {
                string part = Clean(renderer.name);
                if (!parts.TryGetValue(part, out string[] names))
                {
                    unmatched.Add(part);
                    UseFallbackMaterials(part, imported, slots, trim);
                    return;
                }

                if (slots.Length != names.Length)
                {
                    throw new IOException(
                        "The imported " + body.Name + " part '" + part + "' has " +
                        slots.Length + " mesh slots but " + names.Length +
                        " vendored materials. The model and its part map disagree.");
                }

                for (int i = 0; i < names.Length; i++)
                {
                    if (!imported.TryGetValue(names[i], out Material material))
                    {
                        throw new IOException(
                            "The imported " + body.Name + " part '" + part +
                            "' needs material '" + names[i] + "', which is not vendored.");
                    }

                    slots[i] = material;
                }
            });

            if (unmatched.Count > 0 && Warned.Add(body.Name))
            {
                Debug.LogWarning(
                    "The imported " + body.Name + " has parts with no vendored material " +
                    "assignment: " + string.Join(", ", unmatched) + ". Their slots were " +
                    "filled from the vendored material names or the black trim.");
            }
        }

        /// <summary>
        /// Fills a part the source prefab left unassigned. Vendored materials are
        /// named after their part, so a part with a full set (<c>headlight_l.0</c>
        /// through <c>headlight_l.5</c>) takes them by index, a part with a single
        /// match (<c>plate.1</c>) takes it everywhere, and anything else is trim.
        /// </summary>
        private static void UseFallbackMaterials(
            string part,
            Dictionary<string, Material> imported,
            Material[] slots,
            Material trim)
        {
            var named = new SortedDictionary<int, Material>();
            string prefix = part + ".";
            foreach (KeyValuePair<string, Material> material in imported)
            {
                if (material.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                    int.TryParse(material.Key.Substring(prefix.Length), out int index))
                {
                    named[index] = material.Value;
                }
            }

            bool complete = named.Count == slots.Length;
            if (complete)
            {
                foreach (int index in named.Keys)
                {
                    if (index >= slots.Length)
                    {
                        complete = false;
                        break;
                    }
                }
            }

            if (complete)
            {
                foreach (KeyValuePair<int, Material> material in named)
                {
                    slots[material.Key] = material.Value;
                }

                return;
            }

            if (named.Count == 1)
            {
                Material only = null;
                foreach (KeyValuePair<int, Material> material in named)
                {
                    only = material.Value;
                }

                for (int i = 0; i < slots.Length; i++)
                {
                    slots[i] = only;
                }

                return;
            }

            for (int i = 0; i < slots.Length; i++)
            {
                slots[i] = trim;
            }
        }

        private static Dictionary<string, string[]> LoadPartMap(CarBody body)
        {
            if (PartMaps.TryGetValue(body.AssetPath, out Dictionary<string, string[]> cached))
            {
                return cached;
            }

            string path = Path.Combine(Path.GetDirectoryName(body.AssetPath), "PartMaterials.json");
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            if (asset == null)
            {
                throw new IOException("Missing part material map at " + path);
            }

            var map = JsonUtility.FromJson<PartMap>(asset.text);
            if (map == null || map.entries == null || map.entries.Length == 0)
            {
                throw new IOException("Empty part material map at " + path);
            }

            var parts = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            foreach (Part part in map.entries)
            {
                parts[part.name] = part.materials;
            }

            PartMaps[body.AssetPath] = parts;
            return parts;
        }

        private static void PaintBody(GameObject instance, CarBody body, Material paint, Material trim)
        {
            var names = new HashSet<string>();
            bool painted = false;

            EditMaterialSlots(instance, (renderer, slots) =>
            {
                string part = Clean(renderer.name);
                if (ContainsAny(part, body.KeepImported))
                {
                    return;
                }

                bool glazed = ContainsAny(part, GlassParts);

                for (int i = 0; i < slots.Length; i++)
                {
                    if (slots[i] == null || IsTransparent(slots[i]))
                    {
                        continue;
                    }

                    string name = Clean(slots[i].name);
                    names.Add(name);

                    if (!string.Equals(name, body.PaintMaterial, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    // Trim that belongs to the glazing reads as black gloss, not
                    // as another painted body panel.
                    slots[i] = glazed ? trim : paint;
                    painted |= !glazed;
                }
            });

            if (!painted)
            {
                throw new IOException(
                    "The imported " + body.Name + " has no material named '" +
                    body.PaintMaterial + "'. It has: " + string.Join(", ", names));
            }
        }

        /// <summary>
        /// Replaces every transparent slot with the car's opaque glass. The
        /// vendored glass is semi-transparent with no cabin behind it: the
        /// interiors are stripped, so it shows the hollow shell and whatever
        /// the source left in there. Opaque dark glass reads as tint at chase
        /// distance and costs one material for the whole field.
        /// </summary>
        private static void ReplaceGlass(GameObject instance, CarBody body, Material glass)
        {
            var replaced = new List<string>();
            EditMaterialSlots(instance, (renderer, slots) =>
            {
                for (int i = 0; i < slots.Length; i++)
                {
                    if (IsTransparent(slots[i]))
                    {
                        replaced.Add(Clean(slots[i].name));
                        slots[i] = glass;
                    }
                }
            });

            if (replaced.Count > 0 && Logged.Add(body.Name + " glass"))
            {
                Debug.Log(
                    "The imported " + body.Name + " renders " + string.Join(", ", replaced) +
                    " as opaque car glass.");
            }
        }

        /// <summary>
        /// Fails the build on a null material slot, which renders Unity's magenta
        /// error material in the field, and warns if a glazed part still carries
        /// the livery. Both would otherwise reach the player silently.
        /// </summary>
        private static void VerifyMaterials(GameObject instance, CarBody body, Material paint)
        {
            foreach (MeshRenderer renderer in instance.GetComponentsInChildren<MeshRenderer>(true))
            {
                Material[] slots = renderer.sharedMaterials;
                string part = Clean(renderer.name);

                for (int i = 0; i < slots.Length; i++)
                {
                    if (slots[i] == null)
                    {
                        throw new IOException(
                            "The imported " + body.Name + " part '" + part +
                            "' has an unassigned material slot, which renders magenta.");
                    }

                    if (slots[i] == paint && ContainsAny(part, GlassParts) &&
                        Warned.Add(body.Name + " glass livery"))
                    {
                        Debug.LogWarning(
                            "The imported " + body.Name + " part '" + part +
                            "' still takes the car's livery on its glazing.");
                    }
                }
            }
        }

        private static bool IsTransparent(Material material) =>
            material != null &&
            (material.renderQueue >= (int)RenderQueue.Transparent ||
             (material.HasProperty("_Surface") && material.GetFloat("_Surface") > 0.5f));

        /// <summary>
        /// The source bodies carry one real wheel and three empty anchors. Copy
        /// the right-front wheel onto the other anchors so every car has four
        /// tyres. The left pair is not mirrored here: the scene builder mounts
        /// each wheel on a car-space pivot, where mirroring is unambiguous.
        /// </summary>
        private static void CompleteWheels(GameObject instance, CarBody body)
        {
            Transform source = FindWheelDummy(instance.transform, WheelSourceDummy, body.Name);
            var sourceChildren = new List<Transform>();
            foreach (Transform child in source)
            {
                sourceChildren.Add(child);
            }

            if (sourceChildren.Count == 0 ||
                source.GetComponentsInChildren<MeshRenderer>(true).Length == 0)
            {
                throw new IOException(
                    "The imported " + body.Name + " has no wheel mesh under " + WheelSourceDummy + ".");
            }

            foreach (string name in WheelDummies)
            {
                if (name == WheelSourceDummy)
                {
                    continue;
                }

                Transform dummy = FindWheelDummy(instance.transform, name, body.Name);

                // The Ferrari carries bare rims on the other anchors; drop them
                // so every corner gets the source's tyre-and-rim assembly.
                for (int i = dummy.childCount - 1; i >= 0; i--)
                {
                    UnityEngine.Object.DestroyImmediate(dummy.GetChild(i).gameObject);
                }

                foreach (Transform child in sourceChildren)
                {
                    GameObject clone = UnityEngine.Object.Instantiate(child.gameObject, dummy, false);
                    clone.name = child.name;
                }
            }
        }

        /// <summary>Finds a named anchor inside a car, or fails the build rather than leaving a wheel-less car.</summary>
        internal static Transform FindWheelDummy(Transform visual, string name, string carName)
        {
            foreach (Transform child in visual.GetComponentsInChildren<Transform>(true))
            {
                if (string.Equals(child.name, name, StringComparison.Ordinal))
                {
                    return child;
                }
            }

            throw new IOException("The imported " + carName + " has no " + name + ".");
        }

        /// <summary>Gives every renderer its material slots once, then saves them back.</summary>
        private static void EditMaterialSlots(GameObject instance, Action<MeshRenderer, Material[]> edit)
        {
            foreach (MeshRenderer renderer in instance.GetComponentsInChildren<MeshRenderer>(true))
            {
                Material[] slots = renderer.sharedMaterials;
                edit(renderer, slots);
                renderer.sharedMaterials = slots;
            }
        }

        private static Dictionary<string, Material> LoadMaterials(CarBody body)
        {
            string folder = Path.Combine(Path.GetDirectoryName(body.AssetPath), "Materials");
            var found = new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase);

            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material != null)
                {
                    found[Path.GetFileNameWithoutExtension(path)] = material;
                }
            }

            if (found.Count == 0)
            {
                throw new IOException("No vendored materials in " + folder);
            }

            return found;
        }

        private static bool ContainsAny(string value, string[] fragments)
        {
            foreach (string fragment in fragments)
            {
                if (value.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static string Clean(string name) =>
            name.EndsWith(" (Instance)", StringComparison.Ordinal)
                ? name.Substring(0, name.Length - " (Instance)".Length)
                : name;

        [Serializable]
        private sealed class PartMap
        {
            public string source;
            public Part[] entries;
        }

        [Serializable]
        private sealed class Part
        {
            public string name;
            public string[] materials;
        }
    }
}
