using DG.Tweening;
using UnityEngine;

/// <summary>
/// ボタン（HButton）を押したときと同じ「ぽん」と弾む動き。
/// 一瞬縮め → 大きく広げて傾ける → 元の大きさ・傾き 0 に戻す。
/// 数字が変わったところ（スキルのレベルや値段など）に付けて、変わったことを目立たせる。
/// </summary>
public static class UIPopAnim
{
    public const float Squash = 0.9f;     // 一瞬縮める大きさ
    public const float TimeSquash = 0.05f; // 縮めておく秒数
    public const float Scale = 1.15f;     // 広げる大きさ
    public const float Tilt = -15f;       // 広げたときの傾き（度）
    public const float TimeGrow = 0.1f;   // 広げるまでの秒数
    public const float TimeBack = 0.25f;  // 元に戻るまでの秒数

    /// <summary>
    /// target を弾ませる。元の大きさは baseScale、傾きは 0 に戻す。
    /// 大きさと傾きは別の Sequence で返す（呼び出し側で片方だけ止められるように）。
    /// </summary>
    public static void Play(Transform target, out Sequence scale, out Sequence tilt,
        float baseScale = 1f, float delay = 0f, float tiltAngle = Tilt, float scaleUp = Scale)
    {
        scale = null;
        tilt = null;
        if (target == null) return;

        // 遅らせるときは、待っている間は元の見た目のまま。
        if (delay <= 0f)
        {
            target.localScale = Squash * baseScale * Vector3.one;
            target.localRotation = Quaternion.identity;
        }

        scale = DOTween.Sequence().SetUpdate(true).SetTarget(target)
            .AppendInterval(delay)
            .AppendCallback(() =>
            {
                target.localScale = Squash * baseScale * Vector3.one;
                target.localRotation = Quaternion.identity;
            })
            .AppendInterval(TimeSquash)
            .Append(target.DOScale(scaleUp * baseScale * Vector3.one, TimeGrow).SetEase(Ease.OutQuad))
            .Append(target.DOScale(baseScale * Vector3.one, TimeBack).SetEase(Ease.OutQuad));
        tilt = DOTween.Sequence().SetUpdate(true).SetTarget(target)
            .AppendInterval(delay + TimeSquash)
            .Append(target.DOLocalRotate(new Vector3(0f, 0f, tiltAngle), TimeGrow).SetEase(Ease.OutQuad))
            .Append(target.DOLocalRotate(Vector3.zero, TimeBack).SetEase(Ease.OutQuad));
    }

    /// <summary>止める必要のない場所向け。前の動きを止めてから弾ませる。</summary>
    public static void Play(Transform target, float delay = 0f, float tiltAngle = Tilt, float scaleUp = Scale)
    {
        if (target == null) return;
        Stop(target);
        Play(target, out _, out _, 1f, delay, tiltAngle, scaleUp);
    }

    /// <summary>動きを止めて、大きさ 1・傾き 0 に戻す。</summary>
    public static void Stop(Transform target)
    {
        if (target == null) return;
        target.DOKill();
        target.localScale = Vector3.one;
        target.localRotation = Quaternion.identity;
    }
}
