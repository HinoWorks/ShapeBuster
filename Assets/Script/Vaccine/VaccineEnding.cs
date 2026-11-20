using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Random = UnityEngine.Random;

/// <summary>
/// ワクチンを撒いたあとの流れ。エンドロールはない。
/// 1. 画面の中央から緑のしずくが四方へ弾け、画面がうっすら緑に光る（撒く）。
/// 2. ゆっくり暗転する。
/// 3. 真っ黒な画面に、そのあとの日記を 1 行ずつ出す（vaccine.epilogue1〜）。クリックかキーで飛ばせる。
/// 4. 暗いうちにタイトルへ切り替え、明るく戻す。
/// クリアは最初に記録する（途中で閉じても残る）。演出の間は画面の操作を止める。
/// </summary>
public static class VaccineEnding
{
    // 画面の幕（TransitionCurtain は 32000）より下、ほかの UI より上。
    const int SortingOrder = 31000;
    const int DropCount = 140;
    const float DropTimeMin = 0.7f;
    const float DropTimeMax = 1.4f;
    const float FlashPeak = 0.22f;
    // 撒いてから暗転が始まるまで。
    const float SpreadDuration = 1.6f;
    const float FadeOutDuration = 1.2f;
    const float BlackHold = 0.5f;
    const float FadeInDuration = 0.8f;
    // 暗転の文章。1 行が出てから次の行までと、全部出たあとに待つ時間。
    const int EpilogueLines = 5;
    const float LineFade = 0.8f;
    const float LineInterval = 1.9f;
    const float EpilogueHold = 2.5f;
    const float EpilogueOut = 0.8f;
    const float EpilogueFontSize = 40f;

    static readonly Color[] DropColors =
    {
        new Color(0.45f, 0.95f, 0.55f, 1f),
        new Color(0.65f, 1f, 0.7f, 1f),
        new Color(0.3f, 0.8f, 0.45f, 1f),
        new Color(0.85f, 1f, 0.85f, 1f),
    };

    static Sprite dropSprite;

    public static bool Playing { get; private set; }

    /// <summary>
    /// 撒く演出を始める。始められたら true。
    /// 撒けるのは一度だけ。画面の切り替え中やアウトゲーム以外では始めない（クリアも記録しない）。
    /// </summary>
    public static bool Play()
    {
        if (!CanPlay()) return false;
        PlayAsync().Forget();
        return true;
    }

    public static bool CanPlay()
    {
        if (Playing || !VaccineRuntime.IsFull || VaccineRuntime.Cleared) return false;
        var watcher = GameWatcher.Inst;
        if (watcher == null) return false;
        if (watcher.IsChangingScreen || watcher.currentGameState != GameStateType.OutGame) return false;
        return true;
    }

    static async UniTaskVoid PlayAsync()
    {
        Playing = true;
        VaccineRuntime.MarkCleared();

        var root = BuildCanvas(out var flash, out var black, out var dropLayer);
        var token = root.GetCancellationTokenOnDestroy();
        try
        {
            SoundManager.SE(SEType.UI_PurchaseMax);
            SoundManager.SE(SEType.Transition, 0.8f, 0.7f);
            if (CameraManager.Inst != null)
                CameraManager.Inst.Shake(CameraShakeLevel.Medium);

            SpawnDrops(dropLayer);
            flash.DOFade(FlashPeak, 0.25f).SetUpdate(true)
                .OnComplete(() => flash.DOFade(0f, 1.2f).SetUpdate(true));

            await Wait(SpreadDuration, token);

            if (SoundManager.Inst != null)
                SoundManager.Inst.SetCurtainMuffle(true, FadeOutDuration);
            black.DOFade(1f, FadeOutDuration).SetEase(Ease.InQuad).SetUpdate(true);
            await Wait(FadeOutDuration, token);
            black.color = Color.black;
            dropLayer.gameObject.SetActive(false);
            await Wait(BlackHold, token);

            await PlayEpilogue(root.transform, token);

            if (GameWatcher.Inst != null)
                GameWatcher.Inst.SetGameState(GameStateType.Title);
            await UniTask.DelayFrame(2, cancellationToken: token);

            if (SoundManager.Inst != null)
                SoundManager.Inst.SetCurtainMuffle(false, FadeInDuration);
            black.DOFade(0f, FadeInDuration).SetEase(Ease.OutQuad).SetUpdate(true);
            await Wait(FadeInDuration, token);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            Playing = false;
            if (root != null)
            {
                DOTween.Kill(root.transform, true);
                foreach (var graphic in root.GetComponentsInChildren<Graphic>(true))
                {
                    graphic.DOKill();
                    graphic.rectTransform.DOKill();
                }
                UnityEngine.Object.Destroy(root);
            }
        }
    }

    /// <summary>真っ黒な画面に、そのあとの日記を 1 行ずつ出す。押すと残りの行をまとめて出し、もう一度押すと先へ進む。</summary>
    static async UniTask PlayEpilogue(Transform root, CancellationToken token)
    {
        var box = new GameObject("epilogue", typeof(RectTransform), typeof(VerticalLayoutGroup));
        box.transform.SetParent(root, false);
        var rect = (RectTransform)box.transform;
        rect.anchorMin = new Vector2(0.15f, 0.2f);
        rect.anchorMax = new Vector2(0.85f, 0.8f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        var layout = box.GetComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.spacing = 26f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var lines = new System.Collections.Generic.List<TextMeshProUGUI>();
        for (int i = 1; i <= EpilogueLines; i++)
        {
            string text = Loc.Content("vaccine.epilogue" + i, "");
            if (string.IsNullOrEmpty(text)) continue;
            var go = new GameObject("line" + i, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(box.transform, false);
            var label = go.GetComponent<TextMeshProUGUI>();
            Loc.UseFont(label);
            label.fontSize = EpilogueFontSize;
            label.alignment = TextAlignmentOptions.Left;
            label.color = new Color(1f, 1f, 1f, 0f);
            label.raycastTarget = false;
            label.SetText(text);
            lines.Add(label);
        }

        bool skipped = false;
        foreach (var label in lines)
        {
            if (skipped) break;
            label.DOFade(1f, LineFade).SetUpdate(true);
            skipped = await WaitOrSkip(LineInterval, token);
        }
        if (skipped)
        {
            // 押されたら、残りの行をまとめて出す。
            foreach (var label in lines)
            {
                label.DOKill();
                label.DOFade(1f, 0.25f).SetUpdate(true);
            }
            await Wait(0.3f, token);
        }
        await WaitOrSkip(EpilogueHold, token);

        foreach (var label in lines)
        {
            label.DOKill();
            label.DOFade(0f, EpilogueOut).SetUpdate(true);
        }
        await Wait(EpilogueOut, token);
    }

    /// <summary>決めた秒数だけ待つ。途中でクリックかキーが押されたら、そこで終えて true を返す。</summary>
    static async UniTask<bool> WaitOrSkip(float seconds, CancellationToken token)
    {
        float end = Time.unscaledTime + seconds;
        // 押したままの入力で飛ばないように、1 フレームおいてから見る。
        await UniTask.Yield(PlayerLoopTiming.Update, token);
        while (Time.unscaledTime < end)
        {
            if (SkipPressed()) return true;
            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }
        return false;
    }

    static bool SkipPressed()
    {
        var mouse = Mouse.current;
        if (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame)) return true;
        var keyboard = Keyboard.current;
        return keyboard != null && keyboard.anyKey.wasPressedThisFrame;
    }

    static UniTask Wait(float seconds, CancellationToken token)
    {
        return UniTask.Delay(TimeSpan.FromSeconds(seconds), DelayType.UnscaledDeltaTime, cancellationToken: token);
    }

    static GameObject BuildCanvas(out Image flash, out Image black, out RectTransform dropLayer)
    {
        var root = new GameObject("VaccineEnding");
        UnityEngine.Object.DontDestroyOnLoad(root);
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        root.AddComponent<GraphicRaycaster>();

        // 演出の間は下の画面を押せないようにする（透明な板で受ける）。
        var blocker = FullScreen("blocker", root.transform);
        blocker.color = new Color(0f, 0f, 0f, 0f);
        blocker.raycastTarget = true;

        flash = FullScreen("flash", root.transform);
        flash.color = new Color(0.55f, 1f, 0.6f, 0f);

        var layer = new GameObject("drops", typeof(RectTransform));
        layer.transform.SetParent(root.transform, false);
        dropLayer = (RectTransform)layer.transform;
        dropLayer.anchorMin = Vector2.zero;
        dropLayer.anchorMax = Vector2.one;
        dropLayer.offsetMin = Vector2.zero;
        dropLayer.offsetMax = Vector2.zero;

        black = FullScreen("black", root.transform);
        black.color = new Color(0f, 0f, 0f, 0f);
        return root;
    }

    static Image FullScreen(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        var image = go.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    /// <summary>中央から、緑のしずくを四方へ弾けさせる。外へ行くほど大きく、薄くなって消える。</summary>
    static void SpawnDrops(RectTransform layer)
    {
        var sprite = DropSprite();
        float reach = 1300f;
        for (int i = 0; i < DropCount; i++)
        {
            var go = new GameObject("drop", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(layer, false);
            var rect = (RectTransform)go.transform;
            float size = Random.Range(10f, 34f);
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = Random.insideUnitCircle * 30f;
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            image.color = DropColors[Random.Range(0, DropColors.Length)];

            var dir = Random.insideUnitCircle.normalized;
            var target = dir * Random.Range(0.35f, 1f) * reach;
            float time = Random.Range(DropTimeMin, DropTimeMax);
            float delay = Random.Range(0f, 0.25f);
            rect.localScale = Vector3.one * 0.3f;
            rect.DOAnchorPos(target, time).SetDelay(delay).SetEase(Ease.OutCubic).SetUpdate(true);
            rect.DOScale(Random.Range(1f, 1.8f), time).SetDelay(delay).SetEase(Ease.OutQuad).SetUpdate(true);
            image.DOFade(0f, time * 0.6f).SetDelay(delay + time * 0.5f).SetUpdate(true);
        }
    }

    /// <summary>丸いしずくの絵。画像を持たないので、起動中に一度だけ作る。</summary>
    static Sprite DropSprite()
    {
        if (dropSprite != null) return dropSprite;
        const int size = 32;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        float r = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                float a = Mathf.Clamp01(r - d);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        texture.Apply();
        dropSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return dropSprite;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay()
    {
        Playing = false;
        dropSprite = null;
    }
}
