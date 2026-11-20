using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 画面切り替えの幕。インゲームとアウトゲームを行き来するときに画面を覆い、裏で場を作り直すのを隠す。
/// シーンに置かなくても、起動時に自動で作られる。呼び出しは GameWatcher.ChangeScreen から行う。
///
/// 見た目は Resources/Transition/mat_TransitionWipe（シェーダー ShapeBuster/UITransitionWipe）。
/// 閉じるたびに、ワイプの形（5 種類）と左右反転をランダムに選ぶ。開くときは同じ形を逆に戻す。
/// 時間は unscaled で数えるので、Time.timeScale が 0 でも動く。
/// </summary>
public class TransitionCurtain : MonoBehaviour
{
    public static TransitionCurtain Inst { get; private set; }

    public const int PatternCount = 5;
    const string MaterialPath = "Transition/mat_TransitionWipe";
    const string ShaderName = "ShapeBuster/UITransitionWipe";
    const int SortingOrder = 32000;
    // 閉じきってから、真ん中のゾンビを出し始めるまでの時間と、出しきるまでの時間。
    // 幕を閉じている間（GameWatcher の MinCurtainHold）は毎回見せる演出なので、すぐ出す。
    const float LoadingDelay = 0.03f;
    const float LoadingFade = 0.1f;

    static readonly int ProgressId = Shader.PropertyToID("_Progress");
    static readonly int PatternId = Shader.PropertyToID("_Pattern");
    static readonly int MirrorId = Shader.PropertyToID("_Mirror");
    static readonly int SeedId = Shader.PropertyToID("_Seed");
    static readonly int AspectId = Shader.PropertyToID("_Aspect");
    static readonly int LoadingId = Shader.PropertyToID("_Loading");
    static readonly int LoadingTimeId = Shader.PropertyToID("_LoadingTime");

    [Tooltip("閉じるのにかける時間（秒）")]
    [SerializeField] float closeDuration = 0.5f;
    [Tooltip("開くのにかける時間（秒）")]
    [SerializeField] float openDuration = 0.5f;

    RawImage image;
    Material material;
    float progress;
    float closedTime;
    bool fullyClosed;
    float loading;

    /// <summary>幕が少しでも画面を覆っている。</summary>
    public bool IsCovering => progress > 0f;
    /// <summary>閉じる・開くの途中。</summary>
    public bool IsMoving { get; private set; }
    /// <summary>今の（直前の）ワイプの形。0〜PatternCount-1。</summary>
    public int Pattern { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void AutoCreate()
    {
        Ensure();
    }

    public static TransitionCurtain Ensure()
    {
        if (Inst != null) return Inst;
        var found = FindFirstObjectByType<TransitionCurtain>();
        if (found != null) return found;
        var go = new GameObject("===TransitionCurtain");
        return go.AddComponent<TransitionCurtain>();
    }

    void Awake()
    {
        if (Inst != null && Inst != this)
        {
            Destroy(gameObject);
            return;
        }
        Inst = this;
        DontDestroyOnLoad(gameObject);
        Build();
        SetProgress(0f);
    }

    void OnDestroy()
    {
        if (Inst == this) Inst = null;
        if (material != null) Destroy(material);
    }

    void Build()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        // 覆っている間は、下の UI を押せないようにする。
        gameObject.AddComponent<GraphicRaycaster>();

        var child = new GameObject("Wipe", typeof(RectTransform));
        child.transform.SetParent(transform, false);
        var rect = (RectTransform)child.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        image = child.AddComponent<RawImage>();
        image.raycastTarget = true;
        var source = Resources.Load<Material>(MaterialPath);
        if (source != null)
            material = new Material(source);
        else
        {
            var shader = Shader.Find(ShaderName);
            if (shader == null)
                Debug.LogWarning($"TransitionCurtain：Resources/{MaterialPath} もシェーダー {ShaderName} も見つかりません。黒い幕で代用します。");
            else
                material = new Material(shader);
        }
        if (material != null)
            image.material = material;
        else
            image.color = Color.black;
    }

    void Update()
    {
        if (!IsCovering) return;

        if (fullyClosed)
        {
            closedTime += Time.unscaledDeltaTime;
            loading = Mathf.Clamp01((closedTime - LoadingDelay) / LoadingFade);
        }
        else
        {
            // 開き始めても跳ねる時間は進め、空中で止まったまま消えないようにする。
            if (loading > 0f)
                closedTime += Time.unscaledDeltaTime;
            loading = Mathf.MoveTowards(loading, 0f, Time.unscaledDeltaTime / LoadingFade);
        }

        if (material == null) return;
        material.SetFloat(AspectId, Screen.width / Mathf.Max(1f, Screen.height));
        material.SetFloat(LoadingId, loading);
        material.SetFloat(LoadingTimeId, Mathf.Max(0f, closedTime - LoadingDelay));
    }

    void SetProgress(float value)
    {
        progress = Mathf.Clamp01(value);
        image.enabled = progress > 0f;
        if (material != null)
        {
            material.SetFloat(ProgressId, progress);
            material.SetFloat(AspectId, Screen.width / Mathf.Max(1f, Screen.height));
        }
        else
            image.color = new Color(0f, 0f, 0f, progress);
    }

    /// <summary>ワイプの形をランダムに選んで、画面を覆う。閉じきったら戻る。</summary>
    public async UniTask Close(CancellationToken token = default)
    {
        Pattern = Random.Range(0, PatternCount);
        if (material != null)
        {
            material.SetFloat(PatternId, Pattern);
            material.SetFloat(MirrorId, Random.value < 0.5f ? 1f : 0f);
            material.SetFloat(SeedId, Random.value * 10f);
        }
        fullyClosed = false;
        closedTime = 0f;
        SoundManager.SE(SEType.Transition);
        if (SoundManager.Inst != null)
            SoundManager.Inst.SetCurtainMuffle(true, closeDuration);

        await Animate(1f, closeDuration, token);
        fullyClosed = true;
    }

    /// <summary>閉じたときと同じ形を逆にたどって、画面を開く。開ききったら戻る。</summary>
    public async UniTask Open(CancellationToken token = default)
    {
        fullyClosed = false;
        if (SoundManager.Inst != null)
            SoundManager.Inst.SetCurtainMuffle(false, openDuration);
        await Animate(0f, openDuration, token);
    }

    async UniTask Animate(float target, float duration, CancellationToken token)
    {
        IsMoving = true;
        try
        {
            float from = progress;
            float time = 0f;
            duration = Mathf.Max(0.01f, duration);
            while (time < duration)
            {
                time += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(time / duration);
                // 始めと終わりをゆっくりにする。
                t = t * t * (3f - 2f * t);
                SetProgress(Mathf.Lerp(from, target, t));
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
            SetProgress(target);
        }
        finally
        {
            IsMoving = false;
        }
    }
}
