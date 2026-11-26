using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// ゾンビ図鑑の敵の絵を書き出す。各項目の代表のプレハブをシーンの外に置いて撮り、
/// Assets/Resources/Bestiary/bestiary_&lt;id&gt;.png（スプライト、ポイント）として保存する。
/// プレハブの見た目を変えたら、もう一度実行する。
/// </summary>
public static class BestiaryPortraitExport
{
    const string Folder = "Assets/Resources/Bestiary";
    const int Size = 128;
    // 撮影用に、ほかの物から離れた場所に置く。
    static readonly Vector3 StagePoint = new Vector3(5000f, 5000f, 0f);

    // 雑魚の、図鑑の id ごとの代表のプレハブ。
    static readonly Dictionary<int, string> Sources = new()
    {
        { 1, "Assets/Prefab/Block/pf_Block_Square.prefab" },
        { 2, "Assets/Prefab/Block/pf_Block_Armor.prefab" },
        { 3, "Assets/Prefab/Block/pf_Block_Regen.prefab" },
        { 4, "Assets/Prefab/Block/pf_Block_Bloat.prefab" },
        { 5, "Assets/Prefab/Block/pf_Block_Split.prefab" },
        { 6, "Assets/Prefab/Block/pf_Block_Ghost.prefab" },
        { 7, "Assets/Prefab/Block/pf_Block_Glass.prefab" },
        { 8, "Assets/Prefab/Block/pf_Block_Chest.prefab" },
        { 9, "Assets/Prefab/Block/pf_Block_Solid.prefab" },
    };

    // ボスは形ごとに 1 項目（id は 101 ＋ bossIndex）。プレハブの bossIndex から id を決めて、すべてのボスを撮る。
    // 図鑑（SO_BestiaryData）に無い id は撮らない（段に出ていないボス）。
    const string BossFolder = "Assets/Prefab/Boss";

    static Dictionary<int, string> CollectSources()
    {
        var sources = new Dictionary<int, string>(Sources);
        var table = SOLoader.BestiaryData;
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { BossFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var piece = prefab != null ? prefab.GetComponent<BlockPiece>() : null;
            if (piece == null || piece.BossIndex < 0) continue;
            int id = BlockPiece.BossBestiaryId(piece.BossIndex);
            if (table != null && table.Find(id) == null) continue;
            sources[id] = path;
        }
        return sources;
    }

    [MenuItem("Tools/ゾンビ図鑑/敵の絵を書き出す")]
    public static void Export()
    {
        if (!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets/Resources", "Bestiary");

        var sources = CollectSources();
        int written = 0;
        foreach (var pair in sources)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(pair.Value);
            if (prefab == null)
            {
                Debug.LogWarning($"図鑑の絵：プレハブがありません（{pair.Value}）");
                continue;
            }
            string path = $"{Folder}/bestiary_{pair.Key}.png";
            if (Capture(prefab, path))
                written++;
        }
        AssetDatabase.Refresh();
        foreach (var pair in sources)
            ImportAsSprite($"{Folder}/bestiary_{pair.Key}.png");
        Debug.Log($"ゾンビ図鑑の絵を {written} 枚書き出しました（{Folder}）");
    }

    static bool Capture(GameObject prefab, string path)
    {
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        var cameraObject = new GameObject("BestiaryPortraitCamera");
        var texture = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32);
        try
        {
            instance.transform.position = StagePoint;
            var renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return false;
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers)
                bounds.Encapsulate(renderer.bounds);

            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            camera.transform.position = new Vector3(bounds.center.x, bounds.center.y, bounds.center.z - 10f);
            camera.orthographicSize = Mathf.Max(bounds.extents.x, bounds.extents.y) * 1.12f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 50f;
            camera.targetTexture = texture;
            camera.Render();

            var previous = RenderTexture.active;
            RenderTexture.active = texture;
            var image = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(path, image.EncodeToPNG());
            Object.DestroyImmediate(image);
            camera.targetTexture = null;
            return true;
        }
        finally
        {
            Object.DestroyImmediate(instance);
            Object.DestroyImmediate(cameraObject);
            texture.Release();
            Object.DestroyImmediate(texture);
        }
    }

    static void ImportAsSprite(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
    }
}
