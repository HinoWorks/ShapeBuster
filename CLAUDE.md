# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 回答言語

ユーザーへの回答と、作成する `.md` ファイルはすべて日本語で書く。

## プロジェクト概要

ShapeBuster は Unity **6000.2.7f2**（URP・2D）のゲーム。インゲームはブロック崩し（ボールを発射して、下りてくるブロック列を壊す）。その外側に、リソース・スキルツリー・マイルストーン・リソース変換画面からなる放置／育成系のメタループがある。コードのコメントや識別子の多くは日本語で、新しく書くコメントも同じ書き方にそろえる。

コマンドラインでのビルド・Lint・テストの仕組みはない。コンパイル・実行・ビルドは Unity エディタで行う。ルートの `.csproj`／`.sln` は Unity が自動生成するので編集しない。プロジェクト独自のテストはない（`*Test*` という名前のファイルは SRDebugger プラグインのもの）。

`Packages/manifest.json` に MCP for Unity（`com.coplaydev.unity-mcp`）が入っている。エディタが開いていて MCP サーバーがつながっていれば、コンパイル結果やコンソールのエラーはその MCP ツールで確認する。つながっていないときは、コードを変更したあとエディタでの確認をユーザーに頼む。

## 構成

- `Assets/Script/`：ゲームコードはすべてここ。asmdef はなく、既定の `Assembly-CSharp` に入る。サブフォルダは `Block`（インゲーム）、`Common`（マネージャー類）、`Milestone`、`Bestiary`（ゾンビ図鑑）、`Vaccine`（ゾンビワクチン＝最終目標とエンディング）、`Steam`（Steamworks の接続と実績）、`Enemy`／`Resource`（敵の生成と、倒したときに落ちるリソースのプール）、`Effect`、`SO`（ScriptableObject の定義）、`UI/{Base,Title,Out,In}`、`IF`（`IDamagable` などのインターフェース）、`Debug`。
- `Assets/Resources/SO/`：ScriptableObject のデータアセット。実行時に名前で読み込む。
- `Assets/Scenes/SampleScene.unity`：シーンはこれ一つだけ。
- `Assets/Plugins/`：DOTween（Demigiant）と Easy Save 3。`Assets/StompyRobot/` は SRDebugger。git から入れているパッケージは UniTask・UniRx・Coffee UIParticle・Steamworks.NET。
- `Assets/asset/CSVSerializer/`：マスターデータの読み込みに使う CSV パーサー。

## アーキテクチャ

**シーンは一つ、状態遷移で進める。** `GameWatcher`（シングルトン `Inst`）はシーンを切り替えず、`GameStateType` だけを進める：Title → OutGame → InGame_Ready → InGame → InGame_End → Result →（`ResultEnd_ToOutGame` を経て OutGame へ、または `ResultEnd_ToIngameReady` を経てもう一度 InGame_Ready へ）。遷移のたびに `GameEvent.GameState.SetGameState` で通知する。各システムや UI は直接呼ばれるのではなく、この通知を購読して自分の有効／無効を切り替える。Title 以外の状態へは、`GameParamManager.isInitEnd` を待ってから入る。

**イベントバス。** `GameEvent`（`Script/Common/GameEvent.cs`）は UniRx の `Subject` を static にまとめたもので、入れ子クラス（`GameState`・`InGame`・`Stage`・`Input` など）に分かれている。それぞれ `PublishX` メソッドと `IObservable` プロパティを持つ。システムをまたぐ新しい通知はここに足し、`.AddTo(this)` 付きで購読する。

**シングルトンと static クラス。** マネージャーは `Awake` で `public static X Inst` を設定する。ブロック崩しの本体がシーンにない場合は、`BlockSession.Ensure()` が実行時に生成する。ロジックだけのものは static クラスにある：`GameParamManager`、`SkillTreeBonus`、`MilestoneBonus`、`BestiaryBonus`／`BestiaryRuntime`、`VaccineRuntime`、`Loc`、`SOLoader`、`StaticManager`（定数、BigInteger の数値表示、時間の減速）。

**インゲーム。** `BlockSession`（約 2,500 行の大きなクラス）が 1 回のプレイ全体を受け持つ：列の生成（`BlockRowEmitter`）、ブロック（`BlockPiece`）、ボール（`BlockBall`）、タレット、発射回数の上限、照準ガイド、クリア／失敗。ステージ中に倒した数によるドロップ量とレア出現率の上昇は static クラスの `StageHarvest` が持ち、ステージを出るとリセットされる。調整値は `SO_BlockSession` から、ステージごとの値（列数、HP・速度の倍率、ブロック種類の出現比率）は `SO_StageData` から読む。

**ボーナスの集計。** スキルツリーとマイルストーンの効果は、少しずつ足していく方式ではない：
- `SkillTreeBonus` はスキルごとの効果を「1 レベル分の差分 × 現在レベル」としてスキル単位で持つ。セーブからの復元もプレイ中の強化も同じ処理を通るので、効果が二重に足されない。
- `MilestoneBonus` は受け取り済み段階の合計をキャッシュし、`Invalidate()` が呼ばれたら作り直す。次の破壊から反映されるものと、次のラン開始時に反映されるものがある。
- `BestiaryBonus`（図鑑の研究効果）は `SkillTreeBonus` と同じ「1 Lv 分 × 研究 Lv」を項目ごとに持ち、`Invalidate()` で作り直す。

ゲーム側の処理は、これらのクラスから最終的な値を読む（例：`MilestoneBonus.BallDamageRate`）。

**データ（ScriptableObject）。** SO アセットは `SOLoader.X` プロパティ（遅延読み込み）から取得する。中では `Resources.Load("SO/<型名>")` を呼んでいるので、アセットのファイル名は型名と一致させる必要がある。

**マスターデータは Google スプレッドシートから。** `DataBase`（MonoBehaviour。カスタムインスペクターは `Assets/Editor/GSSReaderEditor.cs`）に「スプレッドシート読み込み」ボタンがある。押すと各シート（Stage・SkillBase・SkillCost・Milestone・Ball・Bestiary・Locale）を gviz エンドポイント経由で CSV として取得し、`CSVSerializer` で SO アセットへ変換してから、アセットを Dirty にして保存する。データクラスのフィールドを変えたら、シートの列見出しも合わせること。スキルの値段は SkillCost シート（1 スキル 1 行。`resource` で資源を 1 つ選び、`Lv1`, `Lv2`… に段の値段を右へ並べる）から読み、`SO_SkillTreeData.UnitsFromCostSheet` で段ごとの `SkillTreeUnit` に広げる。スキルの上限レベルは、SkillCost で値段の入っている最後の段で決まる（`maxLevel` の列はどのシートにも無い）。スキルのデータの管理のまとめは `LevelDesign/SkillData.md`。SkillCost が無いときだけ古い SkillTreeUnit シート（段ごとに 1 行、`cost_AA`〜`cost_FF`）を読む。gviz は無いシート名を頼むと先頭のシートを返すので、シートの有無は見出しで見分けている。Stage シートは読み込み後に `SO_StageData.ApplyBlockCampaign()` を通る。シートのデータが 13 段（`BlockCampaign.StageCount`）に足りないときや、最後の段にブロック種類の出現比率がないときは、`BlockCampaign.CreateStages()` にハードコードされた値で全体が上書きされる。Locale シートが文言の正本で、読み込むと `SO_Locale` はシートの中身で丸ごと置き換わる（シートに無い行は消えるので、エディタで件数を確かめるダイアログが出る）。文言はシートで直し、アセットだけを直さない。`GssPaste/` は、これらのシートに貼り付けるための TSV を置く場所（未コミット。`.gitignore` にも入っていない）。どのファイルをどこに貼るかと、今入っている値は `GssPaste/README.md` に書く。

**セーブ。** `SaveLoader`（シングルトン）は Easy Save 3（`ES3`）で保存する。セクションごとに読み込み失敗フラグを持ち、読み込めなかったデータを上書きしないようにしている。言語設定だけは `Loc` が別に保存する（`PlayerPrefs` のキー `key_language`）。

**Steam。** `SteamManager` は `RuntimeInitializeOnLoadMethod` で自動生成され、Steam が動いていなければ何もしない（ゲームはそのまま動く）。Windows／macOS／Linux 以外では `DISABLESTEAMWORKS` で無効になる。AppID はプロジェクト直下の `steam_appid.txt`（今は試し用の 480）と `SteamManager.ReleaseAppId`。実績は `SteamAchievements` が持ち、`SaveLoader` の読み込み後に `Bind` され、セーブの状態から条件を判定する。詳細は `LevelDesign/Achievements.md`。

**多言語対応。** `Loc` は `SO_Locale`（列は `key`／`ja`／`en`）からキーで文字列を引き、言語が変わると `Changed` イベントを出す。固定のラベルには `UI_LocalizedText` コンポーネントを使う。TMP の日本語フォントアトラスにない文字は四角で表示されるので、**Tools → 日本語フォントを更新**（`Assets/Editor/JapaneseFontBake.cs`）で焼き込む。

**UI。** 画面は `UI/Title`、`UI/Out`（`UI_OutGameTabBase` を継承したアウトゲームのタブ：スキルツリー、マイルストーン、ステージ選択、変換）、`UI/In`（HUD、リザルト、ダメージ数値）に分かれている。ボタンは独自の `HButton`／`HButtonSound` を使い、ポップアップは `UI_PopUpBase` を継承する。非同期処理は UniTask、Tween は DOTween。

## レベルデザイン

進行のテンポ（全体 3〜4 時間、段ごとの突破までの回数）の目標と、それを確かめる計算の前提は `LevelDesign/README.md` にある。ステージ・スキル・ブロックの数値を変えたら、Unity の **Tools → レベルデザイン → 進行計算用データを書き出す** を実行してから `python3 LevelDesign/pacing_sim.py` で計算し直す。ゲームの仕組み（HP や資源の計算式）を変えたら、README の 2 章と `pacing_sim.py` も合わせて直す。ボスの種類（タイプ × 形）と段への割り当て、未作成の案は `LevelDesign/BossDesign.md` にある。ほかの仕様書も `LevelDesign/` にある：`Bestiary.md`（図鑑）、`ZombieVaccine.md`（ワクチンとエンディング）、`Achievements.md`（Steam 実績）、`Options.md`（オプションメニュー）、`SoundCandidates.md`（音源の候補）、`BallPitReference.md`（参考作の調査メモ）、`StorePage.md`（Steam ストアページの文章）。機能を変えたら対応する仕様書も直す。
