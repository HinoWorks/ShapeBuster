using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD の縦の列。手元のボールを、次に撃つものから上から順に並べる。
/// 撃ったボールは黒い枠だけになって列の下へ回り、手元のボールが上へずれる。
/// 戻ったボールは、手元の最後（枠の上）に並ぶ。
/// 並べるのは子の VerticalLayoutGroup。子の枠（pf_ui_ballSlot）を上から順に使い、余った枠は隠す。
/// 枠が足りないときは slotPrefab から足す。
/// </summary>
public class UI_BallQueue : MonoBehaviour
{
    [Serializable]
    public struct KindIcon
    {
        public BlockBallKind kind;
        public Sprite sprite;
    }

    [SerializeField] Image slotPrefab; // 枠が足りないときに足す 1 個分の見た目（pf_ui_ballSlot）
    [SerializeField] KindIcon[] icons = Array.Empty<KindIcon>();
    [SerializeField] Sprite emptyFrame; // 撃って手元にないボールの枠
    [SerializeField] GameObject obj_fullStack; // 「全弾」を覚えていて、ボールが全部手元にあるときに出す（今撃てば攻撃力が上がる）

    readonly List<BlockBall> held = new();
    readonly List<BlockBall> away = new();
    readonly List<Image> slots = new();
    bool collected;

    void LateUpdate()
    {
        var session = BlockSession.Inst;
        if (session != null)
            session.GetHand(held, away);
        else
        {
            held.Clear();
            away.Clear();
        }

        int count = held.Count + away.Count;
        EnsureSlots(count);
        RefreshFullStack(count);

        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            bool used = i < count;
            if (slot.gameObject.activeSelf != used)
                slot.gameObject.SetActive(used);
            if (!used) continue;

            var sprite = i < held.Count ? IconOf(held[i].Kind) : emptyFrame;
            if (slot.sprite != sprite)
                slot.sprite = sprite;
            slot.enabled = sprite != null;
        }
    }

    void RefreshFullStack(int count)
    {
        if (obj_fullStack == null) return;
        bool show = count > 0 && held.Count == count && SkillTreeBonus.FullStackRate > 0f;
        if (obj_fullStack.activeSelf != show)
            obj_fullStack.SetActive(show);
    }

    /// <summary>子の枠を上から順に集め、足りなければ slotPrefab から足す。</summary>
    void EnsureSlots(int count)
    {
        if (!collected)
        {
            collected = true;
            foreach (Transform child in transform)
            {
                var image = child.GetComponent<Image>();
                if (image != null)
                    slots.Add(image);
            }
        }

        while (slots.Count < count && slotPrefab != null)
            slots.Add(Instantiate(slotPrefab, transform));
    }

    Sprite IconOf(BlockBallKind kind)
    {
        for (int i = 0; i < icons.Length; i++)
        {
            if (icons[i].kind == kind)
                return icons[i].sprite;
        }
        return null;
    }
}
