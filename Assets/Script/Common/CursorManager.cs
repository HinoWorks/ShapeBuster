using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// カスタムカーソルをゲーム内UIとして描画する。
/// OSカーソルは Unity Recorder に写らないため、ソフトウェアカーソルを使う。
/// </summary>
public class CursorManager : MonoBehaviour
{
    public static CursorManager Inst;

    const string DefaultResourcePath = "Cursor/GameCursor";

    [SerializeField] Texture2D cursorTexture;
    [SerializeField] Vector2 hotspot = Vector2.zero;
    [SerializeField] string resourcePath = DefaultResourcePath;

    RectTransform canvasRect;
    RectTransform cursorRect;
    Image cursorImage;
    Sprite cursorSprite;
    Texture2D hiddenCursorTexture;
    bool isSoftwareCursorActive;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureInstance()
    {
        if (Inst != null) return;
        if (FindFirstObjectByType<CursorManager>() != null) return;
        var go = new GameObject("===CursorManager");
        go.AddComponent<CursorManager>();
    }

    void Awake()
    {
        if (Inst == null) { Inst = this; }
        else { Destroy(this); return; }

        ApplyCustomCursor();
    }

    void LateUpdate()
    {
        if (!isSoftwareCursorActive) return;

        HideHardwareCursor();
        UpdateCursorPosition();
    }

    void OnDestroy()
    {
        if (cursorSprite != null) Destroy(cursorSprite);
        if (hiddenCursorTexture != null) Destroy(hiddenCursorTexture);
        if (Inst != this) return;

        Inst = null;
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        Cursor.visible = true;
    }

    public void ApplyCustomCursor()
    {
        var texture = ResolveTexture();
        if (texture == null) return;

        EnsureCursorView(texture);
        if (cursorImage != null) cursorImage.enabled = true;

        isSoftwareCursorActive = true;
        HideHardwareCursor();
        UpdateCursorPosition();
    }

    public void ResetCursor()
    {
        isSoftwareCursorActive = false;
        if (cursorImage != null) cursorImage.enabled = false;
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        Cursor.visible = true;
    }

    void EnsureCursorView(Texture2D texture)
    {
        if (cursorRect == null)
        {
            var canvasGO = new GameObject("CursorCanvas");
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;
            canvasRect = canvasGO.GetComponent<RectTransform>();

            var imageGO = new GameObject("CursorImage");
            imageGO.transform.SetParent(canvasGO.transform, false);
            cursorImage = imageGO.AddComponent<Image>();
            cursorImage.raycastTarget = false;
            cursorRect = cursorImage.rectTransform;
            cursorRect.anchorMin = new Vector2(0.5f, 0.5f);
            cursorRect.anchorMax = new Vector2(0.5f, 0.5f);
        }

        ApplySprite(texture);
    }

    void ApplySprite(Texture2D texture)
    {
        if (cursorSprite != null) Destroy(cursorSprite);

        var pivot = new Vector2(
            texture.width > 0 ? hotspot.x / texture.width : 0f,
            texture.height > 0 ? 1f - hotspot.y / texture.height : 1f);

        cursorSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            pivot,
            100f,
            0,
            SpriteMeshType.FullRect);

        cursorImage.sprite = cursorSprite;
        cursorImage.SetNativeSize();
        cursorRect.pivot = pivot;
    }

    void HideHardwareCursor()
    {
        Cursor.SetCursor(GetHiddenCursorTexture(), Vector2.zero, CursorMode.ForceSoftware);
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    Texture2D GetHiddenCursorTexture()
    {
        if (hiddenCursorTexture != null) return hiddenCursorTexture;

        const int size = 32;
        hiddenCursorTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        hiddenCursorTexture.name = "HiddenCursor";
        hiddenCursorTexture.filterMode = FilterMode.Point;
        hiddenCursorTexture.wrapMode = TextureWrapMode.Clamp;
        hiddenCursorTexture.hideFlags = HideFlags.HideAndDontSave;

        var pixels = new Color[size * size];
        pixels[0] = new Color(1f, 1f, 1f, 1f / 255f);
        hiddenCursorTexture.SetPixels(pixels);
        hiddenCursorTexture.Apply();
        return hiddenCursorTexture;
    }

    void UpdateCursorPosition()
    {
        if (cursorRect == null || canvasRect == null) return;
        if (Mouse.current == null) return;

        var screenPos = Mouse.current.position.ReadValue();
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, screenPos, null, out var localPoint);
        cursorRect.localPosition = localPoint;
    }

    Texture2D ResolveTexture()
    {
        if (cursorTexture != null) return cursorTexture;
        if (string.IsNullOrEmpty(resourcePath)) return null;

        var loaded = Resources.Load<Texture2D>(resourcePath);
        if (loaded == null)
        {
            Debug.LogWarning($"CursorManager: cursor texture not found. Place a Texture2D at Resources/{resourcePath}");
        }
        return loaded;
    }
}
