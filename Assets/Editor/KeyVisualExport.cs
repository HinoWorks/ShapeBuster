using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// ストアのキービジュアル（カプセル画像など）用に、ゾンビ・ボス・ボールを背景を透かした大きな PNG で書き出す。
/// 書き出し先はプロジェクト直下の StoreArt/KeyVisual（Assets の外なので、Unity には取り込まれない）。
/// ゾンビの顔は個体の種（_Seed）で変わるので、種を変えた絵を何枚か書き出して、使う顔を選べるようにする。
/// </summary>
public static class KeyVisualExport
{
    const string OutFolder = "StoreArt/KeyVisual";

    /// <summary>1 マスのピクセル数。ドットの細かさ（6・8・9・12 など）で割り切れる数にして、ドットの幅をそろえる。</summary>
    const int PixelsPerCell = 360;

    /// <summary>ボールの書き出しの一辺。</summary>
    const int BallSize = 1024;

    /// <summary>まわりに足す透明の余白（大きさに対する割合）。</summary>
    const float Padding = 0.06f;

    static readonly Vector3 StagePoint = new Vector3(6000f, 6000f, 0f);

    /// <summary>雑魚とブロック。数は、顔の種を変えて書き出す枚数。</summary>
    static readonly (string name, string path, int faces)[] Blocks =
    {
        ("zombie_normal", "Assets/Prefab/Block/pf_Block_Square.prefab", 8),
        ("zombie_tall", "Assets/Prefab/Block/pf_Block_Tall.prefab", 3),
        ("zombie_wide", "Assets/Prefab/Block/pf_Block_Wide.prefab", 3),
        ("zombie_armor", "Assets/Prefab/Block/pf_Block_Armor.prefab", 3),
        ("zombie_regen", "Assets/Prefab/Block/pf_Block_Regen.prefab", 3),
        ("zombie_bloat", "Assets/Prefab/Block/pf_Block_Bloat.prefab", 3),
        ("zombie_split", "Assets/Prefab/Block/pf_Block_Split.prefab", 3),
        ("zombie_ghost", "Assets/Prefab/Block/pf_Block_Ghost.prefab", 3),
        ("glass", "Assets/Prefab/Block/pf_Block_Glass.prefab", 1),
        ("chest", "Assets/Prefab/Block/pf_Block_Chest.prefab", 1),
        ("stone", "Assets/Prefab/Block/pf_Block_Solid.prefab", 1),
    };

    const string BossFolder = "Assets/Prefab/Boss";
    const string BallFolder = "Assets/Prefab/Ball";

    static readonly int SeedId = Shader.PropertyToID("_Seed");

    [MenuItem("Tools/ストア素材/キービジュアルの素材を書き出す")]
    public static void Export()
    {
        Directory.CreateDirectory(OutFolder);
        Directory.CreateDirectory($"{OutFolder}/zombies");
        Directory.CreateDirectory($"{OutFolder}/bosses");
        Directory.CreateDirectory($"{OutFolder}/balls");

        int written = 0;
        foreach (var block in Blocks)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(block.path);
            if (prefab == null)
            {
                Debug.LogWarning($"キービジュアル：プレハブがありません（{block.path}）");
                continue;
            }
            for (int i = 0; i < block.faces; i++)
            {
                string suffix = block.faces > 1 ? $"_{i + 1:00}" : "";
                if (CaptureBlock(prefab, FaceSeed(block.name, i), $"{OutFolder}/zombies/{block.name}{suffix}.png"))
                    written++;
            }
        }

        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { BossFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null || prefab.GetComponent<BlockPiece>() == null) continue;
            string name = Path.GetFileNameWithoutExtension(path).Replace("pf_Block_", "").ToLowerInvariant();
            for (int i = 0; i < 2; i++)
            {
                if (CaptureBlock(prefab, FaceSeed(name, i), $"{OutFolder}/bosses/{name}_{i + 1:00}.png"))
                    written++;
            }
        }

        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { BallFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;
            string name = Path.GetFileNameWithoutExtension(path).Replace("pf_Ball_", "ball_").ToLowerInvariant();
            if (CaptureFit(prefab, $"{OutFolder}/balls/{name}.png"))
                written++;
        }

        Debug.Log($"キービジュアルの素材を {written} 枚書き出しました（{Path.GetFullPath(OutFolder)}）");
        EditorUtility.RevealInFinder(Path.GetFullPath(OutFolder));
    }

    /// <summary>名前と番号から、毎回同じになる顔の種を作る。</summary>
    static float FaceSeed(string name, int index)
    {
        return Mathf.Repeat((name.GetHashCode() & 0xffff) * 0.000123f + index * 0.6180339f, 1f);
    }

    /// <summary>ブロックを、1 マス PixelsPerCell ピクセルで撮る。</summary>
    static bool CaptureBlock(GameObject prefab, float seed, string path)
    {
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        try
        {
            instance.transform.position = StagePoint;
            var piece = instance.GetComponent<BlockPiece>();
            var root = instance.GetComponent<SpriteRenderer>();
            if (root != null)
            {
                var block = new MaterialPropertyBlock();
                root.GetPropertyBlock(block);
                block.SetFloat(SeedId, seed);
                root.SetPropertyBlock(block);
            }
            if (!TryBounds(instance, out var bounds)) return false;

            float cellsX = piece != null ? piece.CellsX : 1f;
            float cellsY = piece != null ? piece.CellsY : 1f;
            // ブロックの本体の大きさ（マス数）からピクセル数を決める。殻などがはみ出す分は、同じ比率で広げる。
            float pixelsPerUnit = PixelsPerCell / Mathf.Max(0.0001f, instance.transform.lossyScale.x / cellsX);
            return Render(bounds, pixelsPerUnit, path);
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    /// <summary>ボールなど、長い辺が BallSize ピクセルになるように撮る。</summary>
    static bool CaptureFit(GameObject prefab, string path)
    {
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        try
        {
            instance.transform.position = StagePoint;
            if (!TryBounds(instance, out var bounds)) return false;
            float longest = Mathf.Max(bounds.size.x, bounds.size.y) * (1f + Padding * 2f);
            return Render(bounds, BallSize / Mathf.Max(0.0001f, longest), path);
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    static bool TryBounds(GameObject instance, out Bounds bounds)
    {
        bounds = default;
        bool found = false;
        foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
        {
            if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
            if (!found) { bounds = renderer.bounds; found = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        return found;
    }

    static bool Render(Bounds bounds, float pixelsPerUnit, string path)
    {
        float width = bounds.size.x * (1f + Padding * 2f);
        float height = bounds.size.y * (1f + Padding * 2f);
        int pixelsX = Mathf.Clamp(Mathf.RoundToInt(width * pixelsPerUnit), 8, 8192);
        int pixelsY = Mathf.Clamp(Mathf.RoundToInt(height * pixelsPerUnit), 8, 8192);

        var cameraObject = new GameObject("KeyVisualCamera");
        var texture = new RenderTexture(pixelsX, pixelsY, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
        try
        {
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            camera.transform.position = new Vector3(bounds.center.x, bounds.center.y, bounds.center.z - 10f);
            camera.orthographicSize = pixelsY / pixelsPerUnit * 0.5f;
            camera.aspect = (float)pixelsX / pixelsY;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 50f;
            camera.targetTexture = texture;
            camera.Render();

            var previous = RenderTexture.active;
            RenderTexture.active = texture;
            var image = new Texture2D(pixelsX, pixelsY, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, pixelsX, pixelsY), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(path, image.EncodeToPNG());
            Object.DestroyImmediate(image);
            camera.targetTexture = null;
            return true;
        }
        finally
        {
            Object.DestroyImmediate(cameraObject);
            texture.Release();
            Object.DestroyImmediate(texture);
        }
    }
}
