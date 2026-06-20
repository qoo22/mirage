using System.Collections.Generic;
using UnityEngine;
using MirageGate.Data;
using MirageGate.Runtime;

namespace MirageGate.Systems
{
    /// <summary>ショップ在庫の1枠。</summary>
    public class ShopSlot { public CardData card; public bool sold; }

    /// <summary>
    /// ショップ（街＆ダンジョン内売店, §7.4）。在庫はランダム10種・重み 120/(cost+8)。
    /// 街=グローバル在庫 / 店タイル=タイル固有在庫。
    /// </summary>
    public class ShopSystem
    {
        readonly GameBalanceConfig cfg;
        readonly List<CardData> sellable; // 非禁忌

        public ShopSystem(GameBalanceConfig cfg, IEnumerable<CardData> allCards)
        {
            this.cfg = cfg;
            sellable = new List<CardData>();
            foreach (var c in allCards) if (!c.forbidden) sellable.Add(c);
        }

        /// <summary>在庫生成（§7.4）：重み抽選で重複なし10種。</summary>
        public List<ShopSlot> RollStock(System.Func<float> rnd01 = null)
        {
            rnd01 ??= () => Random.value;
            var picked = new List<ShopSlot>();
            var work = new List<CardData>(sellable);
            for (int n = 0; n < cfg.shopStockCount && work.Count > 0; n++)
            {
                float total = 0f;
                foreach (var c in work)
                {
                    float w = cfg.shopWeightNum / (c.cost + cfg.shopWeightBias);
                    if (c.category == Core.CardCategory.Heal) w *= cfg.shopHealMul;
                    total += w;
                }
                float r = rnd01() * total; CardData chosen = work[0];
                foreach (var c in work)
                {
                    float w = cfg.shopWeightNum / (c.cost + cfg.shopWeightBias);
                    if (c.category == Core.CardCategory.Heal) w *= cfg.shopHealMul;
                    r -= w; if (r <= 0) { chosen = c; break; }
                }
                picked.Add(new ShopSlot { card = chosen });
                work.Remove(chosen);
            }
            return picked;
        }

        /// <summary>購入（§7.4）。残高チェック→手札/コレクション振り分けは呼び出し側。</summary>
        public bool TryBuy(ShopSlot slot, EconomyManager econ)
        {
            if (slot.sold || econ.Medals < slot.card.cost) return false;
            econ.Medals -= slot.card.cost;
            slot.sold = true;
            return true;
        }

        /// <summary>店主セリフ（§7.4）：在庫レア度・所持・手札数で4分岐。</summary>
        public string ShopkeeperLine(List<ShopSlot> stock, PlayerState p)
        {
            // TODO: SSR以上あり→「いいのが入ってる」 / 手札<=2→「まず一枚どうだ」 / …
            return "ゆっくり見ていきな";
        }
    }
}
