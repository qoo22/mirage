using System.Collections.Generic;
using UnityEngine;
using MirageGate.Data;
using MirageGate.Runtime;

namespace MirageGate.Systems
{
    /// <summary>
    /// カードドロップの重み抽選（§4.7 rollCardDrop）。安いほど高頻度・強カードは深く難い時だけ。
    /// </summary>
    public class CardDropTable
    {
        readonly GameBalanceConfig cfg;
        readonly List<CardData> pool; // 非禁忌の全カード

        public CardDropTable(GameBalanceConfig cfg, IEnumerable<CardData> allCards)
        {
            this.cfg = cfg;
            pool = new List<CardData>();
            foreach (var c in allCards) if (!c.forbidden) pool.Add(c);
        }

        public CardData Roll(RunState run, System.Func<float> rnd01 = null)
        {
            rnd01 ??= () => Random.value;
            int star = run.dungeon.star;
            int f = run.floor + (run.dungeon.hidden ? 4 : 0);
            int deep = f + Mathf.Max(0, star - 2);
            int maxCost = cfg.cardMaxCostBase + deep * cfg.cardMaxCostPerDeep;

            float total = 0f;
            var weights = new List<(CardData c, float w)>();
            foreach (var c in pool)
            {
                if (c.cost > maxCost) continue;
                float w = cfg.cardDropWeightNum / (c.cost + cfg.cardDropWeightBias);
                if (c.category == Core.CardCategory.Heal) w *= cfg.cardDropHealMul;
                w *= 1f + Mathf.Min(cfg.cardDropDeepCap, deep * cfg.cardDropDeepStep)
                       * Mathf.Max(0, c.cost - cfg.cardDropStrongCostThreshold) / 26f;
                weights.Add((c, w));
                total += w;
            }
            if (weights.Count == 0) return null;

            float r = rnd01() * total;
            foreach (var (c, w) in weights) { r -= w; if (r <= 0) return c; }
            return weights[0].c;
        }
    }
}
