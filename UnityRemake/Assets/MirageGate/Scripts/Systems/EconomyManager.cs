using UnityEngine;
using MirageGate.Data;
using MirageGate.Runtime;

namespace MirageGate.Systems
{
    /// <summary>
    /// メダル・配当・宝石・撃破報酬（§7）。loot(ラン内)とmedals(永続)の2層。
    /// </summary>
    public class EconomyManager
    {
        readonly GameBalanceConfig cfg;
        readonly System.Func<float> rnd01;

        /// <summary>永続メダル。実体はMetaProgress/SaveManagerが保持し、ここは参照を持つ想定。</summary>
        public int Medals;

        public EconomyManager(GameBalanceConfig cfg, int startMedals, System.Func<float> rnd01 = null)
        {
            this.cfg = cfg;
            this.Medals = startMedals;
            this.rnd01 = rnd01 ?? (() => Random.value);
        }

        /// <summary>撃破配当（§7.2 深追い配当）。goldSword/gambler加算込み。</summary>
        public int KillLoot(MonsterInstance m, PlayerState p, int floor)
        {
            int pay = m.Pay;
            int total = Mathf.Max(1, Mathf.RoundToInt(
                pay * (cfg.lootBaseFactor + cfg.lootDepthPerFloor * Mathf.Max(0, floor - 1))));

            if (p.goldSword && rnd01() < 0.3f)
                total += Mathf.Max(1, Mathf.RoundToInt(pay * cfg.goldSwordBonus));
            if (p.job.luck && rnd01() < cfg.gamblerLootChance)
                total += pay;
            return total;
        }

        /// <summary>宝石の価値（§7.1）。</summary>
        public int GemValue(DungeonData d)
        {
            float perFloor = (float)d.win / Mathf.Max(1, d.floors);
            return Mathf.RoundToInt(perFloor * (cfg.gemValueMin + rnd01() * cfg.gemValueRandRange));
        }

        /// <summary>BET支払い（出撃時）。足りなければfalse。</summary>
        public bool PayBet(DungeonData d)
        {
            if (Medals < d.bet) return false;
            Medals -= d.bet;
            return true;
        }

        /// <summary>クリア時の配当統合（§7.1）。</summary>
        public int Settle(RunState run)
        {
            int win = run.dungeon.win + run.player.loot;
            Medals += win;
            return win;
        }

        /// <summary>カード→MP変換ゲイン（§4.4）。</summary>
        public int ConvertGain(CardData c)
            => Mathf.Max(cfg.convertMin, Mathf.RoundToInt(c.cost * cfg.convertRate));
    }
}
