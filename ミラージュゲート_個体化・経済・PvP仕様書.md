# ミラージュゲート 個体化・経済・PvP 仕様書（引き継ぎ用）

> 本書は2026-06-20〜21に `monster_gate.html` へ実装した「個体化／スキン経済／城防衛／シーズン／PvPレーティング」の全仕様を、**各仕様の目的（なぜ）つき**でまとめた引き継ぎ資料。
> 上位設計＝`個体化・スキン経済_設計提案.md`（§1–17）、ゲーム全体＝`ミラージュゲート_詳細仕様書.md`、移植＝`ミラージュゲート_Unity設計図.md §15`。
> 大前提：**本物のPvP/市場/換金/シーズン集計はサーバー権威台帳が必須**（設計提案§9/§17）。本書のローカル実装は「体験検証＋サーバーへ載せ替え可能な素地」。`tradableForCash:false` 等は前方互換フラグ。

---

## 0. 共通基盤・保存

- 保存：`localStorage`。アカウント系＝`mg_save`（`saveGame/loadGame`）、ワールド系＝`mg_world`（`saveWorld/loadWorld`）。
- 主な保存フィールド：`mg_save`= medals/collection/monCards/**inv**/serials/**swordSkin**/**heroSkin**/market/mktHist/**rating**/maxRankIdx。`mg_world`= owned/**guardians**/maxArea/view/**incomePot/incomeTs/lastCollect**/**season/castleStats**。
- 乱数：決定的乱数 `mulberry32(seed)`（patternSeed等の再現に使用。`Math.random()`はドロップ判定など非再現でOK）。

---

## 1. 個体（Instance）基盤 ― “世界に1つ”の確定データ
**目的**：モンスター/武器/スキンを「型」でなく**1点物の個体**にし、希少性・コレクション性・市場価値・来歴という“語れる価値”を成立させる土台にする（ポケモン型個体差＋PoE型性能＋CS2型見た目＋Brainrot型変異のハイブリッド）。

**仕様**：個体は `core`（発行時確定・不変想定）＋`meta`（所有/状態・可変）。
```
core: { instanceId, kind:'monster'|'weapon'|'hero', baseId, name, rarity(Common..Mythic),
  patternSeed(0-9999), wear(0-1), mutation(null|名), statRoll{atk,def,crit,traits[]},
  evoLevel(進化Lv・別概念), mint{serial,mintCap,season,isFounder}, provenance{dungeon,floor,sourceKill,bornAt,...},
  bossCard?, recolorHue?, finish?(武器), heroFinish?(ヒーロー), sig:null(Phase2でサーバー署名) }
meta: { owner,ownerHistory,bound,favoriteMark,nickname, tradableForCash:false,
  _inPlay(召喚中), _guarding(城配置中) }
```
**生成（`mintInstance`）**：`rollInstRarity`（cap差/star/floorで上振れ）→`patternSeed`→`rollWear`（無傷-/瀕死+/深層-）→`rollMutation`→`rollInstStats`(traits: drain/haste/guard/venom/crit/lifelink/**income**)→ローカル`serial`採番。`patternTier`でgem/good/normal。`MUTATIONS`=Rainbow/Divine/Cursed/Radioactive/Galaxy/Lava（低確率・見た目＞性能）。
**データ**：`G.inv[]`（台帳）、`G._serials`。`upgradeInstRarity`（レア度1段階UP）。

---

## 2. モンスター召喚カード ― 捕獲→持込→使用で召喚
**目的**：「撃破で稀に手に入る個体＝そのモンスターのカード」を、**他カード同様に袋へ持ち込み、手札から使うと召喚**できるようにする。死亡で再カード化＋稀に成長という周回の旨味で、収集と運用を結ぶ。

**仕様**：
- ドロップ＝個体。`tryRecruit`成功で `mintInstance` → `G.inv` ＋ `registerMonCard`（token `'M:'+instanceId` を `CARDS` に動的登録＝既存の手札/袋/描画がそのまま動く）。
- 持込：袋画面に「🧬モンスター個体」セクション（`renderBag`）。使用＝`summonFromCard`（同時1体・MP消費）。
- **死亡で50%再カード化**（`onSummonDeath`）。**そのうち低確率(15%)でレア度UP**（`upgradeInstRarity`）。50%は消滅(burn)。
- 個体差表示：`monCardEl`（レア度枠/変異オーラ/柄スウォッチ/シリアル/進化Lv/👑ボス/特殊カラー）。
**目的補足**：レア度UPは「使い込むほど価値が育つ」周回動機。burnは供給を絞り価値維持。

---

## 3. 武器スキン（kind:'weapon'）― 攻撃で毎ターン見せる
**目的**：CS2型“見せびらかしスキン”の担い手。装備の剣を1点物スキンにし、**攻撃モーションで毎ターン画面に映す**＝露出で需要を生む。

**仕様**：
- **自動生成の仕組み**：`WEAPON_BASES`(5)×`SKIN_FINISHES`(8: hardened/lava/galaxy/gold/frost/toxic/crimson/shadow)×柄Seed色ジッター＝実質無限。`pickFinish`(高レアは派手寄り)/`skinPalette`(patternSeed由来hue)。
- 入手：撃破3.5%(+ランク補正)で `tryWeaponDrop`。装備：個体コレクションの「🗡️装備」(`equipSwordSkin`/`G.swordSkin`)。
- 見せ場：`drawSwordSkinFX`＝攻撃中に斬撃弧をフィニッシュ配色＋変異グローで描画。小ボーナス`G.skinAtk`(レア度1〜4)。
- **HF原画**：base×finish=40枚を `assets/skins/skin_<base>_<finish>.png`（FLUX生成）。カード/ショーケース/マーケットで表示（無ければ手続き配色フォールバック）。

---

## 4. ヒーローのカラースキン ― 実行時リカラー＋顔マスクbake
**目的**：「今の職業と全く同じ画像の色違い」を**8方向×攻撃×歩行 全アニメ**に適用したい。数千枚を生成する代わりに**実行時hue-rotate**で“同じ絵の色違い”を即・無容量・完全一貫に実現（patternSeedで個体色差）。さらに**顔だけ据え置き**の高品質版を静的bakeで用意。

**仕様**：
- フィニッシュ`HERO_FINISHES`(9: azure/crimson/verdant/golden/violet/aqua/inferno/shadow/**prismatic**=虹アニメ)。入手3%(`tryHeroSkinDrop`+ランク補正)。
- 実行時：`heroSkinFilter`=`ctx.filter=hue-rotate()+saturate()` をヒーロー描画に適用（全アニメ自動）。
- **顔マスクbake**（`_hf_tools/bake_hero_skins.py`・Pillow+numpy・HF不要）：HSVで暖色の肌色を据え置き、装備だけ色相回転→`assets/hero_skins/<finish>/<元名>.png`（全17職×7フィニッシュ=5313枚を生成済み・azure原色/prismatic虹は焼かない）。
- 優先順：`bakedHeroSkinPath`があれば焼き版（顔据え置き・フィルタOFF）、無ければ実行時フィルタ。装備＝`equipHeroSkin`/`G.heroSkin`。
**目的補足**：実行時=軽量・全色即対応／bake=顔を守る高品質の任意強化。容量(約580MB)が重いので配布は取捨選択可。

---

## 5. ボスカード ― ワールド/ストーリーのボス由来
**目的**：「強敵ボスを攻略した証」を**カード化**して所有・召喚・防衛配置できる強力コレクションにする。さらに稀に変異/特殊カラーで“語れる1点物”を生む。

**仕様**：`tryBossCardDrop`（killMonsterでフック）。ボス/ガーディアン/orb/城主/ワールド階段ミニボス撃破時、world/storyで**25%**ドロップ。Epic下限。**さらに約30%**でMythic昇格＋変異＋`recolorHue`（見た目色変え）。`registerMonCard`が`_bossCard`/`_recolorHue`/`_bossArt`を付与。**HF原画**：ボス級15種を `assets/boss_cards/boss_<slug>.png`（`DEATH_SLUG`でマッピング）。

---

## 6. 個体マーケット（疑似・ローカル試作）
**目的**：価値は「流動性」で立つ。**相場の可視化・売買・検索**でコレクター/投資家需要の土台を作る（Phase B 疑似マーケット）。**現金化はしない**（法務回避・設計提案§8/§12）。

**仕様**（`showMarket`）：
- 価格モデル`estimatePrice`＝レア度base×変異mult×gem/sym×wear×Founder×serial×**進化Lv**。
- 売却`sellInstance`（メダル化／召喚中・配置中は不可）、購入`buyListing`（相場＋スプレッド・🔄入荷）、在庫`G._market`。
- 価格履歴：市場インデックス`G._mktHist`（秒/取引でtick）＋`histSVG`折れ線＋前比%。
- 絞り込み`_mktF`（種別/レア度/変異/並び）。

---

## 7. ブレンド（進化）― 進化レベルという別概念
**目的**：同種を集める動機（収集の出口）と、**レア度とは独立の縦軸の成長**を作る。変異/レアと複合し、稀に変異が生まれる射幸性で深みを出す。

**仕様**：`evoLevel`（初期1）。`blendGroups`（名前×進化Lvで2枚以上）。`doBlend`＝同じモンスター×2→**一定確率(高Lvほど低・最低30%)で進化Lv+1**（statRoll×1.25/Lv）。成功時**10%でレア度UP・10%で変異発現**。失敗は1枚消費。UI＝個体コレクション「🧪ブレンド」。価格/防衛力/カード表示に反映。

---

## 8. ショーケース（見せびらかし）
**目的**：露出＝需要の燃料。自慢の個体を飾る場と、同行中の個体オーラで「欲しい」を喚起。

**仕様**：個体コレクション(`showInstanceCollection`)に★(`favoriteMark`・最大6)で「🏅ショーケース」行。盤面では`drawAllyAura`が召喚個体に変異/レア度オーラを常時表示（変異は頭上に✦周回）。

---

## 9. 城ガーディアン防衛（最大5体＝ボス）
**目的**：占領地を“守る”メタ。配置で攻略難易度を上げ、リスク/リターン（強い個体を置くと守れるが奪われると失う）を作る。

**仕様**：
- `G.world.guardians[城ID]`＝**最大5体**(`GUARDIAN_MAX`)の配列。`manageCastle`で出し入れ（占領中は自由）。配置個体は`meta._guarding`で持込/売却/ブレンドから除外。
- 防衛力`guardianStrength`＝最大5体の逓減合計(上限16)。個体は`guardianStrengthOf`（レア度/statRoll/変異/ボス/**進化Lv**）。
- **奪還で配置個体は消滅(burn)**（`worldDefensePhase`）。
- ボスとして立ちはだかる本番は対人＝PvP（将来）。

---

## 10. 城の定期収入
**目的**：占領を“持ち続ける”動機（不労所得）。収入特性持ちモンスターの価値も作る。

**仕様**：`worldIncomeRatePerMin`＝占領城ごと(1+城Lv×0.5)×(1+`guardianIncomeBonus`)。`income`特性個体を配置で増額。`accrueIncome`（秒蓄積・最大3日cap）→`collectIncome`（回収→メダル）。ワールド画面に収入バナー＋回収ボタン。**奪取で蓄積ごと奪われる**のは将来(対人)。

---

## 11. シーズン制・動的リターン（突破率連動）
**目的**：難所ほど報いる動的バランス。**占領されやすい城＝報酬減／突破率(占領率)が低い城＝報酬増**で、攻略先の鮮度と挑戦価値を保つ。

**仕様**：`G.world.season`/`castleStats{att,clr,attW,clrW}`。出撃`castleAttempt`/占領`castleClear`。突破率`castleSuccessRate`（後述の加重）。`castleRewardMult`=clamp(0.6〜2.5, 0.7+(1-rate)×1.9)。`castleReward=min(cap, win×mult)`を出撃WINに反映（場内宝石が動的変動）。`newSeason`で実績reset＋報酬再評価（占領/収入は維持）。

---

## 12. 動的報酬の不正対策（レーティング加重＋上限）
**目的**：「弱アカウントがわざと負けて低レベル城の報酬を吊り上げる」抜け穴を塞ぐ。

**仕様**：
- **レーティング加重突破率**：`castleStats`に`attW/clrW`（挑戦/占領を`accountRating()`で加重和）。`castleSuccessRate`=加重(clrW/attW)＋star prior(平均rating5×4挑戦)で合成＝**強アカウントが落とせない城ほど係数UP／弱者の連敗はほぼ効かない**。`accountRating`＝maxArea/占領数/クリア数/メダルから1〜25。
- **城レベル別の報酬上限**`castleRewardCap`＝win×(1.2+★×0.22)、レベル別天井clamp(★<3:×1.8 / 3-4:×3.0 / 5+:×4.0)＝**低レベルは上がらない・青天井でない**。
- **トップ難所は上限UP**：★4+かつ加重突破率<25%で capMul加算。
- 検証：★2は弱acct30連敗でも上限148止まり／★5強acct15敗1占で加重突破率9%→報酬730・上限690→885。

---

## 13. PvPワールド レーティング（ELO）＋ランク＋報酬＋見た目
**目的**：競技性と“憧れ”。攻略の巧拙をレートで表し、ランク報酬とスキン入手率、そして**高レート者ほど豪華に見える**ことで上位を目指す動機を作る。

**仕様**：
- `G.rating`（初期1000）。城を相手とみなしELO：`castleRating=900+★×170+area×50`、`E=eloExpected(R,Co)`、`K=24+★×4`。`applyRatingResult(c,win)`を**worldOnClear=勝/worldOnLose=負**にフック。
  - 結果（検証）：難所攻略+48／格下攻略+17／高レートが格下攻略≈0／**高レートが弱城ミス-28**／低レートが弱城ミス-11／低レートが格上ミス0。＝「成功で上昇(難所ほど大)・失敗で下落(格下ミスは高レートほど大)」を1式で実現。
- **ランク**`RANKS`7段（ブロンズ→…→グランドマスター）。`rankOf/rankIndex`。**昇格報酬**＝メダル(200×idx)＋演出。降格ログ。`G.maxRankIdx`保持。
- **報酬→スキン入手率UP**：`rankSkinBonus()=rankIdx×1.2%` を `tryWeaponDrop/tryHeroSkinDrop` に加算（マスターで+6%）。
- **見た目（憧れ）**：`ratingBadgeHTML()`＝ランク色バッジ＋レート値、ダイヤ以上は発光、マスター以上は✦。ワールド画面ヘッダに表示。本物は他プレイヤーからも見える（サーバー配信）。

---

## 14. ローカル↔サーバー対応（何が将来サーバー必須か）
| 仕様 | ローカル実装(現在) | サーバー権威で“本物”化(将来) |
|---|---|---|
| 個体所有・真贋 | localStorage・sig=null | 発行server採番＋ed25519署名・DBが正 |
| マーケット | 疑似（相場/履歴/売買・換金なし） | 取引所・約定・エスクロー・(法務後)現金化 |
| 城PvP取り合い | 自分の占領地＋AI侵略(`worldDefensePhase`) | 全プレイヤー共有・対人侵攻・5体ボス戦・略奪 |
| 突破率/動的報酬 | 自分の実績＋難易度prior＋自分のrating加重 | 全プレイヤーの挑戦母数×rating集計 |
| レーティング/ランク | 城=相手レートで近似ELO | 対人ELO・ランキング・他者から可視 |
| シーズン | ローカルreset | 全体集計・期間・報酬配布 |
| 収入/略奪 | ローカル蓄積・回収 | 占領者へ蓄積移転（奪取） |
> クライアントの関数（`applyRatingResult`/`castleSuccessRate`/`recaptureChance`/income系）は、入力をサーバー集計値に差し替えるだけで同じ式が機能する設計。

---

## 15. 主要関数・調整ポイント索引
- 個体：`mintInstance` `upgradeInstRarity` `MUTATIONS` `patternTier` `rollInstStats(TR)`
- 召喚：`registerMonCard` `summonFromCard` `monCardEl` `onSummonDeath`(50%/15%) `drawAllyAura`
- 武器スキン：`WEAPON_BASES` `SKIN_FINISHES` `skinPalette` `skinArtPath` `drawSwordSkinFX` `tryWeaponDrop`(0.035+rank)
- ヒーロースキン：`HERO_FINISHES` `heroSkinFilter` `bakedHeroSkinPath` `_hf_tools/bake_hero_skins.py` `tryHeroSkinDrop`(0.03+rank)
- ボスカード：`tryBossCardDrop`(25%/30%) `DEATH_SLUG` `assets/boss_cards/`
- 市場：`estimatePrice` `sellInstance` `buyListing` `showMarket` `marketTick`/`histSVG`/`_mktF`
- ブレンド：`evoLevel` `blendGroups` `doBlend`(成功率0.62-…) `showBlend`
- 防衛：`guardianList`(max5) `guardianStrength`/`guardianStrengthOf` `recaptureChance`(0.18+0.07*隣接+0.025*area-0.035*守備) `manageCastle` `worldDefensePhase`(burn)
- 収入：`worldIncomeRatePerMin` `guardianIncomeBonus`('income') `accrueIncome`/`collectIncome`
- シーズン/報酬：`castleStats` `castleSuccessRate`(加重) `castleRewardMult` `castleRewardCap` `castleReward` `castleAttempt/Clear` `newSeason` `accountRating`
- レート：`G.rating` `RANKS` `rankOf`/`rankIndex` `castleRating` `eloExpected` `applyRatingResult` `rankSkinBonus` `ratingBadgeHTML`
- バランス調整は上記の係数（drop率/ELOのK/cap係数/income rate/mult範囲）を触る。**バランス初期値は極力保持の方針**（監修レポート）。

---

## 16. 検証・運用メモ
- 各機能はプレビューのDOM/ロジックで検証済み・コンソールエラー0・`PARSE_OK`（JavaScriptCore）。canvas/画像のライブ表示はヘッドレスpreviewが不安定なので**見え方は実機確認**。
- バックアップ：`monster_gate.html.bak_*`（instances/summoncards/phase1/weaponskin_market/heroskin/herobake/bakeall/bosscards/blend/castledef/season/antigame/rating 等）。
- `/tmp` プレビュー配信は容量が小さく、bake画像(約580MB)で溢れることがある→プレビュー用assetミラーは削除可（**プロジェクト本体の画像は無傷**）。
- 一次資料：本書＋`個体化・スキン経済_設計提案.md`（§17にPvP/シーズン/収入の将来設計）＋`ミラージュゲート_詳細仕様書.md`。
