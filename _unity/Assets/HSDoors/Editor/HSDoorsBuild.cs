using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// Import settings for the Blender FBX files: 1 unit = 1 m, axis already baked by the exporter.
public class HSDoorsModelImport : AssetPostprocessor
{
    void OnPreprocessModel()
    {
        if (!assetPath.Replace('\\', '/').Contains("/HSDoors/Models/")) return;
        var mi = (ModelImporter)assetImporter;
        mi.globalScale = 1f;
        mi.useFileScale = true;
        mi.bakeAxisConversion = false;
        mi.preserveHierarchy = true;
        mi.importCameras = false;
        mi.importLights = false;
        mi.importAnimation = false;
        mi.animationType = ModelImporterAnimationType.None;
        mi.importBlendShapes = false;
        mi.addCollider = false;
        mi.isReadable = false;
        mi.importNormals = ModelImporterNormals.Import;
        mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        mi.materialLocation = ModelImporterMaterialLocation.InPrefab;
        mi.meshCompression = ModelImporterMeshCompression.Off;
    }
}

public static class HSDoorsBuild
{
    const string Root = "Assets/HSDoors";
    const string BundleName = "hsdoors";
    const string BlockTag = "T_Block";

    [MenuItem("HS Doors/Build Bundle")]
    public static void BuildMenu()
    {
        Run(false);
    }

    // Batch entry: Unity.exe -batchmode -projectPath _unity -executeMethod HSDoorsBuild.Build -quit
    public static void Build()
    {
        Run(true);
    }

    static void Run(bool exit)
    {
        var log = new StringBuilder();
        int code = 0;
        try
        {
            EnsureTag(BlockTag);
            EnsureFolder(Root + "/Materials");
            EnsureFolder(Root + "/Prefabs");
            var mats = EnsureMaterials();
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

            int made = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { Root + "/Models" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var src = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (src == null) continue;
                var go = UnityEngine.Object.Instantiate(src);
                go.name = src.name;
                int cols = Process(go, mats);
                var prefabPath = Root + "/Prefabs/" + src.name + ".prefab";
                PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
                var b = RenderBounds(go);
                UnityEngine.Object.DestroyImmediate(go);
                AssetImporter.GetAtPath(prefabPath).SetAssetBundleNameAndVariant(BundleName, "");
                log.AppendLine(string.Format("{0}: bounds min {1} max {2}, colliders {3}", src.name, V(b.min), V(b.max), cols));
                made++;
            }
            AssetDatabase.SaveAssets();
            if (made == 0) throw new Exception("No models found in " + Root + "/Models");

            var outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "BundleOut"));
            Directory.CreateDirectory(outDir);
            var manifest = BuildPipeline.BuildAssetBundles(outDir,
                BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.ForceRebuildAssetBundle,
                BuildTarget.StandaloneWindows64);
            if (manifest == null) throw new Exception("BuildAssetBundles returned null");

            var modResources = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Resources"));
            Directory.CreateDirectory(modResources);
            var dest = Path.Combine(modResources, "HSDoors.unity3d");
            File.Copy(Path.Combine(outDir, BundleName), dest, true);
            log.AppendLine("Bundle: " + dest + " (" + new FileInfo(dest).Length + " bytes, " + made + " prefabs)");
            log.AppendLine("DONE");
        }
        catch (Exception e)
        {
            code = 1;
            log.AppendLine("FAILED: " + e);
        }
        var report = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "build_report.txt"));
        File.WriteAllText(report, log.ToString());
        Debug.Log("[HSDoorsBuild]\n" + log);
        if (exit) EditorApplication.Exit(code);
    }

    static string V(Vector3 v)
    {
        return string.Format("({0:0.###}, {1:0.###}, {2:0.###})", v.x, v.y, v.z);
    }

    static Bounds RenderBounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0) return new Bounds();
        var b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }

    // COL_* children become box colliders; everything else gets the HS materials by slot name.
    static int Process(GameObject go, Dictionary<string, Material> mats)
    {
        int cols = 0;
        go.tag = BlockTag;
        go.layer = 0;
        foreach (var t in go.GetComponentsInChildren<Transform>(true))
        {
            t.gameObject.layer = 0;
            if (t.name.StartsWith("COL_", StringComparison.OrdinalIgnoreCase))
            {
                var mf = t.GetComponent<MeshFilter>();
                var bounds = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds : new Bounds(Vector3.zero, Vector3.one);
                var r = t.GetComponent<MeshRenderer>();
                if (r != null) UnityEngine.Object.DestroyImmediate(r);
                if (mf != null) UnityEngine.Object.DestroyImmediate(mf);
                var bc = t.gameObject.AddComponent<BoxCollider>();
                bc.center = bounds.center;
                bc.size = bounds.size;
                t.gameObject.tag = BlockTag;
                cols++;
                continue;
            }
            var mr = t.GetComponent<MeshRenderer>();
            if (mr == null) continue;
            var shared = mr.sharedMaterials;
            for (int i = 0; i < shared.Length; i++)
            {
                var key = shared[i] != null ? CleanName(shared[i].name) : "";
                Material m;
                if (mats.TryGetValue(key, out m)) shared[i] = m;
                else Debug.LogWarning("[HSDoorsBuild] No material for slot '" + key + "' on " + go.name);
            }
            mr.sharedMaterials = shared;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = true;
            t.gameObject.tag = BlockTag;
        }
        return cols;
    }

    static string CleanName(string n)
    {
        n = n.Replace(" (Instance)", "");
        int dot = n.IndexOf('.');
        if (dot > 0) n = n.Substring(0, dot);
        return n.Trim();
    }

    static Dictionary<string, Material> EnsureMaterials()
    {
        var d = new Dictionary<string, Material>();
        d["HS_Glass"] = Glass("HS_Glass", new Color(0.72f, 0.84f, 0.9f, 0.22f));
        d["HS_FrameSilver"] = Opaque("HS_FrameSilver", new Color(0.78f, 0.79f, 0.8f), 0.85f, 0.55f);
        d["HS_FrameDark"] = Opaque("HS_FrameDark", new Color(0.07f, 0.07f, 0.08f), 0.6f, 0.5f);
        d["HS_Rubber"] = Opaque("HS_Rubber", new Color(0.02f, 0.02f, 0.02f), 0f, 0.2f);
        d["HS_Mat"] = Opaque("HS_Mat", new Color(0.11f, 0.11f, 0.12f), 0f, 0.08f);
        d["HS_Floor"] = Opaque("HS_Floor", new Color(0.35f, 0.35f, 0.36f), 0f, 0.3f);
        d["HS_Sensor"] = Opaque("HS_Sensor", new Color(0.03f, 0.03f, 0.035f), 0.2f, 0.8f);
        var light = Opaque("HS_Light", new Color(1f, 0.97f, 0.9f), 0f, 0.5f);
        light.EnableKeyword("_EMISSION");
        light.SetColor("_EmissionColor", new Color(1f, 0.97f, 0.9f) * 1.6f);
        light.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        EditorUtility.SetDirty(light);
        d["HS_Light"] = light;
        AssetDatabase.SaveAssets();
        return d;
    }

    static Material Load(string name)
    {
        var path = Root + "/Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.shader = Shader.Find("Standard");
        return m;
    }

    static Material Opaque(string name, Color c, float metallic, float smooth)
    {
        var m = Load(name);
        m.SetFloat("_Mode", 0f);
        m.SetOverrideTag("RenderType", "");
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
        m.SetInt("_ZWrite", 1);
        m.DisableKeyword("_ALPHATEST_ON");
        m.DisableKeyword("_ALPHABLEND_ON");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        m.renderQueue = -1;
        m.color = c;
        m.SetFloat("_Metallic", metallic);
        m.SetFloat("_Glossiness", smooth);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material Glass(string name, Color c)
    {
        var m = Load(name);
        m.SetFloat("_Mode", 3f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 0);
        m.DisableKeyword("_ALPHATEST_ON");
        m.DisableKeyword("_ALPHABLEND_ON");
        m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        m.color = c;
        m.SetFloat("_Metallic", 0.1f);
        m.SetFloat("_Glossiness", 0.95f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    static void EnsureTag(string tag)
    {
        var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (assets == null || assets.Length == 0) return;
        var so = new SerializedObject(assets[0]);
        var tags = so.FindProperty("tags");
        for (int i = 0; i < tags.arraySize; i++)
            if (tags.GetArrayElementAtIndex(i).stringValue == tag) return;
        tags.InsertArrayElementAtIndex(tags.arraySize);
        tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tag;
        so.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
    }
}
