using UnityEngine;

namespace MirageGate.Data
{
    /// <summary>
    /// ゲーム全体のバランス定数と数式パラメータを一括保持するSO（設計図 付録A）。
    /// 原作にハードコードされていた係数をここへ集約し、Inspectorで調整可能にする。
    /// </summary>
    [CreateAssetMenu(menuName = "MirageGate/Balance Config", fileName = "GameBalanceConfig")]
    public class GameBalanceConfig : ScriptableObject
    {
        [Header("難易度カーブ floorFactor（§6.3）")]
        [Tooltip("1Fの弱体係数。原作 startF=0.85")]
        public float startFactor = 0.85f;
        [Tooltip("最終Fピーク = endBase + endPerStar*min(star,9)。原作 1.30 + 0.11*star")]
        public float endFactorBase = 1.30f;
        public float endFactorPerStar = 0.11f;
        [Tooltip("カーブ指数。>1で序盤やさしく・終盤に手ごたえ。原作は2026-06-18に0.78→1.4へ修正")]
        public float curveExponent = 1.4f;
        [Tooltip("floorFactorをAnimationCurveでも可視化したい場合の任意カーブ（p:0..1）")]
        public AnimationCurve curvePreview = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("敵バンド選別 floorBand（§6.3）")]
        public float bandCutoffBase = 0.12f;   // cutoff = maxPay*(base + slope*prog)
        public float bandCutoffSlope = 0.48f;

        [Header("プレイヤー攻撃（§8.1）")]
        public float critMultiplier = 1.5f;
        public float defaultCritChance = 0.12f;
        public float luckCritChance = 0.18f;
        public int attackVarianceMin = -2; // rnd(-2..+2)
        public int attackVarianceMax = 2;

        [Header("被ダメージ最低保証 minDmg（§5.4）")]
        public int minDmgPay18 = 14;
        public int minDmgPay11 = 10;
        public int minDmgPay7 = 7;
        public int minDmgPay3 = 4;
        public int minDmgDefault = 2;
        public float knockDamageMul = 0.85f;
        public float chargeDamageMul = 1.4f;

        [Header("撃破配当・宝石（§7.2）")]
        public float lootBaseFactor = 0.4f;       // pay*(0.4 + 0.12*(floor-1))
        public float lootDepthPerFloor = 0.12f;
        public float goldSwordBonus = 0.8f;        // 格上撃破で pay*0.8
        public float gamblerLootChance = 0.3f;
        [Range(0f, 1f)] public float gemDropChance = 0.45f; // 床アイテムの宝石率
        public float gemValueMin = 0.3f; // (win/floors)*(0.3 + rand*0.5)
        public float gemValueRandRange = 0.5f;

        [Header("カードドロップ重み（§4.7）")]
        public float cardDropWeightNum = 170f;     // 170/(cost+12)
        public float cardDropWeightBias = 12f;
        public float cardDropHealMul = 2.2f;
        public float cardDropDeepStep = 0.05f;     // min(1.4, deep*0.05)
        public float cardDropDeepCap = 1.4f;
        public int cardDropStrongCostThreshold = 26;
        public int cardMaxCostBase = 20;           // maxCost = 20 + deep*13
        public int cardMaxCostPerDeep = 13;

        [Header("ショップ重み（§7.4）")]
        public float shopWeightNum = 120f;         // 120/(cost+8)
        public float shopWeightBias = 8f;
        public float shopHealMul = 1.5f;
        public int shopStockCount = 10;

        [Header("レア度しきい値（§4.5）")]
        public int rarityCostSR = 24;
        public int rarityCostSSR = 44;
        public int rarityCostUR = 70;

        [Header("装備MP（レア度比例・§4 EQUIP_MP）")]
        public int equipMpR = 6;
        public int equipMpSR = 10;
        public int equipMpSSR = 14;
        public int equipMpUR = 18; // LRも18

        [Header("成長（§3.3 levelUp）")]
        public int hpPerLevel = 1;
        public int atkPerLevel = 1;
        public int mpPerLevel = 1;
        public float defFracPerLevel = 0.45f;     // 約2.22Lvで+1
        public int hpRecoverPerLevel = 2;
        public int mpRecoverPerLevel = 2;

        [Header("カード→MP変換（§4.4）")]
        public float convertRate = 0.5f;          // max(2, round(cost*0.5))
        public int convertMin = 2;

        [Header("初期資産（§12）")]
        public int startingMedals = 2000;
        public int defaultHandMax = 10;
        public int maxHandMax = 12;
        public int defaultBagLimit = 10;

        // ---- ヘルパー（純粋関数。各Systemから参照）----

        /// <summary>難易度係数（§6.3）。floor:1基点, floors:総数, star:難易度。</summary>
        public float FloorFactor(int floor, int floors, int star)
        {
            float p = floors <= 1 ? 1f : (floor - 1f) / (floors - 1f);
            float endF = endFactorBase + endFactorPerStar * Mathf.Min(star, 9);
            return startFactor + (endF - startFactor) * Mathf.Pow(p, curveExponent);
        }

        public int EquipMp(Core.Rarity r)
        {
            switch (r)
            {
                case Core.Rarity.R: return equipMpR;
                case Core.Rarity.SR: return equipMpSR;
                case Core.Rarity.SSR: return equipMpSSR;
                default: return equipMpUR; // UR / LR
            }
        }

        public Core.Rarity RarityFromCost(int cost, bool forbidden, bool isOrbcall)
        {
            if (forbidden) return isOrbcall ? Core.Rarity.LR : Core.Rarity.UR;
            if (cost >= rarityCostUR) return Core.Rarity.UR;
            if (cost >= rarityCostSSR) return Core.Rarity.SSR;
            if (cost >= rarityCostSR) return Core.Rarity.SR;
            return Core.Rarity.R;
        }
    }
}
