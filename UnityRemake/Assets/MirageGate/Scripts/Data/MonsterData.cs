using UnityEngine;
using MirageGate.Core;

namespace MirageGate.Data
{
    /// <summary>
    /// モンスターデータ。原作 MONSTERS（§5.1 全38体）に対応。
    /// </summary>
    [CreateAssetMenu(menuName = "MirageGate/Monster", fileName = "Monster_")]
    public class MonsterData : ScriptableObject
    {
        [Header("識別")]
        public string monsterName = "プーニャ緑";
        public string emoji = "🟢";

        [Header("基礎ステータス（floorFactorで乗算される）")]
        public int hp = 22;
        public int atk = 5;
        public int def = 1;

        [Header("AI/評価")]
        public MonsterRole role = MonsterRole.Melee;
        [Tooltip("強さ/価値の目安。配当・最低保証ダメージの基準（§5.4/§7.2）")]
        public int pay = 1;
        [Tooltip("職業別の育成上限Lv [物理, 魔法, 幸運]（§3.4）")]
        public int[] cap = { 16, 16, 16 };
        [Tooltip("固有の最低保証ダメージ。未設定(-1)ならpayから導出")]
        public int minDmg = -1;

        [Header("フラグ")]
        public bool isBoss = false;       // 章/城ボス・最終F守護者
        public bool minScaleExempt = false; // floorFactorで最低保証を底上げしない

        public int MinDmgFor(GameBalanceConfig cfg)
        {
            if (minDmg >= 0) return minDmg;
            if (pay >= 18) return cfg.minDmgPay18;
            if (pay >= 11) return cfg.minDmgPay11;
            if (pay >= 7) return cfg.minDmgPay7;
            if (pay >= 3) return cfg.minDmgPay3;
            return cfg.minDmgDefault;
        }
    }
}
