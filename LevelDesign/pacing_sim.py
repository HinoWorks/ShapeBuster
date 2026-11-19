"""ShapeBuster の進行を、今の数値で概算するモデル。
前提と使い方は LevelDesign/README.md を参照。

物理は再現せず、「ボール 1 個が 1 秒に当たる回数 K_HIT」を仮定する。
上から流れ込むブロックの HP と、こちらの火力（過剰ダメージは捨てる）を比べる。

使い方: python3 LevelDesign/pacing_sim.py [K_HIT]   （省略時は 2）
"""
import json, os, sys
from collections import defaultdict

DATA = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'data')

# ---------- データ（Unity の Tools → レベルデザイン → 進行計算用データを書き出す） ----------
game = {}
for line in open(os.path.join(DATA, 'game.txt'), encoding='utf-8'):
    key, _, value = line.partition(': ')
    try:
        game[key] = json.loads(value)
    except ValueError:
        pass
stages = game['Stage']['stageDatas']
rule = game['BlockSession']
boss_piece = game.get('boss', {'cellsX': 4, 'cellsY': 2, 'hpScale': 4})
cam = game.get('Camera', {'orthoSize': 5, 'aspect': 16 / 9})

bases, costs = {}, defaultdict(dict)
for l in open(os.path.join(DATA, 'skills.tsv'), encoding='utf-8'):
    r = l.rstrip('\n').split('\t')
    if r[0] == 'B':
        bases[int(r[1])] = dict(name=r[2], max=int(r[4]), target=r[6], tidx=int(r[7]), stat=r[8], delta=float(r[9]))
    elif r[0] == 'U':
        costs[int(r[1])][int(r[2])] = [int(x) for x in r[4:11]]  # AA..GG

# ---------- 定数（SO_BlockSession とコードの定数） ----------
BASE_HP, HP_PER_SEC = rule['baseHp'], rule['hpPerSecond']
BASE_SPEED, SPEED_PER_SEC = rule['baseSpeed'], rule['speedPerSecond']
BASE_FILL, FILL_PER_SEC, MAX_FILL, BOSS_FILL = rule['baseFill'], rule['fillPerSecond'], rule['maxFill'], rule['bossFillScale']
VIEW_W, VIEW_H = cam['orthoSize'] * 2 * cam['aspect'], cam['orthoSize'] * 2
CELL = VIEW_W / rule['defaultColumns'] * rule['blockSizeScale']   # 1 マス（BlockSession.BuildArena）
OPENING_ROWS = 3                                                  # BlockSession.OpeningRows
# 開始時の段の下端からデッドライン（画面下端 + 0.45）までの段数。
SLACK_ROWS = (VIEW_H - 0.5 * CELL - OPENING_ROWS * CELL - 0.45) / CELL
BOSS_CELLS = boss_piece['cellsX'] * boss_piece['cellsY']
BOSS_HPSCALE = boss_piece['hpScale']
OUTGAME_SEC = 30                                                  # 1 回のプレイごとのアウトゲームの時間
DT = 0.5

# 目標テンポ（README の表）。段ごとの突破までの回数。
TARGET_RUNS = [1, 2, 2, 3, 4, 5, 6, 8, 10, 12, 15, 20, 30]

# 案の読み込み：python3 pacing_sim.py [K_HIT] [proposal_*.json]
# 案のファイル（fit_targets.py が書き出す）があれば、ステージの値とスキルのコストをその案に置き換える。
PROPOSAL = None
if len(sys.argv) > 2 and sys.argv[2].endswith('.json'):
    PROPOSAL = json.load(open(sys.argv[2], encoding='utf-8'))
    for row in PROPOSAL['stages']:
        stages[row['stageIndex']] = dict(stages[row['stageIndex']], stageTime=row['stageTime'], blockHpRate=row['blockHpRate'])
    for sid in costs:
        for L in costs[sid]:
            costs[sid][L] = [round(PROPOSAL['cost_scale'] * c ** PROPOSAL['cost_power']) if c > 0 else 0 for c in costs[sid][L]]

# 1 秒あたり、ボール 1 個が当たる回数（球速 1 倍のとき）。引数で変えられる。
K_HIT = float(sys.argv[1]) if len(sys.argv) > 1 else 2.0
# 範囲攻撃が巻き込むマス数の目安。
AREA = {'bomb': 8, 'laser': 6, 'row': 6, 'cross': 8, 'poison': 4}

def block_mix(st):
    w = dict(square=st['weightSquare'], wide=st['weightWide'], tall=st['weightTall'],
             solid=st['weightSolid'], armor=st['weightArmor'], regen=st['weightRegen'],
             bloat=st.get('weightBloat', 0), split=st.get('weightSplit', 0), ghost=st.get('weightGhost', 0))
    # 分裂は、親 1 マス ＋ 子 2 体 × splitHpRate ぶんの HP を持つ 1 マスとして扱う。
    split_rate = game.get('split', {}).get('splitHpRate', 0.4) * game.get('split', {}).get('splitCount', 2)
    # 幽霊は、すり抜けている間はレーザーしか当たらないので、周期 ÷ 実体の時間 ぶん HP が多い 1 マスとして扱う。
    g = game.get('ghost', {})
    ghost_solid = g.get('ghostSolidTime', 2.4) or 2.4
    ghost_rate = (ghost_solid + g.get('ghostPhaseTime', 1.6)) / ghost_solid
    cells = dict(square=1, wide=3, tall=2, solid=1, armor=1, regen=1, bloat=1, split=1 + split_rate, ghost=ghost_rate)
    tot = sum(w.values()) or 1
    return [(k, w[k] / tot, cells[k]) for k in w if w[k] > 0]

# ---------- プレイヤーの強さ ----------
def S(lv, stat, tidx=None, target=None):
    t = 0.0
    for sid, L in lv.items():
        b = bases[sid]
        if b['stat'] == stat and (tidx is None or b['tidx'] == tidx) and (target is None or b['target'] == target):
            t += b['delta'] * L
    return t

BALLS = [  # (名前, BallTarget, 素の比率, 範囲の種類, 素の発生率)
    ('bomb', 0, 0.5, 'bomb', 0.1), ('laser', 1, 0.3, 'laser', 0.1), ('heavy', 2, 0.5, None, 0.0),
    ('row', 3, 0.5, 'row', 0.1), ('orbit', 4, 0.5, None, 0.0), ('cross', 5, 0.5, 'cross', 0.1),
    ('poison', 6, 0.5, 'poison', 0.1)]

def power(lv):
    """ボールごとの (1 秒の当たり回数, 1 発の威力, 範囲の発生率, 範囲のマス数) と、補正値。"""
    normal = (1 + S(lv, 'BallFlatDamage', target='Ball')) * (1 + S(lv, 'BallDamageRate', target='Ball'))
    crit_rate = min(1, S(lv, 'BallCritRate'))
    crit_mult = 2 + S(lv, 'BallCritDamage')
    crit = 1 + crit_rate * (crit_mult - 1)
    speed = 1 + S(lv, 'BallSpeed')
    k = K_HIT * speed ** 0.8
    chain = 1.25 if S(lv, 'BallChain') >= 1 else 1.0
    first = 1 + S(lv, 'BallFirstAttack')
    full = 1 + 0.5 * S(lv, 'BallFullStack')   # 全回収発射は半分くらいの時間
    mult = normal * crit * chain * full
    shots = []
    n_normal = 1 + round(S(lv, 'BallCount'))
    for _ in range(n_normal):
        shots.append((k, mult, 0, 0))
    for name, idx, ratio0, area, proc0 in BALLS:
        if S(lv, 'BallUnlock', tidx=idx) < 1:
            continue
        copies = 2 if S(lv, 'BallExtra', tidx=idx) >= 1 else 1
        ratio = ratio0 + S(lv, 'BallRatio', tidx=idx)
        proc = min(1, proc0 + S(lv, 'BallProc', tidx=idx)) if area else 0
        kk = k * (0.5 + S(lv, 'BallHeavySpeed', tidx=2)) / 1.0 if name == 'heavy' else k
        hits = 3 if name == 'heavy' else 1          # 貫通
        if name == 'orbit':
            hits = 1 + 0.5 * (3 + S(lv, 'BallOrbitCount', tidx=4))
        for _ in range(copies):
            shots.append((kk * hits, mult * ratio, proc, AREA.get(area, 0)))
    boss = 1 + S(lv, 'BallBoss')
    return shots, first, boss

def eff_damage(d, mix, H, first):
    """1 発でブロックに入る実ダメージの期待値。過剰分は捨てる。装甲は 3 割弾かれる。"""
    e = 0
    for kind, p, cells in mix:
        if kind == 'solid':
            continue
        hp = cells * H
        dd = min(d * first, hp) if d * first >= hp else d   # 先制は最初の 1 発だけ。倒しきれるときだけ効くと近似
        dd = min(dd, hp)
        if kind == 'armor':
            dd *= 0.7
        e += p * dd
    return e

# ---------- 1 回のプレイ ----------
def run(st, lv):
    mix = block_mix(st)
    shots, first, boss_mult = power(lv)
    cols = st['blockColumns']
    hp_rate, spd_rate, res_rate = st['blockHpRate'], st['blockSpeedRate'], st['blockResourceRate']
    harvest = 1 + S(lv, 'BlockHarvest')
    backlog = float(OPENING_ROWS)   # まだ壊していない段
    t, kills_cells = 0.0, defaultdict(float)
    boss_hp = None
    boss_dead = False
    avg_cells = sum(p * c for _, p, c in mix)
    while True:
        H = (BASE_HP + t * HP_PER_SEC) * hp_rate
        speed = (BASE_SPEED + t * SPEED_PER_SEC) * spd_rate
        fill = min(MAX_FILL, BASE_FILL + FILL_PER_SEC * t) * (BOSS_FILL if t >= st['stageTime'] else 1)
        rows_in = speed / CELL * DT
        backlog += rows_in
        # 火力（実ダメージ/秒）
        dps = 0
        for k, d, proc, area in shots:
            dps += k * (eff_damage(d, mix, H, first) + proc * area * eff_damage(d, mix, H, 1))
        row_hp = cols * fill * H
        budget = dps * DT
        if t >= st['stageTime'] and not boss_dead:
            if boss_hp is None:
                boss_hp = (BASE_HP + st['stageTime'] * HP_PER_SEC) * hp_rate * BOSS_CELLS * BOSS_HPSCALE
            # 押し返しに必要な分を除いた余りをボスへ。ボスには過剰ダメージが出ない。
            need = min(budget, max(0, backlog - 1) * row_hp)
            spare = budget - need
            boss_hit = sum(k * d * boss_mult for k, d, _, _ in shots) * DT
            boss_hp -= min(spare / max(dps * DT, 1e-9) * boss_hit, boss_hit)
            budget = need
            if boss_hp <= 0:
                boss_dead = True
        cleared = min(backlog, budget / max(row_hp, 1e-9))
        backlog -= cleared
        for kind, p, cells in mix:
            if kind != 'solid':
                kills_cells[kind] += cleared * cols * fill * p   # マス数
        t += DT
        if boss_dead:
            return t, True, kills_cells, harvest, res_rate
        if backlog >= SLACK_ROWS + OPENING_ROWS or t > 400:
            return t, False, kills_cells, harvest, res_rate

def income(kills, harvest, res_rate, lv):
    bloat_reward = game.get('bloat', {}).get('rewardScale', 2)
    aa = (kills['square'] + kills['wide'] + kills['tall'] + kills['bloat'] * bloat_reward) * harvest * res_rate
    bb = kills['regen'] * (1 + S(lv, 'RegenDrop')) * res_rate
    cc = kills['armor'] * (1 + S(lv, 'ArmorDrop')) * res_rate
    return [aa, bb, cc]

# ---------- 買い物（効率のよい順に買う） ----------
SKIP = {'BlockMagnet', 'BlockLinger', 'BallCatch', 'MilestoneUnlock', 'ConvertUnlock', 'BlockCompact',
        'RegenSpawn', 'ArmorSpawn'}

def score(lv, stage_idx=5):
    """強さの目安。今いる段の 45 秒時点での実ダメージ/秒。"""
    st = stages[min(stage_idx, len(stages) - 1)]
    mix = block_mix(st)
    shots, first, boss = power(lv)
    H = (BASE_HP + 45 * HP_PER_SEC) * st['blockHpRate']
    v = sum(k * (eff_damage(d, mix, H, first) + p * a * eff_damage(d, mix, H, 1)) for k, d, p, a in shots)
    return v * (1 + 0.15 * (boss - 1)) * (1 + S(lv, 'BlockHarvest')) ** 0.7

def shop(lv, wallet, stage_idx):
    bought = 0
    while True:
        base = score(lv, stage_idx)
        best = None
        for sid, b in bases.items():
            if b['stat'] in SKIP:
                continue
            L = lv.get(sid, 0)
            if L >= b['max'] or (L + 1) not in costs[sid]:
                continue
            c = costs[sid][L + 1][:3]
            if any(c[i] > wallet[i] for i in range(3)):
                continue
            lv[sid] = L + 1
            gain = score(lv, stage_idx) / base - 1
            lv[sid] = L
            if L == 0 and b['stat'] == 'BallUnlock':
                gain += 0.01
            val = gain / max(1, sum(c))
            if best is None or val > best[0]:
                best = (val, sid, c)
        if best is None or best[0] <= 0:
            return bought
        _, sid, c = best
        lv[sid] = lv.get(sid, 0) + 1
        for i in range(3):
            wallet[i] -= c[i]
        bought += 1

def total_tree_cost():
    t = [0] * 7
    for sid, levels in costs.items():
        for L, c in levels.items():
            for i in range(7):
                t[i] += c[i]
    return t

def campaign(max_runs=400):
    lv, wallet = {}, [0.0, 0.0, 0.0]
    stage, runs_in_stage, total_runs, total_sec = 0, 0, 0, 0
    rows = []
    buys = []
    while stage < len(stages) and total_runs < max_runs:
        st = stages[stage]
        t, win, kills, harvest, res_rate = run(st, lv)
        inc = income(kills, harvest, res_rate, lv)
        for i in range(3):
            wallet[i] += inc[i]
        n = shop(lv, wallet, stage)
        buys.append(n)
        runs_in_stage += 1
        total_runs += 1
        total_sec += t + OUTGAME_SEC
        if win:
            rows.append(dict(stage=stage, runs=runs_in_stage, last_time=t, total_runs=total_runs,
                             total_min=total_sec / 60, income=[round(x) for x in inc],
                             levels=sum(lv.values())))
            stage += 1
            runs_in_stage = 0
    return rows, lv, buys, total_runs

if __name__ == '__main__':
    rows, lv, buys, n = campaign()
    print(f'K_HIT = {K_HIT}（ボール 1 個が 1 秒に当たる回数）')
    print('段 | 目標の回数 | 計算の回数 | 突破時のプレイ時間 | 計算の累計時間(分) | 突破時の稼ぎ AA/BB/CC | 合計レベル')
    for r in rows:
        tgt = TARGET_RUNS[r['stage']] if r['stage'] < len(TARGET_RUNS) else '-'
        print(f"{r['stage']:>2} | {tgt:>4} | {r['runs']:>4} | {r['last_time']:>5.1f} 秒 | {r['total_min']:>6.1f} | {r['income']} | {r['levels']}")
    if len(rows) < len(stages):
        print(f'段 {len(rows)} で {n} 回を超えたので打ち切り')
    tc = total_tree_cost()
    print('ツリー全体のコスト AA/BB/CC/DD/EE/FF/GG:', tc)
    mx = sum(b['max'] for s, b in bases.items() if b['stat'] not in SKIP)
    print('買ったレベル:', sum(lv.values()), '/', mx, '（計算で買わない種類は除く）')
    print('プレイごとに買った数:', buys)
