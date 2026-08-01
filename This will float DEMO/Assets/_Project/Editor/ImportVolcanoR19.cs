using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class ImportVolcanoR19
{
    private const string Root = "Assets/_Project/Art/Volcano/Hero_R19";
    private const string ModelPath = Root + "/Models/Volcano_Hero_R19.fbx";
    private const string AlbedoPath = Root + "/Textures/Volcano_Hero_R19_Albedo.png";
    private const string NormalPath = Root + "/Textures/Volcano_Hero_R19_Normal.png";
    private const string RoughnessPath = Root + "/Textures/Volcano_Hero_R19_Roughness.png";
    private const string AoPath = Root + "/Textures/Volcano_Hero_R19_AO.png";
    private const string MetallicSmoothnessPath = Root + "/Textures/Volcano_Hero_R19_MetallicSmoothness.png";
    private const string MaterialPath = Root + "/Materials/Volcano_Hero_R19_URP.mat";
    private const string PrefabPath = Root + "/Prefabs/Volcano_Hero_R19.prefab";
    private const string SessionKey = "ThisWillFloat.VolcanoR19.AutoImport";

    static ImportVolcanoR19()
    {
        if (Application.isBatchMode || SessionState.GetBool(SessionKey, false)) return;
        SessionState.SetBool(SessionKey, true);
        EditorApplication.delayCall += () =>
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
                Run();
        };
    }

    [MenuItem("Tools/This Will Float/Import Volcano R19")]
    public static void Run()
    {
        Directory.CreateDirectory(Path.Combine(Application.dataPath, "_Project/Art/Volcano/Hero_R19/Materials"));
        Directory.CreateDirectory(Path.Combine(Application.dataPath, "_Project/Art/Volcano/Hero_R19/Prefabs"));
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        ConfigureColorTexture(AlbedoPath, true);
        ConfigureNormalTexture(NormalPath);
        ConfigureDataTexture(RoughnessPath, true);
        ConfigureDataTexture(AoPath, false);
        ConfigureModel();

        BuildMetallicSmoothness();
        ConfigureDataTexture(MetallicSmoothnessPath, false);

        var material = BuildMaterial();
        BuildPrefab(material);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Debug.Log("VOLCANO_R19_IMPORT_COMPLETE: " + PrefabPath);
    }

    private static void ConfigureColorTexture(string path, bool srgb)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = srgb;
        importer.alphaSource = TextureImporterAlphaSource.None;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Trilinear;
        importer.anisoLevel = 8;
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
    }

    private static void ConfigureNormalTexture(string path)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;
        importer.textureType = TextureImporterType.NormalMap;
        importer.sRGBTexture = false;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Trilinear;
        importer.anisoLevel = 8;
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
    }

    private static void ConfigureDataTexture(string path, bool readable)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = false;
        importer.isReadable = readable;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Trilinear;
        importer.anisoLevel = 8;
        importer.maxTextureSize = 2048;
        importer.textureCompression = readable
            ? TextureImporterCompression.Uncompressed
            : TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
    }

    private static void ConfigureModel()
    {
        AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        if (importer == null) return;
        importer.globalScale = 1f;
        importer.useFileScale = true;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.importBlendShapes = false;
        importer.importAnimation = false;
        importer.importNormals = ModelImporterNormals.Import;
        importer.importTangents = ModelImporterTangents.CalculateMikk;
        importer.meshCompression = ModelImporterMeshCompression.Off;
        importer.isReadable = false;
        importer.SaveAndReimport();
    }

    private static void BuildMetallicSmoothness()
    {
        var roughness = AssetDatabase.LoadAssetAtPath<Texture2D>(RoughnessPath);
        if (roughness == null) throw new FileNotFoundException("Missing roughness texture", RoughnessPath);

        var source = roughness.GetPixels32();
        var packed = new Color32[source.Length];
        for (var i = 0; i < source.Length; i++)
        {
            var r = source[i].r;
            packed[i] = new Color32(0, 0, 0, (byte)(255 - r));
        }

        var texture = new Texture2D(roughness.width, roughness.height, TextureFormat.RGBA32, false, true);
        texture.SetPixels32(packed);
        texture.Apply(false, false);
        var bytes = texture.EncodeToPNG();
        Object.DestroyImmediate(texture);

        var fullPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, MetallicSmoothnessPath);
        File.WriteAllBytes(fullPath, bytes);
        AssetDatabase.ImportAsset(MetallicSmoothnessPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
    }

    private static Material BuildMaterial()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new System.InvalidOperationException("URP Lit shader was not found.");
            material = new Material(shader) { name = "Volcano_Hero_R19_URP" };
            AssetDatabase.CreateAsset(material, MaterialPath);
        }

        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(AlbedoPath));
        material.SetColor("_BaseColor", Color.white);
        material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath));
        material.SetFloat("_BumpScale", 1f);
        material.EnableKeyword("_NORMALMAP");
        material.SetTexture("_OcclusionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(AoPath));
        material.SetFloat("_OcclusionStrength", 1f);
        material.EnableKeyword("_OCCLUSIONMAP");
        material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(MetallicSmoothnessPath));
        material.SetFloat("_Metallic", 0f);
        material.SetFloat("_Smoothness", 1f);
        material.EnableKeyword("_METALLICSPECGLOSSMAP");
        material.SetFloat("_Surface", 0f);
        material.SetFloat("_Cull", 2f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void BuildPrefab(Material material)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (model == null) throw new FileNotFoundException("Missing imported volcano FBX", ModelPath);

        var instance = PrefabUtility.InstantiatePrefab(model) as GameObject;
        if (instance == null) throw new System.InvalidOperationException("Could not instantiate volcano FBX.");
        instance.name = "Volcano_Hero_R19";
        instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        instance.transform.localScale = Vector3.one;

        foreach (var renderer in instance.GetComponentsInChildren<MeshRenderer>(true))
            renderer.sharedMaterial = material;

        foreach (var filter in instance.GetComponentsInChildren<MeshFilter>(true))
        {
            var collider = filter.GetComponent<MeshCollider>();
            if (collider == null) collider = filter.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = filter.sharedMesh;
        }

        PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
        Object.DestroyImmediate(instance);
    }
}
