"""目標テンポ表（TARGET_RUNS）どおりに進むよう、敵の HP の倍率とスキルのコストを逆算する。
前提は LevelDesign/README.md。計算そのものは pacing_sim.py を使う。

やり方：
  1. スキルのコストを COST_SCALE × 今のコスト ^ COST_POWER にする（高いスキルほど大きく値上げする）。
  2. 段 0 から順に、「ちょうど目標の回数目で突破する、いちばん大きい blockHpRate」を二分探索する。
  3. 決めた値で進めた状態から、次の段を同じように探す。

使い方: python3 LevelDesign/fit_targets.py [COST_SCALE] [COST_POWER] [K_HIT]
"""
import copy, sys

cost_scale = float(sys.argv[1]) if len(sys.argv) > 1 else 1.0
cost_power = float(sys.argv[2]) if len(sys.argv) > 2 else 1.0
k_hit = sys.argv[3] if len(sys.argv) > 3 else '2'
sys.argv = [sys.argv[0], k_hit]
import pacing_sim as P

for sid in P.costs:
    for L in P.costs[sid]:
        P.costs[sid][L] = [round(cost_scale * c ** cost_power) if c > 0 else 0 for c in P.costs[sid][L]]

MAX_RUNS = 60
# ボスの出る時刻（stageTime）。ボス戦を含めて 1 回が目標の 60〜90 秒に収まるよう、目標の時間より 15 秒早くする。
BOSS_TIMES = [45, 45, 50, 55, 55, 60, 60, 65, 65, 65, 70, 70, 75]


def play_stage(st, lv, wallet, stage_idx, limit):
    """突破するまで遊ぶ。(回数, 各回で買った数, 各回の時間, 突破したか) を返す。lv と wallet は書き換える。"""
    buys, times = [], []
    for n in range(1, limit + 1):
        t, win, kills, harvest, res_rate = P.run(st, lv)
        inc = P.income(kills, harvest, res_rate, lv)
        for i in range(3):
            wallet[i] += inc[i]
        buys.append(P.shop(lv, wallet, stage_idx))
        times.append(t)
        if win:
            return n, buys, times, True
    return limit, buys, times, False


def fit():
    lv, wallet = {}, [0.0, 0.0, 0.0]
    result = []
    prev_hp = 0.05
    for s, st in enumerate(P.stages):
        target = P.TARGET_RUNS[s]

        def runs_for(hp):
            st2 = dict(st, blockHpRate=hp, stageTime=BOSS_TIMES[s])
            P.stages[s] = st2
            n, buys, times, win = play_stage(st2, copy.deepcopy(lv), list(wallet), s, target + 1)
            return n if win else target + 1

        # 目標の回数以内に突破できる、いちばん大きい倍率を探す。
        lo, hi = prev_hp * 0.5, prev_hp * 2
        while runs_for(hi) <= target and hi < 1e7:
            lo, hi = hi, hi * 2
        while runs_for(lo) > target and lo > 1e-3:
            hi, lo = lo, lo / 2
        for _ in range(14):
            mid = (lo * hi) ** 0.5
            if runs_for(mid) <= target:
                lo = mid
            else:
                hi = mid
        hp = lo
        st2 = dict(st, blockHpRate=hp, stageTime=BOSS_TIMES[s])
        P.stages[s] = st2
        n, buys, times, win = play_stage(st2, lv, wallet, s, MAX_RUNS)
        result.append(dict(stage=s, hp=hp, runs=n, target=target, buys=buys, times=times,
                           levels=sum(lv.values())))
        prev_hp = hp
    return result, lv


if __name__ == '__main__':
    result, lv = fit()
    print(f'COST_SCALE = {cost_scale}  COST_POWER = {cost_power}  K_HIT = {P.K_HIT}')
    print('ツリー全体のコスト AA/BB/CC:', P.total_tree_cost()[:3])
    print('段 | 今の倍率 | 逆算した倍率 | 前の段との比 | 目標 | 回数 | 1 回の時間（平均） | 買った数（最小/平均） | 合計レベル')
    prev = None
    total_sec = 0
    import json, os
    original = []
    for line in open(os.path.join(P.DATA, 'game.txt'), encoding='utf-8'):
        if line.startswith('Stage: '):
            original = [x['blockHpRate'] for x in json.loads(line[7:])['stageDatas']]
    for r in result:
        ratio = r['hp'] / prev if prev else 0
        prev = r['hp']
        avg_t = sum(r['times']) / len(r['times'])
        total_sec += sum(t + P.OUTGAME_SEC for t in r['times'])
        print(f"{r['stage']:>2} | {original[r['stage']]:>6.2f} | {r['hp']:>10.2f} | {ratio:>5.2f} | {r['target']:>3} | {r['runs']:>3} | "
              f"{avg_t:>5.1f} 秒 | {min(r['buys'])}/{sum(r['buys']) / len(r['buys']):.1f} | {r['levels']}")
    print(f'累計 {total_sec / 60:.0f} 分')
    print('買ったレベル:', sum(lv.values()))
    out = os.path.join(os.path.dirname(os.path.abspath(__file__)), f'proposal_{cost_scale}_{cost_power}.json')
    json.dump(dict(cost_scale=cost_scale, cost_power=cost_power, k_hit=P.K_HIT,
                   stages=[dict(stageIndex=r['stage'], stageTime=BOSS_TIMES[r['stage']], blockHpRate=round(r['hp'], 3))
                           for r in result]), open(out, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    print('保存:', out)
