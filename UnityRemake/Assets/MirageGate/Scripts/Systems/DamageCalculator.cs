using UnityEngine;
using MirageGate.Core;
using MirageGate.Data;
using MirageGate.Runtime;

namespace MirageGate.Systems
{
    /// <summary>
    /// ダメージ・難易度係数の純粋計算（§8.1 / §5.4 / §6.3）。状態を持たない。
    /// </summary>
    public class DamageCalculator
    {
        readonly GameBalanceConfig cfg;
        readonly System.Func<int, int> rndInclusive; // [min,max] 乱数（テスト差替え可）

        public DamageCalculator(GameBalanceConfig cfg, System.Func<int, int> rndInclusive = null)
        {
            this.cfg = cfg;
            this.rndInclusive = rndInclusive ?? DefaultRnd;
        }

        static int DefaultRnd(int maxInclusive) => Random.Range(0, maxInclusive + 1);

        int Variance()
        {
            int span = cfg.attackVarianceMax - cfg.attackVarianceMin; // 4
            return cfg.attackVarianceMin + rndInclusive(span);        // -2..+2
        }

        /// <summary>難易度係数（§6.3）。</summary>
        public float FloorFactor(RunState run)
            => cfg.FloorFactor(run.floor, run.EffectiveFloors, run.dungeon.star);

        /// <summary>プレイヤー→敵 与ダメージ（§8.1）。crit判定は呼び出し側で行い結果を渡す。</summary>
        public int PlayerToEnemy(PlayerState p, MonsterInstance m, bool crit)
        {
            float baseDmg = (p.atk + p.swordAtk) * (p.swordMul <= 0 ? 1f : p.swordMul);
            int raw = Mathf.Max(1, Mathf.RoundToInt(baseDmg - m.def + Variance()));
            float critMul = crit ? cfg.critMultiplier : 1f;
            int dmg = Mathf.RoundToInt(raw * critMul);

            // 格下補正：lvl>=敵cap かつ 非ボス なら確定1撃（§3.4/§8.1）
            int cap = (m.data.cap != null && m.data.cap.Length > p.job.capIndex)
                ? m.data.cap[p.job.capIndex] : int.MaxValue;
            if (p.lvl >= cap && !m.IsBoss && m.hp > dmg) dmg = m.hp;
            return dmg;
        }

        /// <summary>会心率（職特性/luck）。</summary>
        public float CritChance(PlayerState p)
        {
            if (p.job.crit > 0) return p.job.crit;
            return p.job.luck ? cfg.luckCritChance : cfg.defaultCritChance;
        }

        /// <summary>敵→プレイヤー 被ダメージ（§5.4）。mult=charge等の倍率。</summary>
        public int EnemyToPlayer(MonsterInstance m, PlayerState p, RunState run, float mult = 1f)
        {
            int atkP = Mathf.RoundToInt(m.atk * mult);
            int defTot = p.def + p.shieldDef;
            float ff = m.data.minScaleExempt ? 1f : FloorFactor(run);
            int minG = Mathf.Max(1, Mathf.RoundToInt(m.data.MinDmgFor(cfg) * ff));
            int dmg = Mathf.Max(minG, Mathf.RoundToInt(atkP - defTot) + Variance());
            if (m.Role == MonsterRole.Knock) dmg = Mathf.RoundToInt(dmg * cfg.knockDamageMul);
            return dmg;
        }

        /// <summary>魔法カードのダメージ（§4.3）。</summary>
        public int MagicDamage(CardData c, PlayerState p)
        {
            float ringMul = p.HasRing("magic") ? 1.5f : 1f;
            return Mathf.RoundToInt(c.mag * p.job.magBonus * ringMul);
        }
    }
}
