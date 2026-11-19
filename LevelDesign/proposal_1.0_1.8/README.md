# 案：COST_SCALE 1、COST_POWER 1.8（baseHp 1 版）

`../proposal_1.0_1.8.json` を、スプレッドシートに貼れる形にしたもの。作り方と結果は `../README.md` の 5 章を参照。

## 入れ方

1. `Stage.tsv` を Stage シートに、`SkillTreeUnit.tsv` を SkillTreeUnit シートに貼る（見出しの行も同じ）。
   - `SkillTreeUnit.tsv` は左から `skillTreeIndex`〜`cost_GG` の 9 列だけ。`[JP]` の列はシート側で参照しているので入れていない。`maxLevel` は変えないので入れていない。
2. Unity で DataBase の「スプレッドシート読み込み」を押す。
3. `Assets/Resources/SO/SO_BlockSession.asset` を次の値にする（シートからは読まない）。

| 項目 | 今 | 案 |
|---|---|---|
| baseHp | 4 | 1 |
| hpPerSecond | 0.1 | 0.025 |

## baseHp 1 にした分の換算

1 マスの HP は `(baseHp + 経過秒 × hpPerSecond) × blockHpRate`。
baseHp を 4 → 1、hpPerSecond を 0.1 → 0.025（どちらも 1/4）にし、blockHpRate を 4 倍にしたので、HP は逆算した案と同じになる。

## 変えたところ

- **Stage**：`stageTime`（ボスの出る時刻）と `blockHpRate` だけ。ほかの列は、書き出した時点のアセットの値のまま。
- **SkillTreeUnit**：コストを `今のコスト ^ 1.8`（四捨五入）にした。
  - コストは、書き出した時点のアセットの値をもとにした。アセットはスプレッドシートと同じ値（2026-11-19 に確認）。`GssPaste/SkillTreeUnit.tsv` は 4 行（1004 Lv5、2062 Lv10、2110 Lv1、4002 Lv2）が古い。

## 気をつけること

- baseHp は色のレベル 1 段分の HP（`stepHp`）でもある。1 にすると、色の段は 1 HP ずつになり、それを超えた HP はすべて一番上の色の段に入る。HP が大きい段では、ほぼいつも一番上の色で表示される（今の baseHp 4 でも、後半は同じことが起きる）。
