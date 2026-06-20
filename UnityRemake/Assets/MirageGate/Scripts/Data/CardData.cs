using UnityEngine;
using MirageGate.Core;

namespace MirageGate.Data
{
    /// <summary>
    /// カードデータ。原作 CARDS（§4.1 全45枚）に対応。
    /// 効果は「どのフィールドに値が入っているか」で表現する（原作の素朴な構造を踏襲）。
    /// 大量の効果分岐は CardEffectExecutor 側で解決する（§4.3）。
    /// </summary>
    [CreateAssetMenu(menuName = "MirageGate/Card", fileName = "Card_")]
    public class CardData : ScriptableObject
    {
        [Header("識別")]
        public string id = "fire";
        public string cardName = "ファイア";
        public string icon = "🔥";
        public CardCategory category = CardCategory.Attack;
        [TextArea] public string desc = "単体に魔法18";

        [Header("コスト")]
        [Tooltip("ショップ/メダル価格。レア度判定にも使用（forbiddenは0）")]
        public int cost = 12;
        [Tooltip("使用MP。装備はレア度から自動設定する想定")]
        public int mp = 6;

        [Header("対象")]
        public TargetKind target = TargetKind.SingleEnemy;

        [Header("攻撃系（§4.1）")]
        public int mag = 0;        // 魔法ダメージ基礎値
        public bool multi = false; // 視界内全敵
        public int spear = 0;      // 直線貫通ダメージ
        public int poison = 0;     // 毒付与ターン数
        public bool death = false; // 即死

        [Header("回復系")]
        public int heal = 0;
        public bool healMax = false;
        public int mpRestore = 0;
        public int regen = 0;      // 歩行毎+1HP（累積上限）
        public int hpUp = 0;       // 最大HP永続+

        [Header("補助・制御系")]
        public int lockAll = 0;    // 視界内全敵を停止（ターン数）
        public int sleep = 0;
        public int slow = 0;
        public int panic = 0;
        public int charm = 0;
        public int storm = 0;      // 吹き飛ばし＋ダメージ
        public bool bright = false;
        public bool map = false;
        public bool search = false;
        public bool silent = false;
        public bool teleport = false;
        public bool escape = false;

        [Header("バフ系")]
        public int barrier = 0;    // 無効回数
        public int invis = 0;      // ターン数
        public int combo = 0;      // 連続行動回数
        public int speed = 0;      // ターン数
        public bool glow = false;  // ステ小上昇＋微レベルup
        public bool power = false; // ステ大上昇＋レベルup

        [Header("装備系（§3.5）")]
        public float swordMul = 0f;     // 通常攻撃倍率（longsword1.3/greatsword1.6）
        public int swordAtk = 0;        // 攻撃加算
        public bool goldSword = false;  // 格上撃破で配当+80%
        public bool drain = false;      // 与ダメ1/4吸収
        public bool sleepSword = false; // 攻撃時に睡眠付与
        public int shieldDef = 0;       // このランDEF+
        public int pocket = 0;          // 手札枠+
        [Tooltip("指輪効果キー: magic/lucky/heal/tonic/bright/revive。空なら指輪でない")]
        public string ring = "";

        [Header("禁忌（§4.1・§10.4）")]
        public bool forbidden = false;  // ショップ非売・記憶片購入・TRUEエンド封印
        public int costMaxHp = 0;       // 使用で最大HP永続低下
        public int selfDmg = 0;         // 使用で自傷
        public int gainShard = 0;       // 撃破で記憶片+
        public int shardPrice = 0;      // 記憶片での購入価格

        /// <summary>cost/forbidden からレア度を導出（§4.5）。GameBalanceConfig 経由で算出推奨。</summary>
        public Rarity GetRarity(GameBalanceConfig cfg)
            => cfg.RarityFromCost(cost, forbidden, id == "orbcall");
    }
}
