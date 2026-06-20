# ミラージュゲート — Unityリメイク雛形

[設計図](../ミラージュゲート_Unity設計図.md)に基づくUnityプロジェクトの骨組み。**データ層（ScriptableObject）は完全定義**、システム層は責務と数式（設計図§参照）を埋め込んだ**スタブ**。コンパイルが通る状態の足場で、TODOを埋めていけば動く。

## 使い方（Unityへの取り込み → ▶Playまで4ステップ）
1. Unity 2021 LTS 以降で空の2Dプロジェクトを作成し、この `Assets/MirageGate/` を `Assets/` 配下にコピー。
2. メニュー **`MirageGate ▸ Import Data ▸ ★ Import ALL`** … 全SOアセット＋`GameDatabase.asset`を自動生成。
3. メニュー **`MirageGate ▸ Setup Play Scene`** … 現在のシーンにGameController/各システム/カメラ/HUDを自動構築・自動結線。
4. **▶Play** … デバッグ出撃が始まる。**矢印/WASD＝移動（QEZC＝斜め）、1〜5＝カード使用**。敵に向かって移動で攻撃、金色マス（クリスタル）到達で次フロアへ。

> プロトタイプはアート素材不要（色分けスクエアで描画）。本素材は後で `SpriteRenderer.sprite` を差し替えるだけ（Visual Onlyなのでゲームロジックは不変）。
> 手動で組む場合：空シーンに `GameController` を置き Inspector で `GameDatabase` と各MonoBehaviour参照を割り当ててもよい。

## フォルダ構成
```
Assets/MirageGate/
├ Scripts/
│  ├ Core/        GameEnums.cs          … CardCategory/Rarity/MonsterRole/StatusType/GimmickType 等
│  ├ Data/        *Data.cs（SO定義）     … Job/Card/Monster/Dungeon/StatusEffect/StoryChapter/Dialogue/GameBalanceConfig
│  ├ Runtime/     状態クラス             … PlayerState/MonsterInstance/RunState/GridMap（ランごとの可変状態）
│  ├ Systems/     ロジック              … 下記
│  ├ GameController.cs                   … 全システムの結線（ブートストラップ）
│  └ MirageGate.asmdef
├ ScriptableObjects/   ← ここに実データのSOアセットを置く
│  ├ Jobs/ Cards/ Monsters/ Dungeons/ StatusEffects/ Campaigns/ Dialogues/
├ Scenes/ Prefabs/ Art/（Sprites,VFX）/ Audio/（BGM,SE）/
```

## システム層の責務（設計図§対応）
| クラス | 実装状況 | 役割 |
|---|---|---|
| `GameBalanceConfig`(SO) | ✅実装 | 全数式の定数・ヘルパ（FloorFactor等, 付録A） |
| `DamageCalculator` | ✅実装 | 与/被ダメージ・会心・難易度係数（§8.1/§5.4/§6.3） |
| `ProgressionSystem` | ✅実装 | レベルアップ・撃破経験値（§3.3/§3.4） |
| `EconomyManager` | ✅実装 | medals/loot・撃破配当・宝石・BET・変換（§7） |
| `CardDropTable` | ✅実装 | カードドロップ重み抽選（§4.7） |
| `ShopSystem` | ◐ 抽選実装/UI TODO | 在庫生成・購入・店主セリフ（§7.4） |
| `SlotSystem` | ◐ 骨組み | 掛金・抽選・卵UR45%（§7.5） |
| `SaveManager` | ◐ 実装/キー定義 | 3スロット×2部・PlayerPrefs（§12） |
| `CombatResolver` | ✅実装 | 攻撃〜撃破〜被弾の解決＋演出結線（§8） |
| `GameFeelDirector` | ✅実装 | **手応えの核**：ヒットストップ/画面揺れ/敵けぞり/プレイヤーけぞり/白フラッシュ/撃破フラッシュ/ダメージ数字/斬撃FX/SE連携（§8.3） |
| `SfxPlayer`(View) | ✅実装 | 効果音を波形実行時合成（命中/会心/撃破/被弾・音源ファイル不要） |
| `SlashFxPlayer`(View) | ✅実装 | 斬撃FX5種（arc/thrust/sweep/double/chop）をLineRendererでprocedural描画・会心は金色/長め（§8.1） |
| `TurnManager` | ✅実装 | 1ターン制ループ・移動/攻撃/カード・撃破スイープ（§2） |
| `DungeonGenerator` | ✅実装・検証済 | プロシージャル生成（§6.2・3200ラン連結/到達性検証済） |
| `MovementSystem` | ✅実装 | 移動・氷スライド・床ギミック（§2.2/§6.4） |
| `CardEffectExecutor` | ✅実装 | カード効果（攻撃/回復/状態/バフ/装備）（§4.3） |
| `EnemyAI` | ✅実装 | 敵フェーズ・role別挙動（melee/ranged/charge/lunge/healer等）（§5.2） |
| `StatusEffectManager` | ✅実装 | 状態異常の付与＋毎ターンtick（§5.3） |
| `DialogueManager`/`TutorialManager` | ○ スタブ | VN/チュートリアル（§10/§11） |
| `GameDatabase`(SO) | ✅実装 | 全SOの参照ハブ（id→データ） |
| `GameBoardView`(View) | ✅実装 | 盤面描画（スプライト実行時生成・アート不要） |
| `CameraRig`(View) | ✅実装 | プレイヤー追従＋画面揺れ適用（§8.5） |
| `GameInput`(View) | ✅実装 | キーボード8方向移動＋カード使用 |
| `Hud`(View) | ✅実装 | HP/MP/Lv/メダル/配当/手札＋ダメージ数字（OnGUI） |

## 実装状況（M1〜M2のゲームロジック ✅完了・C#外で検証済み）
グリッド生成・ターン制・移動・戦闘・敵AI・状態異常・カード効果のロジック層を実装。
**C#コンパイラが無い環境のため、同一アルゴリズムをPythonへ移植して検証済み**：
- ダンジョン生成：3,200ラン（8構成×400seed）で**全部屋連結・到達可能・孤立タイルゼロ（ソフトロック無）・敵が始点に湧かない**を確認。
- ターンループ統合：BFS自動プレイ各300試行で**詰み/タイムアウト0%**（移動と経路の統合が健全）、無限ループ・クラッシュ無し。
- 難易度カーブ妥当性：勝率が **★1=95% > ★2=52% > ★6=0%** と単調減少（floorFactor=1.4が機能）。
> ※最終的なUnityでのコンパイル確認・シーン/プレハブ/UI結線は実機（Unity Editor）で必要。ロジックは検証済みだがC#構文の最終確認はUnity取り込み時に。

## 次のステップ（推奨順 = 設計図§13）
- ✅ **M1〜M2ロジック＋提示層＋M5手応えの核** 完了：生成・ターン制・移動・戦闘・敵AI・状態異常・カード効果・描画・入力・カメラ・HUD・**ヒットストップ/画面揺れ/敵けぞり/撃破フラッシュ/ダメージ数字/合成SE**。色分けで遊べ、手応えも乗った状態。
1. ✅ **実コンパイル確認 完了**：Unity 6000.4.5f1 同梱Roslyn＋実UnityEngine.dllでビルド → ランタイム/エディタ両アセンブリ **errors=0 / warnings=0**。（検出・修正したバグ: GameController.cs と DataImporter.cs の using漏れ2件）。残るは `Import ALL`→`Setup Play Scene`→▶Play での実行時動作確認。
2. ✅ **手応えの仕上げ 完了**：斬撃FX5種（arc/thrust/sweep/double/chop）・プレイヤーけぞり・被弾白フラッシュを実装・実コンパイル検証済。（任意の追加：撃破の崩壊演出fxDie・本素材スプライトへの差し替え）
3. ✅ **アイテム/ショップ/スロット 完了**：床アイテム拾得（カード→手札／宝石→配当）、ショップタイル(≈42%)で購入UI、スロットタイル(≈30%)でスピンUI。生成時にカード中身(`CardDropTable`)・宝石額(`EconomyManager`)を確定。取引中は移動入力をブロック。実コンパイル検証済。
4. **視界(fog)・床ギミック演出**：`GameBoardView.useFog`を有効化し`computeVisibility`を実装。
5. **M6〜M8**：モード選択/物語(VN)/チュートリアル/セーブ＋QA一周。
6. **データ補完**：隠し職7種・章ボス個別・幻影のオーブ戦・bandsの原作照合・StatusEffectData/Dialogue。

## データ投入（CSV → SO 一括生成）✅実装済み
設計図の表を `Assets/MirageGate/EditorData/*.csv` に用意済み。エディタ拡張で全SOアセットを自動生成する。

### 手順
1. Unityでプロジェクトを開く（`Editor/` がコンパイルされる）。
2. メニュー **`MirageGate ▸ Import Data ▸ ★ Import ALL`** を実行。
3. `ScriptableObjects/{Monsters,Cards,Dungeons,Jobs}/` にアセットが生成され、`GameDatabase.asset` も自動構築される。
   - 個別取り込みは `① Monsters → ② Cards → ③ Dungeons → ④ Jobs` の順（DungeonsはMonster参照を解決するため後）。
   - 再実行すると id/名前で照合して**既存アセットを更新**（重複生成しない）。

### CSVの内容（投入済みデータ量）
| CSV | 件数 | 備考 |
|---|---|---|
| `monsters.csv` | 32体 | hp/atk/def/role/pay/cap。城主・章ボス個別は別途追加 |
| `cards.csv` | 54枚 | 効果は `effects` 列に `key:value;...` で記述→リフレクションで適用 |
| `dungeons.csv` | 11個 | `bands` はフロア`|`敵`,`区切り（**近似値・要原作照合**）。ultimateはautoDeepen |
| `jobs.csv` | 9職 | 基本3＋侍/竜騎士/聖騎士/魔導士/闇騎士/レン。隠し職7種は数値要確認のため未収録 |

### CSV編集のコツ
- カードに効果を足す：`effects` 列に `barrier:10` のように追記（フィールド名は `CardData` と一致）。bool系は値なしで `multi`。
- 列にカンマを含む値（dungeonsの`bands`等）は**ダブルクオートで囲む**。
- 行頭 `#` はコメント。
- 取込時、未知の効果フィールド名・未解決のモンスター名は Console に警告が出る（黙って欠落しない）。

### 残データ（手作業 or CSV追記）
- 隠し職7種、章ボス／城主の個別MonsterData、幻影のオーブ戦の詳細。
- `StatusEffectData`（13種）・`DialogueData`／`CampaignData`（物語）はCSV未対応。Inspectorで作るか、必要ならCSV対応を追加。

## 設計原則（厳守）
- **Visual Only**：演出は論理座標に干渉しない（衝突判定・AIがブレない）。
- **ヒットストップは時間凍結**（`Time.timeScale=0`にしない）。SE/揺れ/FXはunscaled時間で駆動。
- **足すより削る・待たせない・出戻りを消す**（桜井監修の結論）。
- **バランス定数は`GameBalanceConfig`に集約**し、コードに数値を散らさない。
