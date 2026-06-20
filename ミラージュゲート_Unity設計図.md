# ミラージュゲート — Unityフルリメイク設計図

> 原作: `monster_gate.html`（単一HTML・約7,670行・Canvas2D＋素JS）  
> 本書: ゲーム要素のみを言語化した実装非依存の設計図。描画(Canvas)の実装細部は除外し、**ルール・データ・式・体験仕様**を抜け漏れなく記述する。  
> 数値は原作ソースからの引用。`§`は本書の節、`L####`は原作の行番号目安（編集で前後するのでgrep再特定推奨）。  
> 作成: 2026-06-18 ／ 更新: 2026-06-20（**§15 に挙動・近接仲間AI・8方向アニメーションの直近変更を反映**）。詳細は別紙 `ミラージュゲート_詳細仕様書.md` を参照。

---

## 0. このゲームは何か（30秒サマリ）

- **ジャンル**: 1ターン制ローグライク × メダルRPG × デッキ構築。グリッド移動・遭遇即戦闘の見下ろし型ダンジョン探索。
- **1ターンの単位**: 「1マス移動」または「1回の行動（攻撃／カード使用）」＝1ターン。プレイヤーが動くと敵も動く（ターンベース）。
- **目的**: ダンジョンに**BET（賭けメダル）**を払って潜り、敵撃破・宝石拾得で**配当(loot)**を稼ぎ、**目標WIN**到達＝最深部のクリスタル到達でクリア。配当はメダルに統合され、次の挑戦やショップ購入の元手になる。
- **ビルド**: 出撃前に**手札（カードデッキ）**を編成。カードは攻撃魔法・回復・状態異常・バフ・装備の5系統。MPで使う。手札は**ランごとに使い切り**＝リスク資産。
- **2つの軸**: ①ローグライクの「強くなって深く潜る」リスク＆リターン。②デッキ構築の「引き・編成・取捨選択」。
- **物語**: 封印カードの迷宮を巡るVNストーリー（2部構成・分岐エンディング）。フリープレイ／ワールドモードも併設。
- **核の手触り**: 1ターン制なのに「当たった・効いた・会心」を**積層演出**（踏み込み→斬撃FX→ヒットストップ→けぞり振動→ダメージ数字→画面揺れ→撃破演出→SE）で強く伝える。これが本作の生命線。

---

## 1. データ駆動アーキテクチャ（Unity設計の全体像）

原作は全データをJSオブジェクト辞書で保持。Unityでは**ScriptableObject(SO)**へ移し、ロジックと分離する。

| SOアセット | 原作の対応 | 主フィールド |
|---|---|---|
| `JobData` | `JOBS` (§3) | key, jpName, emoji, hp, mp, atk, def, sight, capIdx, potBonus, magBonus, crit?, luck?, mpHalf?, hidden? |
| `CardData` | `CARDS` (§4) | id, name, category, icon, cost, mp, desc, 効果フィールド群(mag/heal/poison/swordMul/ring/…), forbidden?, costMaxHp?, selfDmg? |
| `MonsterData` | `MONSTERS` (§5) | name, hp, atk, def, role, pay, cap[3], minDmg?, emoji |
| `DungeonData` | `DUNGEONS` (§6) | id, jpName, star, floors, bet, win, size, rooms, dense, spawn, bands[floor][], tex, hidden?, bagLimit? |
| `StatusEffectData` | 状態異常 (§5.3) | type, defaultDuration, tickEffect, target(self/enemy), blockedBy(magicimmune等) |
| `StoryChapterData` | `STORY`/`STORY2` (§10) | key, title, dungeon, intro[], boss, outro[], final? |
| `DialogueData` | `playCutscene` lines (§11) | who, text, face, effect?, choices? |

**ロジック層（MonoBehaviour / 純Cクラス）**: `TurnManager`, `DungeonGenerator`, `CombatResolver`, `DamageCalculator`, `CardEffectExecutor`, `EnemyAI`, `StatusEffectManager`, `EconomyManager`, `ShopSystem`, `SlotSystem`, `ProgressionSystem`, `SaveManager`, `DialogueManager`, `TutorialManager`, `GameFeelDirector`(演出専用).

**重要原則「Visual Only」**: 全演出（揺れ・FX・ノックバック）は**論理座標に一切干渉しない**。キャラの実座標は不変で、描画時だけオフセット・変形・エフェクトを足す。衝突判定・AI・移動はブレず、手応えだけ最大化する。Unityでも演出は表示用Transform/別レイヤーで行い、ゲームステートと分離すること。

---

## 2. コアループ & ターン順序（§Combat抽出）

### 2.1 状態遷移
```
タイトル → モード選択 →（ストーリー/フリー/ワールド分岐）→ デッキ編成 → 出撃(startRun)
  → buildFloor（マップ生成）→ ゲームループ → クリア(clearDungeon) or 敗北(gameOver) → 報酬/リトライ
```

### 2.2 1ターンの処理順（`playerTurn` L3550 付近）
1. **入力受理** `tryAct(dir)`：移動／攻撃／カード使用／その場待機。
2. **プレイヤー行動確定**
   - 移動 → 氷結スライド判定 → 部屋入室回復 → 床ギミックダメージ(毒1/溶岩2/呪2)
   - 攻撃 → `doPlayerAttack(mon)`（§8.2）
   - カード → `resolveCard(i, target)`（§4.3）
3. **ターンロック判定**：`now() < G._lockUntil`（ヒットストップ中）は入力を受けない。
4. **敵フェーズ** `enemyPhase()` L3861：味方(ally)行動 → 各敵の索敵・移動・攻撃 → 床ギミック → 敵回復AI。
   - **コンボ中**(`G.status.combo>0`)は敵フェーズをスキップ＝プレイヤー追加行動。
5. **状態異常カウントダウン** `tickStatuses()`：barrier/invis/speed/regen 等を1減らす。毒は敵/自分に毎ターン適用。
6. **視界再計算** `computeVisibility()` → 描画。

### 2.3 Unity実装指針
- `TurnManager` をステートマシン化（`AwaitInput → ResolvePlayer → EnemyPhase → Upkeep → AwaitInput`）。
- 敵フェーズは Coroutine + `WaitForSecondsRealtime` で「揺れ収束後に次の敵が動く」テンポを再現（敵攻撃の時間差180ms×敵番号、L4092）。
- 入力ロックは実時間（`Time.realtimeSinceStartup`）で管理。ヒットストップとは独立。

---

## 3. プレイヤー & 職業（§Player抽出）

### 3.1 職業テーブル `JOBS`（L1299–1355）
基本3職＋隠し職。`capIdx`は敵の`cap[]`配列のどの列を見るか（0=物理/1=魔法/2=幸運系の育成上限）。

| key | 表示 | HP | MP | ATK | DEF | sight | capIdx | 特性 |
|---|---|---|---|---|---|---|---|---|
| warrior | 戦士 | 50 | 20 | 12 | 6 | 3 | 0 | 標準（potBonus1.0 / magBonus1.0） |
| mage | 魔法使い | 42 | 40 | 8 | 4 | 4 | 1 | potBonus1.25 / magBonus1.5（魔法特化・視界広） |
| gambler | ギャンブラー | 34 | 26 | 9 | 3 | 3 | 2 | luck=true（撃破時30%で配当+pay） |
| samurai | 侍 | 44 | 22 | 14 | 5 | 3 | 0 | crit0.22（高会心） |
| dragoon | 竜騎士 | 58 | 24 | 11 | 9 | 3 | 0 | 重装・magBonus1.1 |
| paladin | 聖騎士 | 56 | 22 | 14 | 9 | 3 | 0 | potBonus1.25 / crit0.16（両刀） |
| sorcerer | 魔導士 | 36 | 38 | 9 | 4 | 3 | 1 | potBonus1.15 / magBonus1.5 |
| darkknight | 闇騎士 | 56 | 24 | 16 | 5 | 3 | 0 | magBonus1.15 / crit0.16（高火力） |
| ren | レン（物語専用） | 46 | 23 | 10 | 5 | 3 | 0 | mpHalf=true（MP=HP/2に同期）/ potBonus1.1 / magBonus1.25・hidden |
| (他) fallen, valkyrie, magicblade, reddragoon, gilgamesh, lucifer, odin | 隠し職 | — | — | — | — | — | — | unlocked フラグで解放 |

> 注: 原作で隠し職の正確な数値は要ソース確認（`JOBS`定義を全件移植すること）。基本3職＋レンが本筋。

### 3.2 プレイヤーのランタイム状態（`G` のプレイヤー域 L1560–1572）
- 戦闘: `hp/maxhp`, `mp/maxmp`, `atk`, `def`, `lvl`(初期1), `shieldDef`(盾加算), `swordMul`(剣倍率), `swordAtk`(剣加算)
- 手札: `hand[]`(カードid配列), `handMax`(初期10・最大12), `bag[]`(持ち込み枠), `bagMax`(10)
- 進行: `floor`(初期1), `px/py`(座標), `loot`(ラン内配当・クリアでmedalsへ)
- 装備/バフ: `equip{sword,shield,rings[]}`, `rings{magic,lucky,heal,tonic,bright}`, `goldSword`, `drain`, `sleepSword`, `reviveCharge`
- `status{barrier,invis,combo,speed,regen,bright,silent,...}`
- 永続（セーブ域）: `medals`, `collection{id:数}`, `unlocked{}`, `clearedDuns{}`, `stats{}`

### 3.3 レベルアップ `levelUp(n)`（L3753）
1Lvごと: `maxhp+1`, `atk+1`, `maxmp+1`, `def`は`_defFrac+=0.45`を蓄積し1超で`def+1`（約2.22Lvで+1）。レベルアップ時に`hp+=2n`, `mp+=2n`回復。レン(mpHalf)は`maxmp=round(maxhp/2)`を常時同期。

### 3.4 経験値（撃破成長）`killMonster`内（L3699–3709）
敵の`cap[job.capIdx]`と自Lvの差 `diff = cap - lvl` で判定：

| diff | 成長確率 | 上昇Lv |
|---|---|---|
| ≥70 | 100% | +5 |
| ≥50 | 100% | +4 |
| ≥30 | 100% | +3 |
| ≥15 | 95% | +2 |
| ≥5 | 85% | +1〜2 |
| ≥1 | 55% | +1 |
| ≤0 | 0% | 0（同格以下では伸びない） |

→ **格上を倒すほど急成長**。`gain`は`min(gain, cap-lvl)`で上限cap到達まで。cap到達後はその敵を**確定1撃**で倒せる（§8.2の格下補正）。

### 3.5 装備（§Cardのequip系と連動）
- 剣(1スロ・上書き): `longsword`×1.3 / `greatsword`×1.6 / `goldsword`(格上撃破で配当+80%) / `drainsword`(与ダメ1/4吸収) / `sleepsword`(攻撃時に睡眠付与)
- 盾: `shield`→このランDEF+6
- 指輪(複数重複可・ラン持続): `ringMagic`(魔法×1.5/回復×1.25) / `ringLucky`(MP消費半減) / `ringHeal`(歩行毎+1HP) / `ringTonic`(部屋入室回復+5) / `ringBright`(常時視界最大) / `revive`(力尽き時1度だけ半HP復活)
- ポケット: `pocket1`(+1枠) / `pocket2`(+2枠)・最大12枠

### 3.6 Unity化指針
`JobData`(SO) と `PlayerStats`(クラス)。成長式・経験値テーブルは`ProgressionSystem`に集約し、cap差分テーブルはSO/設定ファイル外部化。装備は`EquipmentSlot`コンポーネント、指輪は`List<EquipmentData>`で重複保持。

---

## 4. カードシステム（§Card抽出・全45枚）

### 4.1 全カード定義表（`CARDS` L1440–1501）
レア度は `cost`で自動判定（§4.5）。装備MPはレア度比例（R6/SR10/SSR14/UR18/LR18, `EQUIP_MP` L1503）。

| id | 名前 | cat | cost | mp | 効果 | 主要フィールド |
|---|---|---|---|---|---|---|
| pot20 | ポーション20 | heal | 8 | 4 | HP20回復 | heal:20 |
| pot40 | ポーション40 | heal | 14 | 7 | HP40回復 | heal:40 |
| pot60 | ポーション60 | heal | 19 | 9 | HP60回復 | heal:60 |
| pot80 | ポーション80 | heal | 24 | 12 | HP80回復 | heal:80 |
| potMax | ポーションMAX | heal | 40 | 18 | HP全回復 | healMax |
| mpot | マジックポーション | heal | 20 | 0 | MP30回復 | mpRestore:30 |
| heal100 | ヒール100 | heal | 18 | 8 | 歩行毎HP+1（計100） | regen:100 |
| fire | ファイア | atk | 12 | 6 | 単体に魔法18 | mag:18 |
| thunder | サンダー | atk | 20 | 12 | 単体に魔法34 | mag:34 |
| meteo | メテオ | atk | 34 | 20 | 単体に魔法60 | mag:60 |
| mfire | マルチファイア | atk | 26 | 12 | 視界内全敵18 | mag:18, multi |
| mthunder | マルチサンダー | atk | 40 | 22 | 視界内全敵34 | mag:34, multi |
| mmeteo | マルチメテオ | atk | 60 | 34 | 視界内全敵60 | mag:60, multi |
| spear | スピア | atk | 22 | 10 | 直線貫通40 | spear:40 |
| poison | ポイズン | atk | 15 | 7 | 単体に毒（5ダメ×8T） | poison:8 |
| lock | ロック | sup | 16 | 8 | 視界内全敵を5T停止 | lockAll:5 |
| sleep | スリープ | sup | 14 | 6 | 単体6T睡眠 | sleep:6 |
| slow | スロー | sup | 14 | 6 | 単体8T鈍足 | slow:8 |
| panic | パニック | sup | 16 | 8 | 視界内全敵6T混乱 | panic:6 |
| storm | マルチストーム | sup | 24 | 10 | 全敵を吹飛ばし5ダメ | storm:5 |
| charm | チャーム | sup | 20 | 12 | 単体を味方化（8T） | charm:8 |
| bright | ブライト | sup | 12 | 5 | このフロア視界MAX | bright |
| map | マップ | sup | 10 | 4 | フロア全体表示 | map |
| search | サーチ | sup | 10 | 4 | 敵位置表示 | search |
| silent | サイレント | sup | 22 | 12 | このフロア湧き停止 | silent |
| teleport | テレポート | sup | 14 | 8 | フロア内ランダム移動 | teleport |
| escape | エスケープ | sup | 16 | **0** | 即脱出（配当・手札保持） | escape |
| barrier | バリア | buff | 30 | 16 | 10回ダメージ無効 | barrier:10 |
| invis | インビシブル | buff | 24 | 12 | 10T透明（敵に無視される） | invis:10 |
| combo | コンボ | buff | 26 | 14 | 3回連続行動 | combo:3 |
| hyper | ハイパーコンボ | buff | 50 | 30 | 10回連続行動 | combo:10 |
| speed | スピードアップ | buff | 14 | 6 | 10T移動2倍 | speed:10 |
| glow | グローアップ | buff | 16 | 8 | ATK/DEF/HP小+ & 微レベルup | glow |
| power | パワーアップ | buff | 34 | 18 | ATK/DEF/HP大+ & レベルup | power |
| hpup | HPアップ | buff | 18 | 8 | 最大HP+10（永続） | hpup:10 |
| death | デス | atk | 45 | 25 | 単体即死（代償Lv/ATK低下） | death, 代償 |
| longsword | ロングソード | equip | 50 | 6* | 通常攻撃+30% | swordMul:1.3 |
| greatsword | グレートソード | equip | 90 | 18* | 通常攻撃+60% | swordMul:1.6 |
| goldsword | ゴールドソード | equip | 70 | 10* | 格上撃破で配当+80% | goldSword |
| drainsword | ドレインソード | equip | 80 | 14* | 与ダメ1/4吸収 | drain |
| sleepsword | スリープソード | equip | 60 | 10* | 攻撃時たまに睡眠付与 | sleepSword |
| shield | シールド | equip | 60 | 10* | このランDEF+6 | shieldDef:6 |
| pocket1 | ポケット+1 | equip | 40 | 6* | 手札枠+1 | pocket:1 |
| pocket2 | ポケット+2 | equip | 70 | 10* | 手札枠+2 | pocket:2 |
| revive | リバイブリング | equip | 80 | 18* | 力尽き時1度だけ半HP復活 | ring:revive |
| ringMagic | 魔導の指輪 | equip | 70 | 18* | 魔法1.5倍/回復1.25倍 | ring:magic |
| ringLucky | 幸運の指輪 | equip | 70 | 18* | MP消費半減 | ring:lucky |
| ringHeal | 癒しの指輪 | equip | 60 | 14* | 歩行毎HP+1 | ring:heal |
| ringTonic | 強壮の指輪 | equip | 55 | 14* | 部屋入室回復+5 | ring:tonic |
| ringBright | 視界の指輪 | equip | 45 | 6* | 常時視界MAX | ring:bright |
| **bloodfire** | ブラッドファイア | atk | 0◆ | 10 | 単体魔法95・代償maxHP-8 | forbidden, costMaxHp:8 |
| **soulreturn** | ソウルリターン | heal | 0◆ | 0 | HP/MP全回復・代償maxHP-12 | forbidden, costMaxHp:12 |
| **memeater** | 記憶喰らい | atk | 0◆ | 18 | 視界内全敵60・記憶片+1 | forbidden, multi, gainShard |
| **orbcall** | オーブコール | atk | 0◆◆ | 30 | 全敵110・自傷25 | forbidden(LR), selfDmg:25 |

`*` 装備MPはレア度で自動設定。`◆`禁忌＝ショップ非売、記憶片(shard)購入のみ。禁忌使用は**TRUEエンドを遠ざける**（§10.4）。

### 4.2 カテゴリ(cat)の意味
- `heal` 回復系（ポーション/MP回復/リジェネ）
- `atk` 攻撃魔法（単体/範囲multi/貫通spear/毒/即死）
- `sup` 補助・制御（状態異常/視界/脱出/テレポート/湧き停止）
- `buff` 自己強化（バリア/透明/連続行動/加速/ステ上昇）
- `equip` 装備（剣/盾/指輪/ポケット）

### 4.3 効果実行 `resolveCard(i, target)`（L4300–4415）
1. MP充足判定: `need = mpCost(c)`。不足なら失敗（消費なし）。
2. 効果適用（cat別、下記）。
3. 消費が成立したら: 手札から除去・MP減算・キャストFX・**敵フェーズ発生**・状態tick。

主要計算:
- **魔法ダメージ** `dmg = round(c.mag × job.magBonus × (ringMagic?1.5:1))`。`multi`なら視界内全敵に同値。
- **貫通** `spear`：最寄り敵方向へ直線連鎖ダメージ。
- **即死** `death`：target.hp=0。代償で自Lv-8相当・maxHP-16・ATK-6（要ソース確認）。
- **回復** `heal`/`healMax`/`mpRestore`/`regen`(歩行毎+1)/`hpup`(maxHP+10永続)。
- **制御**: `lockAll`→視界内全敵lock5T、`sleep/slow`→単体、`panic`→視界内全敵、`charm`→ボス以外を味方化（`allies`へ移動）、`teleport`/`map`/`search`/`silent`/`bright`/`storm`。
- **バフ**: `barrier`(回数), `invis`(T), `combo`(連続行動回数), `speed`(移動2倍), `glow/power`(ステ加算＋微レベルup)。
- **装備**: 剣は`clearSwordSlot()`で前効果リセット→新効果。指輪は`rings[name]=true`で重複保持。

### 4.4 MPシステム
- `mpCost(c) = ringLucky装備時 max(1, floor(mp/2)) : mp`（L4196）。装備カードは原則MP0だがレア度で自動設定。
- MP回復: 敵フェーズ毎`+1`、`mpot`で+30、`soulreturn`で全回復、`ringTonic`部屋入室。
- **カード→MP変換**（GBA式無料アクション L4238）: `gain = max(2, round(cost×0.5))`。不要カードをその場でMP化。

### 4.5 レア度判定（`cardRarity` L1512）
`forbidden`→UR（orbcallのみLR）。それ以外は cost で: `≥70→UR`, `≥44→SSR`, `≥24→SR`, `<24→R`。

### 4.6 デッキ構築
- 出撃前に`bag`へカードを詰める（上限=ダンジョンの`bagLimit`、既定10）。出撃で`hand`へコピー、超過分は`collection`へ返却。
- **おまかせ編成** `fillStarterBag`（L2170）: 職業別優先配列を上限まで自動投入。例: mage=`[mfire,fire,mpot,bright,lock,pot40,pot20,escape,power,pot80]`。
- 手札枠が満杯で新カード入手時は「交換 or 破棄」モーダル。
- **クリア/脱出で手札→collectionへ銀行化**。**死亡で手札消失**（gameOverで`hand=[]`）＝ローグライクのリスク。

### 4.7 カード入手 `rollCardDrop`（L1541）
```
deep   = floor + max(0, star-2)          // 難ダンジョンほど深さ底上げ
maxCost= 20 + deep×13                     // 解禁コスト上限
weight = 170/(cost+12)                    // 安いほど高頻度（強カードは稀）
        × (cat==heal ? 2.2 : 1)
        × (1 + min(1.4, deep×0.05)×max(0, cost-26)/26)  // 強カードは深く難い時だけ増
```
床の宝石(≈45%)/カード直落ち、敵撃破12%でドロップ。禁忌は記憶片購入のみ。

### 4.8 Unity化指針
`CardData`(SO) + `ICardEffect.Execute(player, target, ctx)`。catごとに派生（AttackEffect/HealEffect/ControlEffect/BuffEffect/EquipEffect）。ドロップは`CardDropTable(dungeon, floor, star)`で累積分布抽選。手札/コレクションは`CardDeck`クラス。

---

## 5. モンスター & 状態異常（§Monster抽出・全38体）

### 5.1 全モンスター表（`MONSTERS` L1259–1292）
`pay`=強さ/価値の目安（配当・最低保証ダメージの基準）。`cap`=職業別育成上限Lv（3列）。`role`=AI種別。

| 名前 | HP | ATK | DEF | role | pay | cap(目安) | 備考 |
|---|---|---|---|---|---|---|---|
| プーニャ緑 | 22 | 5 | 1 | melee | 1 | 16 | 最弱・初級 |
| プーニャ黄 | 26 | 6 | 1 | melee | 1 | 20 | 初級 |
| リザード | 34 | 7 | 2 | revive | 1 | 28 | 倒すと死骸→2T後蘇生 |
| キラービー | 42 | 8 | 2 | swarm | 2 | 36 | 群れ |
| ホッピー | 30 | 6 | 1 | coward | 2 | 24 | 逃げ |
| キラーホッピー | 46 | 9 | 2 | lunge | 3 | 40 | 1〜2マス飛びかかり |
| スケルトン | 52 | 10 | 3 | melee | 2 | 48 | 中級 |
| ケムンパ | 60 | 11 | 3 | melee | 3 | 56 | 中級 |
| プリースト | 58 | 8 | 3 | healer | 3 | 54 | 味方回復 |
| スピット | 56 | 9 | 3 | split | 3 | 50 | 分裂 |
| ゴブリン | 66 | 12 | 4 | melee | 4 | 64 | 中級後期 |
| バルチャー | 70 | 12 | 4 | knock | 4 | 66 | 吹き飛ばし |
| 魔道士 | 62 | 11 | 2 | ranged | 5 | 58 | 遠隔 |
| マザービー | 90 | 14 | 6 | summon | 6 | 92 | ビー召喚 |
| キラービー群 | 72 | 14 | 5 | swarm | 6 | 90 | 群れ・上級 |
| ベノム | 74 | 13 | 5 | poison | 5 | 74 | 毒床を残す |
| グランシャーク | 92 | 17 | 7 | melee | 7 | 96 | 上級 |
| リスタール | 120 | 19 | 10 | magicimmune | 7 | 128 | 魔法無効 |
| ターミネーター | 104 | 19 | 9 | melee | 8 | 110 | 最上級 |
| ドラゴン | 128 | 24 | 10 | ranged | 10 | 136 | 遠隔・最上級 |
| 死神 | 148 | 29 | 10 | melee | 11 | 118 | 最上級 |
| ビショップ | 160 | 22 | 10 | healer | 11 | 122 | 回復役 |
| マッドブル | 178 | 31 | 12 | charge | 13 | 130 | 直線突進 |
| エビルアイ | 208 | 31 | 13 | ranged | 15 | 138 | 最終帯 |
| デーモン | 230 | 42 | 15 | melee | 18 | 144 | 最終帯 |
| リスタドラゴン | 218 | 35 | 20 | magicimmune | 20 | 140 | 魔法無効 |
| キングドラゴン | 248 | 50 | 20 | ranged | 25 | 148 | 最終帯 |
| カオス | 260 | 52 | 20 | melee | 28 | 150 | 最強級 |
| 幻影のオーブ | 2500 | 55 | 22 | ranged | 60 | 160 | 幻/ラスボス |
| ミミック | 115 | 20 | 7 | melee | 7 | 95 | 宝箱擬態 |
| レイス | 122 | 27 | 6 | knock | 9 | 112 | 吹き飛ばし |
| クリスタルゴーレム | 205 | 24 | 16 | magicimmune | 12 | 136 | 魔法無効 |
| （城主系×複数） | 可変 | 可変 | 可変 | melee | 8–50 | 155 | ワールド/ストーリーボス |

> cap列は職業3列の代表値。原作の`cap:[a,b,c]`を全件移植すること。城主・章ボスは個別定義。

### 5.2 AI役割(role)別の挙動（`enemyPhase` L3861–3969）
| role | 挙動 |
|---|---|
| melee | 隣接時のみ攻撃。未発見なら稀にうろつき。廊下のコーナーは安全。 |
| lunge | 距離1〜2＆開放地形で飛びかかり（1マスギャップ無視）。廊下では跳べない。 |
| charge | 開放地形＆距離2〜4＆直線クリアで最大3マス突進。終端隣接で**1.4倍**攻撃。 |
| ranged | 開放地形＆距離≤5＆直線クリアで遠隔射撃。物陰/廊下では隣接時のみ殴る。 |
| healer | 半径2の傷ついた敵を70%で+8回復。なければ後退。 |
| summon | 毎T25%で隣接空きにキラービー産出。 |
| swarm | 基本melee。複数湧きで圧。 |
| revive | 撃破で死骸→2〜3T後にHP半分で蘇生。踏んで破壊可。 |
| poison | 占有マスに毒床を残す（踏むと毒）。 |
| knock | 与ダメ0.85倍だが命中でプレイヤーを1マス吹き飛ばす。 |
| coward | 距離保持、距離≤2で時々接近。追われると反撃。 |
| split | 撃破時80%で隣接2体に分裂（子は分裂不可）。 |
| magicimmune | 魔法/毒/睡眠/スロー/パニック/チャーム/ロックを無効。 |

**索敵（aggro）** L3888: 視界内/同部屋/距離≤4/直線距離≤8で見通しあり→ロックオン。一度ロックすると追跡。`invis`付与で敵のaggroリセット→wander。

### 5.3 状態異常（敵・プレイヤー共通）
| 異常 | 効果 | 既定持続 | 対象 | 無効化 |
|---|---|---|---|---|
| poison | 毎T -5HP | 8T | 両方 | magicimmune |
| lock | 行動不可 | 5T | 敵(視界内全) | magicimmune |
| sleep | 行動不可 | 6T | 敵単体 | magicimmune |
| slow | 隔ターン行動（50%スキップ） | 8T | 敵単体 | magicimmune |
| panic | ランダムうろつき | 6T | 敵(視界内全) | magicimmune |
| charm | 味方化 | 8T | 敵単体（ボス不可） | magicimmune |
| barrier | ダメージ無効 | 10回 | 自分 | — |
| invis | 敵に無視される | 10T | 自分 | — |
| combo | 連続行動 | 3/10回 | 自分 | — |
| speed | 移動2倍 | 10T | 自分 | — |
| regen | 歩行毎+1HP | 累積100 | 自分 | — |
| bright/silent | 視界MAX/湧き停止 | ラン中 | 自分/フロア | — |

### 5.4 被ダメージ計算 `enemyAttack(m, ranged, mult)`（L4073–4112）
```
atkP   = round(m.atk × (mult||1))                 // chargeなど mult=1.4
defTot = G.def + G.shieldDef
baseMin= m.pay>=18?14 : m.pay>=11?10 : m.pay>=7?7 : m.pay>=3?4 : 2   // 最低保証
ownMin = MONSTERS[m].minDmg ?? baseMin
minG   = max(1, round(ownMin × floorFactor()))    // フロア倍率で底上げ
dmg    = max(minG, round(atkP - defTot) + rnd(-2..+2))
if role==knock: dmg ×= 0.85
被弾揺れ振幅 ampScale = dmg<=10?0.5 : dmg<=20?2/3 : 1.0
```
→ 防御を上げても**最低保証**は通る。終盤(floorFactor大)ほど保証値が上がり、防御だけでは耐えきれない設計。

### 5.5 Unity化指針
`MonsterData`(SO) + role別`EnemyBehavior`（Strategyパターン or Behavior Tree）。`StatusEffectManager`は敵/自共用で`Tick()`管理、magicimmuneは付与時に弾く。被ダメは`DamageCalculator.EnemyToPlayer(enemy, player, floorFactor)`に集約。knockは`KnockbackSystem`へ委譲。

---

## 6. ダンジョン & プロシージャル生成（§Dungeon抽出・全11）

### 6.1 全ダンジョン表（`DUNGEONS` L1359–1414）
| id | 名前 | star | floors | bet | win | size | rooms | dense | spawn | tex/備考 |
|---|---|---|---|---|---|---|---|---|---|---|
| tutorial | 操作の間 | 0 | 1 | 0 | 30 | 9 | 4 | 0.05 | 0 | プーニャ緑のみ・チュートリアル |
| hajimete | はじめての穴 | 1 | 3 | 3 | 40 | 11 | 4 | 0.08 | 0.02 | 入門 |
| easy | やさしい谷 | 1 | 3 | 5 | 60 | 11 | 4 | 0.10 | 0.04 | 弱敵 |
| kiraku | きらくな小径 | 2 | 5 | 7 | 90 | 12 | 5 | 0.12 | 0.05 | 段階上昇 |
| futsuu | ふつうの坑道 | 2 | 5 | 10 | 160 | 13 | 5 | 0.13 | 0.06 | 標準 |
| fun | うれしい森 | 3 | 4 | 12 | 140 | 13 | 5 | 0.13 | 0.06 | 毒ステージ寄り |
| tegowai | てごわい岩窟 | 4 | 5 | 15 | 320 | 14 | 7 | 0.16 | 0.09 | 部屋多 |
| fierce | はげしい洞窟 | 5 | 4 | 18 | 260 | 13 | 6 | 0.18 | 0.16 | 急加速・高密度 |
| hard | 難しい遺跡 | 6 | 7 | 25 | 400 | 15 | 7 | 0.16 | 0.10 | 最長7F |
| maboroshi | 幻 | 7 | 8 | 0 | 1200 | 15 | 8 | 0.14 | hidden/持込5枚/一度きり消滅/tex=space |
| ultimate | 究極の門 | 8→9 | 9→30 | 40→60 | 800→2400 | 15→17 | 7 | 0.18 | 0.13 | 30F自動生成(`deepenUltimate`) |

各ダンジョンは`bands[floor][]`に敵名配列を持ち、フロアごとの出現帯を定義（最後の敵が最強）。`maboroshi`は25%で帰還時に出現し一度クリアで消失。

### 6.2 マップ生成 `buildFloor`（L2237–2434）
- グリッド: `W=H=size+4`（外枠は壁）。論理タイル（描画はTW108×TILE100px）。**8近傍チェビシェフ移動**。
- 部屋: 最大120回試行、3〜5×3〜4、部屋間gap≥2、上限`d.rooms`。
- 通路: 前室→後室をL字（横→縦/縦→横を50%）。**2×2の幅広通路を禁止**（`wouldWiden`）し1マス幅維持。
- スタート=rooms[0]中心。ゴール=最終Fは**クリスタル(gem)**、通常Fは**下り階段**。
- アイテム: フロア1〜3個、45%宝石(`val=(win/floors)×(0.3+rand0.5)`)/55%カード(`rollCardDrop`)。
- 敵配置: `target=max(2, round(部屋面積×dense×0.9))`。スタート3マス以内回避。
- **階段守護者**(30%): 下り階段隣にHP1.5倍・pay×3の強敵。**最終F守護者**: クリスタル上にHP1.8倍（倒すまでクリスタル出現せず）。章/城ボスは最終F専用。

### 6.3 難易度スケール
- `floorFactor()`（L2440・**現行: 指数1.4の凸カーブ**）:
  ```
  p     = (floor-1)/(floors-1)            // 0..1
  startF= 0.85                            // 1Fは弱め
  endF  = 1.30 + 0.11×min(star,9)         // 最終Fピーク（star依存）
  factor= startF + (endF-startF)×p^1.4    // 序盤やさしく・終盤に手ごたえ
  ```
  敵HP/ATK・被ダメ保証値に乗算。例: easy(★1)endF≈1.41 / ultimate(★8)endF≈2.18。
  > 2026-06-18にカーブ指数を0.78→1.4へ修正（旧版は前のめりで序盤に難度集中していた。ピークは不変）。
- `floorBand()`（L2450）: `cutoff = maxPay×(0.12+0.48×prog)` で深層ほど弱敵を除外。

### 6.4 床ギミック（`gimProfile`/`scatterGimmicks` L2465–2538）
| 種類 | 効果 | ダメージ | 配置 |
|---|---|---|---|
| 毒沼 poison | 歩行で毒/直接ダメージ | 1 | 部屋奥のみ |
| 溶岩 fire | 歩行ダメージ | 2 | 部屋奥のみ |
| 呪印 curse | 歩行ダメージ＋画面暗転 | 2 | 部屋奥のみ |
| 氷 ice | 同方向に自動スライド（壁/敵/階段で停止） | 0 | 通路も可 |

出現傾向はダンジョン別（例: fun=毒0.6中心 / fierce=火0.5・毒0.4 / ultimate=火0.6・氷0.5）。パッチ数`min(7, round(1+star×0.6))`。障害物（rock/pillar/mstone/crystal）はstar依存で華やかさが上がり、配置後にBFSで全床到達可能を保証（詰み防止）。

### 6.5 究極の門 30F自動生成 `deepenUltimate`（L1417–1435）
19種敵の難易度順配列に対し`center=floor((19-3)×(f/29))`のスライディングウィンドウで各Fの4連敵帯を生成。序盤ゴブリン級→終盤カオス級へ自動段階化。

### 6.6 ショップ部屋/スロットコーナー配置
- ショップ(≈42%・最終F以外): 部屋上行に屋台3個を2マス間隔で並列。各屋台独立在庫。
- スロット(≈30%・最終F以外): 部屋上行に台3個（item/gambler/monster種をローテーション）。

### 6.7 Unity化指針
`DungeonGenerator`（Seed確定で再現可能）。部屋生成はStrategy差し替え可能に。`floorFactor`/`floorBand`は`AnimationCurve`/設定で外部化。床ギミックは`TileEffect`コンポーネント＋ParticleSystem。生成Seed＋Band履歴をセーブし深層再突入を完全再現。

---

## 7. 経済・ショップ・スロット・報酬（§Economy抽出）

### 7.1 通貨の流れ
- **medals**（永続資産・初期2000）: BET支払い・ショップ購入に使う。
- **loot**（ラン内配当・初期0）: 敵撃破/宝石で加算。**クリア時に`medals += win + loot`** で統合。**敗北時は stat&BET損失**（loot獲得なし）。
- **gem**（宝石ドロップ）: 拾うと`loot`へ加算。`val=(win/floors)×(0.3+rand0.5)`。
- BET/WINはダンジョン定義（§6.1）。

### 7.2 撃破配当（`killMonster` L3710–3718）
```
基本配当 = max(1, round(pay × (0.4 + 0.12×max(0, floor-1))))   // 深追い配当
goldsword: + max(1, round(pay×0.8))（格上撃破時）
gambler職: + pay（30%確率）
```
F1=pay×0.4、F7=pay×1.12。深く潜るほどリターン増（リスク＆リターン）。

### 7.3 レア度体系
cost自動判定（§4.5）。装備MPはR6/SR10/SSR14/UR18。**高レアほどMPを食う**＝手札枠とMPの取捨選択ジレンマ。

### 7.4 ショップ（街＆ダンジョン内売店）
- 在庫`rollShopStock`(L1841): ランダム10種×1枚。重み`weight=120/(cost+8)×(heal?1.5:1)`（安いほど高頻度）。
- 売切れ表示(グレー)、購入確認モーダル、残高チェック、手札/コレクション振り分け。
- 街=グローバル在庫、ダンジョン店=タイル固有在庫。
- 店主セリフ`shopkeeperLine`が在庫レア度・所持装備・手札数で4分岐。

### 7.5 スロット（カジノ）
- 台種と掛金倍率`SLOT_BET`: item×1 / gambler×2 / monster(卵)×3。`bet = line × mult × SLOT_BET`。最大5line×3mult×3=45メダル/回。
- 絵柄`SLOT_SYMS`: 💎Diamond(wild500) / ❤️Ruby200 / 🔷Aqua120 / 🔵Sapphire80 / ⚪Pearl40 / 🟡Topaz30 / ⚔️Sword20 / 💗Heart20 / 🥚Egg(モンスター) / ⭐Special(禁忌) / ❓(8)。
- 抽選`slotDecide`(L1945): Diamond1.5% / gamblerはSPECIAL6%（禁忌カード）/ monsterは卵13%。**卵の45%はUR級**、残りSSR/UR。
- テンパイ演出（中リール確定後CHANCE表示）、SSR以上で`sfxRareGet`＋大当たり演出。お姉さんセリフ`slotLadyLine`が所持メダル/手札で分岐。

### 7.6 レア取得演出
`rareGetFx`(SR以上)＋`sfxRareGet`（高音）＋画面フラッシュ＋「✨大当たり！✨」大賞賛。**「この一枚は違う」を瞬時に伝える**。

### 7.7 Unity化指針
`EconomyManager`（loot/medals2層・死亡で喪失演出）。`ShopSystem`（重み抽選・在庫スコープ別）。`SlotSystem`（台別確率・卵UR45%）。レア演出は`GameFeelDirector`へ集約。

---

## 8. 戦闘解決 & 手応え演出（§Feel抽出）— **本作の生命線**

### 8.1 プレイヤー攻撃 `doPlayerAttack`（L3590–3674）
```
base = (G.atk + G.swordAtk) × (G.swordMul||1)
crit確率 = job.crit ?? (job.luck?0.18:0.12)
dmg  = max(1, round(base - 敵def + rnd(-2..+2))) × (crit?1.5:1.0)
格下補正: lvl≥敵cap かつ 非エリート なら dmg=敵HP（確定1撃）
```
斬撃は5種ランダム（arc/thrust/sweep/double/chop）。命中の瞬間`mon._hit`を仕込み敵がけぞり振動を開始。`G._eDelay = 120 + 振動持続 + 80`で「揺れ収束後に敵が動く」。

### 8.2 撃破 `killMonster`（L3699–3752）
撃破ヒットストップ＆揺れ→仲間カード化12%→revive/split処理→経験値（§3.4）→配当（§7.2）→死亡演出(fxDie/killflash)→敵リスト削除。

### 8.3 手応えパラメータ表（Unity再現用）
**ヒットストップ**（実時間ベース。`now()`はHS中フリーズ）:
| 場面 | ms |
|---|---|
| 命中・通常 | 120 |
| 命中・会心 | 260 |
| 撃破・ボス | 320 |
| 撃破・雑魚 | 150 |
| 被弾・近接 | 160 |
| 被弾・遠隔着弾 | 280 |
| 味方被弾 | 120 |

**画面揺れ**（`G._shake`・実時間`_rawNow`基準・2段階「タメ→余韻」L3031）:
```
hold  = max(0, (amp-0.5)×30)      // amp0.5→0 / amp1→15 / amp2→45 ms
decay = 200 + amp×80              // amp0.5→240 / amp1→280 / amp2→360 ms
k     = se<hold ? 1 : 1-(se-hold)/decay
offset= rand(-1..1) × amp × 5 × k
```
amp: 通常攻撃0.5 / 会心1.0 / 雑魚撃破1.0 / **ボス撃破2.0**（±10px）/ 被弾0.5。

**斬撃FX5種**: fxSlash(会心520/通常380ms) / fxThrust(460/340) / fxRing(500/400) / fxImpact(180) / fxMagic(fire460/thunder300/meteor520)。

**被弾リアクション** `hitOff`（L3269）: 「強いゴムを押し込んで離した減衰振動」モデル。横ノックバック＋8.4回往復の指数減衰＋白フラッシュ＋scaleX/Y伸縮（回転なし）。持続`dur = min(1.10, 0.40+0.40×max(0,mag-0.6))`秒。

**ダメージ数字** `popText`: 会心=大・金(760ms) / 通常=白(620ms) / 被弾=赤 / 回復=青緑。

### 8.4 防御
- `barrier`（回数制ダメージ無効）、`shieldDef`（盾でDEF+6）、`invis`（敵に無視される）。
- 状態異常付与は確率（sleepsword等）。

### 8.5 Unity化指針
- **ヒットストップ**: `Time.timeScale=0`ではなく**ゲーム内時計を凍結**。SE/揺れ/FXは`Time.realtimeSinceStartup`で駆動（HS中も鳴る・揺れる）。DOTweenは`SetUpdate(true)`(unscaled)。
- **画面揺れ**: Cinemachine Impulse か直接カメラOffset。実時間入力でtimeScale非依存。
- **被弾**: 表示用TransformにhitOff出力を毎フレーム適用（論理座標は不変）。
- **斬撃FX**: ParticleSystem5種、会心で色(白→金)＆持続を変更。
- **SE**: realtimeグループで再生し、HS中も実タイミングで鳴らす。

---

## 9. ゲームモード & 進行（§Player/§Narrative抽出）

| モード | 特徴 | 起点 |
|---|---|---|
| ストーリー | 職業・ダンジョン固定（レン）。章ボス・VN・分岐エンド。2部構成。 | `startStory`→`enterChapter(i)` |
| フリープレイ | 全職業/解放ダンジョン選択・自由デッキ。 | `openSetup`→`startRun` |
| ワールド | 主人公で城攻略・職業解放。配当0、制覇でWIN。 | `G.worldMode` |

**フリー解放順** `FREE_ORDER = [hajimete, easy, kiraku, futsuu, fun, fierce, tegowai, hard, ultimate]`（最初2つ常時解放、以降クリアで次解放）。

**勝敗**: クリア=最終Fクリスタル到達（守護者/ボス撃破後に出現）→`medals += win+loot`。敗北=HP0（`reviveCharge`があれば1度だけ半HP復活）。**エスケープ**で配当・手札を持って撤退可能。

---

## 10. ストーリー & VN（§Narrative抽出）

### 10.1 オープニング（`playOpening`/`OP_SCENES` L6946–7069）
11シーンの映像OP（門の伝説→3つの力（富/癒/命）→封印→封印解除→少年と不治の病の妹→タイトル「MIRAGE GATE / 封印カードの迷宮」）。**既読スキップ**: `localStorage 'mg_op_seen'`が立つと2回目以降は即タイトル。低音ドローン＋FF風アルペジオ。

### 10.2 VN会話（`playCutscene` L6209）
- データ: `[who, text, expr?, opts?]` または `{who, text, face, effect:'shake'|'tremor'|'red'|'slow'|'silent'|'noflow', choices}`。
- 立ち絵差分・表情・キャラ別ボイス、タイプ表示（句読点でウェイト）、早送りFF・スキップ、会話ログ`mg_log`（★重要台詞マーク）。
- **序章後回し** `_introPending`(L6464): 第1部は序章会話を保存していきなり第1章へ（先に操作を学ばせる）→第1章クリア直後に序章再生。
- 選択肢→`mg_flags`にフラグ記録→分岐。

### 10.3 章構成
**第1部（本編・9章+終章, L5750）**: 1はじまりの願い(hajimete)→2悲しみ売り(kiraku)→3明日を手放した子(futsuu)→4白紙の騎士(fun)→5まどろみの聖女(fierce)→6閉じられぬ門(tegowai)→7奪われた未来(hard・ミアの病の真実/レン=封印型判明)→8喪失の幻影(ultimate)→終章: 幻影のオーブ(3択エンド)。
**第2部（探索者試験編・10章+終章, L5895）**: e1ゴブリン試験→…→e8封印騎士アルゼン(記憶完全復帰)→e9幻のダンジョン(グラム/持込5枚)→e10団長レオン→終章: 幻影のオーブ(3択)。第1部クリアで解放。
登場キャラ: hero(レン)/pico/lily/mia/noa/garm/vail（第1部）、kyle/mel/sigma/crow/arzen/leon/iris/gram/setc/riine（第2部）。

### 10.4 エンディング分岐（`endingChoice` L6530）
3択（奇跡使用/破壊/書き換え＝真エンド）。**真エンドは禁忌カード(`forbidden`)を一度も使っていない場合のみ**（`forbiddenEver`/`forbiddenUsed`で判定）。第2部も同型（解放/破壊/カードホルダー）。

### 10.5 Unity化指針
`DialogueManager`（行データJSON駆動・立ち絵/表情/ボイス/選択肢を一元制御）。OPは`SceneSequence`＋既読フラグ。分岐は`StoryFlags`（禁忌使用履歴含む）。

---

## 11. チュートリアル（§Narrative抽出）

`startTutorial`(L6833): `dungeon=tutorial`・`bag=[pot20,fire]`で開始。1F・最弱プーニャのみ・守護者なし。ヒント発火順（`tutTick` L3450）: move→look→atk→kill→card→goal（各最低1.4秒間隔）。クリアで専用モーダル→モード選択へ。**第1章でも継続ヒント**（監修#3で`G.tut`再初期化、ノア/ガルム/リリィが「先生」役）。

Unity: 条件リスト（移動した/敵を見た/カード使用 等）でフェーズ遷移するトースト方式。進度を保存し「前から再開」可。

---

## 12. セーブシステム（§Narrative抽出）

| localStorageキー | 内容 |
|---|---|
| `mg_save` | medals, collection, clearedDuns, monCards 等 |
| `mg_story` | 章進行(chapter/cleared)・2部・storyJob・memShards・bossCards・titles・**forbiddenEver**・ver |
| `mg_world` | ワールド進行 |
| `mg_log` | 会話ログ |
| `mg_flags` | 会話フラグ |
| `mg_opt` | bgmVol, seVol, textSpeed, reduceFx |
| `mg_op_seen` | OP既読 '1' |
| `mg_slot` | 選択スロット 1/2/3 |
| `*_2`, `*_3` | スロット2/3（接尾辞） |

全`setItem`をtry-catchでラップ、parse失敗時はnull→スターターコレクション付与（初期: pot20×2, pot40×2, pot80, fire×2, mfire, bright, power, escape, lock, mpot＋medals2000）。3スロット×（本編/2部）の2次元管理。

Unity: `SaveManager`でJSON（スロット×キャンペーンの2次元）。ネストを平坦化して破損リスク低減。設定/字幕は外部化しローカライズ容易に。

---

## 13. 実装ロードマップ（推奨マイルストーン）

1. **M1 コア基盤**: グリッド・ターン制移動・視界fog・`DungeonGenerator`（1ダンジョンSeed再現）。SO基盤（Job/Card/Monster/Dungeon）。
2. **M2 戦闘ロジック**: 通常攻撃・被ダメ計算・経験値/レベル・敵role AI（melee/ranged/charge/lunge）。**Visual Onlyを最初から徹底**。
3. **M3 カード&状態異常**: `CardEffectExecutor`・MP・手札/デッキ・`StatusEffectManager`・カードドロップ抽選。
4. **M4 経済**: medals/loot/gem・ショップ・スロット・レア度・報酬演出。
5. **M5 手応え演出**: ヒットストップ・画面揺れ(タメ→余韻)・斬撃FX5種・被弾けぞり・ダメージ数字・SE（§8パラメータ表を厳密移植）。**ここが品質の分かれ目**。
6. **M6 進行/モード**: フリー/ストーリー/ワールド・解放・撤退・リトライ。
7. **M7 物語**: VN会話・OP・章・分岐エンド（禁忌判定）・チュートリアル。
8. **M8 セーブ&設定**: 3スロット・オプション・既読・QA一周（押せるボタン/画面内/ソフトロック無/エラー0）。

---

## 14. リメイク時の設計判断メモ（桜井流・プレイヤーファースト）

- **足すより削る・待たせない・出戻りを消す**（監修レポートの結論）。原作の最大課題は「盛り込みすぎ＋導入の長さ」。リメイクは機能の再現に留まらず、**導入を短く・即リトライ・無反応撲滅**を初期設計に組み込む。
- **手応えは情報伝達**: ヒットストップ・揺れ・けぞりは「効いた」を伝えるためのもの。§8の数値は体験の核なので安易に変えない。雑魚撃破・命中・被弾の振幅は原作の手触りを尊重、ボスだけ派手(amp2.0)。
- **難易度カーブ**: 序盤やさしく・終盤に手ごたえ（floorFactor指数1.4・ピーク不変）。Unityでも`AnimationCurve`で可視化しチューニングできるように。
- **リスク＆リターン**: 深追い配当・手札使い切り・BET損失・禁忌の代償（真エンド封印）が「悩む理由」を作っている。リメイクでもこの緊張を薄めない。
- **著作権**: KONAMI等の特定IPは不使用。素材は自作/AI生成で「伝わるか・気持ちいいか」を基準に。

---

## 15. 直近アップデートの反映（2026-06）— 挙動・AI・8方向アニメーション

> 本章は原作HTML(`monster_gate.html`)へ2026-06に加えた変更を、Unity移植向けにまとめたもの。既存各章（§2 ターン順、§4 カード、§5 モンスター/AI、§8 戦闘）の差分として読むこと。

### 15.1 挙動・バランスの変更（差分）
| 項目 | 変更内容 | 原作の場所 | Unity実装指針 |
|---|---|---|---|
| ボスはチャーム不可 | charm/誘惑カードを boss/guardian/orbBoss/城主/カオス に無効化（ボスをチャームしてクリスタルが出ず詰む事故の防止） | `resolveCard` charm分岐 | 対象選定の Validator で `IsBoss` を弾く。無効時はカード非消費＋失敗SE |
| 通路で斜め斬り禁止 | プレイヤーの**攻撃にも角抜け禁止**を適用（両隣の直交が壁なら斜めの敵を斬れない）。その場斬りも同様 | `tryAct`（攻撃分岐／dir5分岐） | 移動と攻撃で共通の `CanReachDiagonally(from,dir)` を使う。敵meleeは既に角を尊重 |
| 攻撃魔法の向き直線優先 | ファイヤー/メテオ/サンダーは**向いている直線上の敵**を最優先ターゲット（直線上に居なければ最寄りの可視敵） | `_useCardDo`＋`monsterOnFacingLine` | 単体mag解決時、まず facing ray を走査→ヒットを優先。fallback=最寄り可視 |
| ドレインソード弱体化 | 吸収 1/4→**1/8・1回最大4・HP満タン時は回復なし** | `doPlayerAttack` の drain分岐 | `heal = clamp(round(dmg*0.125),1,4)`、`hp<maxhp` ガード |

### 15.2 近接仲間AI（`allyAct`）— ※原作に新規明文化
チャーム中の味方／召喚モンスターの毎ターン行動。優先度順：
1. **隣接する敵が居れば攻撃**（撃破時は敵リストから除去）。
2. **近くに敵が居れば最優先で倒しに行く**：自分 or プレイヤーいずれかから **6マス(`ALLY_ENGAGE`)以内**の最寄り敵へ `stepToward`。
3. 敵が居なければ**プレイヤーの「後ろ」（進行方向の反対マス `px-dirX, py-dirY`）に追従**。隣接して張り付いている時は待機。
- **進路は塞がない**：プレイヤーが進む先に味方が居たら入れ替わって道を譲る（`tryAct`）。

```csharp
// Unity擬似コード（味方1体の1ターン）
if (TryAttackAdjacentEnemy(ally)) return;
var foe = NearestEnemyWithin(ally, player, ALLY_ENGAGE); // 6
if (foe != null) { StepToward(ally, foe.cell); return; }
StepToward(ally, BehindCell(player)); // 進行方向の反対
```
Unity化指針：敵/味方/プレイヤーの占有を1つの `OccupancyGrid` で持ち、`StepToward` は角抜け禁止＋占有チェックを共有。味方の「道を譲る」入れ替えは Player移動解決時に同セル味方をスワップ。

### 15.3 8方向スプライト・アニメーション仕様（**新規・本作の見た目の核**）
原作はヒーロー/主要モンスターを **ビュー×状態のスプライト** で持ち、`dirSprite(key, entity)` が向き(`_dirX/_dirY`)と状態から1枚を返す。

- **ビュー(5)**：`front / back / side / fdiag(斜め前) / bdiag(斜め後)`。横・斜めは**右向き基準で左は水平反転**。8方向＝front/back＋side±＋fdiag±＋bdiag±。
- **状態**：`idle / walk / hit / attack / atkwind` ＋ 追加した多コマ群（下記）。
- **命名**：`assets/t_d_<job>_<view>_<state>.png`（例 `t_d_warrior_front_atk2.png`）。

#### (a) 多コマ歩行 `walk1/walk2/walk3`
- 移動中、ビューに `walk1` があれば **[walk1→walk2→walk3→walk2] を120ms連続ループ**（idleを挟まない＝なめらか）。
- 全17プレイヤー職 × 全5ビューに生成済み。未生成キャラ（モンスター等）は従来の idle/walk 2コマトグルにフォールバック。

#### (b) 多コマ攻撃 `atk1/atk2/atk3`
- lunge(攻撃モーション)中の経過 `lt=(now-t0)/300` を **lt<0.34→atk1（振りかぶり）/ <0.67→atk2（振り抜き）/ else atk3（追い打ち）** に割当。
- 全17職 × 全5ビューに生成済み。未生成は `atkwind→attack→idle` にフォールバック。

#### (c) 装備モーション `raisemid/raise`（剣を掲げる／天高く掲げる）
- 装備カード使用中（`G._equipPose`）、ヒーロー状態を **経過<38%→raisemid / else raise** に固定（剣を頭上に掲げきり・金色発光）。本体の伸び上がり＋頭上の光るアイコン/光輪/きらめきを別途描画。
- 現状 **front のみ**生成（他ビューは実 `atkwind` にフォールバック）。レアカード(SR以上)使用時はカード拡大の賞賛演出を併発。

#### Unity実装指針（アニメーション）
- **Animator + BlendTree（2Dフリーフォーム）** で向きベクトル(`dirX,dirY`)→ビュー選択、`flipX` で左右反転。各状態を Animator State にし、walk/attack はクリップ（walk1-3 / atk1-3 をフレームに）。
- もしくは **Sprite Library + 命名規約 `t_d_{job}_{view}_{state}{frame}`** でスウォップ。状態優先度：`equip(raise) > hit > lunge(atk) > walk > idle`（原作 `dirSprite` の if 順に一致）。
- **フォールバック連鎖**を必ず実装：`raise→atkwind→attack→idle`、`atk*→atkwind→attack→idle`、`walk*→walk→idle`、斜め/横→縦成分ビュー→idle。これが無いと未生成キャラで欠落する。
- 透過PNG前提（生成物は白背景を flood-fill で透過済み）。

### 15.4 素材生成パイプライン（HF Kontext）— 量産レシピ
新規アニメスプライトは **既存の `*_idle` を入力に FLUX.1-Kontext でポーズだけ編集**（キャラ・装備・絵柄を保持＝逸脱なし）→白背景を透過に戻す、で量産した。
- 公開データセット：`takaokkkk/mirage-hero-anim`（`src/<job>_<view>.png` が入力、生成物 `t_d_<job>_<view>_<state>.png` ＋ 確認用 `*_sheet.png`/`*.gif`）。
- 生成スクリプト：`_hf_tools/`（`upload_hero_sprites.py`＝素材UP、Kontextバッチは VIEW/VIEWS をenvで切替）。
- 注意：同一データセットへ**多並列コミットは500を誘発**するため、並列は2程度＋アップロードはリトライ＋`atk3`存在で再開スキップ、が安定。
- Unityでは生成物をそのまま `Resources`/Addressables に格納し、命名規約で読む。新ビュー/新職の追加も同パイプラインで拡張可能。

---

## 付録A. 主要数式クイックリファレンス
```
floorFactor = 0.85 + (1.30+0.11×min(star,9) - 0.85) × ((floor-1)/(floors-1))^1.4
敵HP/ATK     = base × floorFactor
floorBand cutoff = maxPay × (0.12 + 0.48×prog)
プレイヤー与ダメ = max(1, round((atk+swordAtk)×swordMul - 敵def + rnd(-2..2))) × (crit?1.5:1)
被ダメ        = max(minG, round(敵atk×mult - (def+shieldDef)) + rnd(-2..2)) ; minG=round(ownMin×floorFactor)
撃破配当       = max(1, round(pay × (0.4 + 0.12×max(0, floor-1))))
宝石value      = round((win/floors) × (0.3 + rand×0.5))
カードドロップ重み = 170/(cost+12) × (heal?2.2:1) × (1 + min(1.4,deep×0.05)×max(0,cost-26)/26)
ショップ重み   = 120/(cost+8) × (heal?1.5:1)
スロット掛金   = line × mult × {item:1, gambler:2, monster:3}
カード→MP変換  = max(2, round(cost×0.5))
レア度        = forbidden?(orbcall?LR:UR) : cost≥70?UR : cost≥44?SSR : cost≥24?SR : R
装備MP        = {R:6, SR:10, SSR:14, UR:18, LR:18}
ドレイン吸収   = (hp<maxhp) ? clamp(round(dmg×0.125), 1, 4) : 0        // 2026-06 弱体化(旧 round(dmg×0.25))
斜め可否(移動/攻撃) = walkable(dst) && (dx*dy==0 || (notWall(x+dx,y) && notWall(x,y+dy)))  // 角抜け禁止＝攻撃にも適用
攻撃魔法ターゲット = monsterOnFacingLine() ?? nearestVisibleMonster()   // 向き直線上を優先
味方行動       = 隣接敵→攻撃 / 6マス内の最寄り敵→接近 / それ以外→プレイヤーの後ろ(px-dirX,py-dirY)に追従
歩行コマ       = moving && view∈{front,back,side,fdiag,bdiag} && walk1有 → [walk1,walk2,walk3,walk2]@120ms
攻撃コマ       = lunge中 lt<0.34?atk1 : lt<0.67?atk2 : atk3   (lt=(now-t0)/300)
装備掲げ       = equipPose中 経過<38%?raisemid : raise
```

## 付録B. 原作の主要関数と行番号（grep再特定推奨）
```
JOBS L1299 / MONSTERS L1259 / CARDS L1440 / DUNGEONS L1359 / EQUIP_MP L1503
floorFactor L2440 / floorBand L2450 / buildFloor L2237 / deepenUltimate L1417
rollCardDrop L1541 / fillStarterBag L2170 / resolveCard L4300 / mpCost L4196
doPlayerAttack L3590 / killMonster L3699 / levelUp L3753 / enemyPhase L3861 / enemyAttack L4073
hitstop/now L3247 / 画面揺れ描画 L3031
rollShopStock L1841 / showShop / showSlot / slotDecide L1945 / SLOT_BET / SLOT_SYMS L1921
playOpening L7069 / OP_SCENES L6946 / playCutscene L6209 / STORY L5750 / STORY2 L5895
enterChapter L6494 / endingChoice L6530 / startTutorial L6833 / tutTick L3450
saveGame L1591 / loadGame L1594 / セーブキー mg_save/mg_story/mg_world/mg_log/mg_flags/mg_opt/mg_op_seen/mg_slot
dirSprite（8方向・状態選択）/ DIR_SPRITES・dset.mk（idle/walk/hit/attack/atkwind/raise/raisemid/walk1-3/atk1-3）
allyAct（仲間AI・ALLY_ENGAGE=6）/ monsterOnFacingLine（向き直線ターゲット）/ tryAct（角抜け禁止の攻撃判定・dir5その場斬り）
cardCastFX・rareUseFx（レア使用演出）/ G._equipPose（掲げモーション state）/ canStep（角抜け禁止）
```

---
*本設計図は原作ソースの抽出に基づく。実装着手時は各値を原作で再grep確認し、隠し職・章ボス個別データ・幻影のオーブ戦など個別定義を全件移植すること。*
*2026-06-20の挙動・AI・8方向アニメーション変更は §15 に反映済み（数式は付録A末尾、関数は付録B末尾）。最新の現行挙動は別紙 `ミラージュゲート_詳細仕様書.md` を一次資料とする。*
