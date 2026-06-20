using System.Collections.Generic;
using MirageGate.Core;
using MirageGate.Data;
using MirageGate.Runtime;

namespace MirageGate.Systems
{
    /// <summary>カード使用の文脈（対象・盤面）。</summary>
    public struct CardCastContext
    {
        public RunState run;
        public MonsterInstance singleTarget; // SingleEnemy時
    }

    /// <summary>
    /// カード効果の実行（§4.3）。原作 resolveCard 相当。
    /// 撃破処理(loot/exp/演出)は呼び出し側で hp<=0 の敵を combat.KillMonster へ流す（責務分離）。
    /// </summary>
    public class CardEffectExecutor
    {
        readonly GameBalanceConfig cfg;
        readonly DamageCalculator dmg;
        readonly ProgressionSystem prog;
        readonly StatusEffectManager status;

        public CardEffectExecutor(GameBalanceConfig cfg, DamageCalculator dmg,
            ProgressionSystem prog, StatusEffectManager status)
        { this.cfg = cfg; this.dmg = dmg; this.prog = prog; this.status = status; }

        /// <summary>MP消費（§4.4）。ringLuckyで半減。</summary>
        public int MpCost(CardData c, PlayerState p)
            => p.HasRing("lucky") && c.mp > 0 ? System.Math.Max(1, c.mp / 2) : c.mp;

        /// <summary>使用を試みる。MP不足ならfalse（消費なし）。</summary>
        public bool TryCast(CardData c, PlayerState p, in CardCastContext ctx)
        {
            int need = MpCost(c, p);
            if (need > p.mp) return false;

            Apply(c, p, ctx);
            p.mp -= need;

            if (c.forbidden)
            {
                p.forbiddenUsed = true; // 真エンド封印（§10.4）
                if (c.costMaxHp > 0) p.maxHp = System.Math.Max(1, p.maxHp - c.costMaxHp);
                if (c.selfDmg > 0) p.hp -= c.selfDmg;
            }
            return true;
        }

        /// <summary>視界内の生存敵（TODO: 厳密な視界判定。暫定でaggro or 全生存）。</summary>
        static List<MonsterInstance> VisibleEnemies(RunState run)
        {
            var list = new List<MonsterInstance>();
            foreach (var m in run.monsters) if (!m.killed) list.Add(m);
            return list;
        }

        void Apply(CardData c, PlayerState p, in CardCastContext ctx)
        {
            var run = ctx.run;

            // ---- 攻撃（§4.3）----
            if (c.mag > 0)
            {
                int d = dmg.MagicDamage(c, p);
                if (c.multi) foreach (var m in VisibleEnemies(run)) DealMagic(m, d);
                else if (ctx.singleTarget != null) DealMagic(ctx.singleTarget, d);
            }
            if (c.spear > 0 && ctx.singleTarget != null)
            {
                // 直線貫通：対象方向の生存敵へ連鎖（簡易：対象と同列/行の敵）
                foreach (var m in VisibleEnemies(run))
                    if (m.x == ctx.singleTarget.x || m.y == ctx.singleTarget.y) DealMagic(m, c.spear);
            }
            if (c.poison > 0 && ctx.singleTarget != null) status.Apply(run, ctx.singleTarget, StatusType.Poison, c.poison);
            if (c.death && ctx.singleTarget != null && ctx.singleTarget.Role != MonsterRole.MagicImmune)
                ctx.singleTarget.hp = 0;

            // ---- 回復 ----
            if (c.heal > 0)
            {
                float mul = p.job.potBonus * (p.HasRing("magic") ? 1.25f : 1f);
                p.hp = System.Math.Min(p.maxHp, p.hp + (int)System.Math.Round(c.heal * mul));
            }
            if (c.healMax) p.hp = p.maxHp;
            if (c.mpRestore > 0) p.mp = System.Math.Min(p.maxMp, p.mp + c.mpRestore);
            if (c.regen > 0) status.Apply(p, StatusType.Regen, c.regen);
            if (c.hpUp > 0) { p.maxHp += c.hpUp; p.hp += c.hpUp; }

            // ---- 制御（§4.3）----
            if (c.lockAll > 0) foreach (var m in VisibleEnemies(run)) status.Apply(run, m, StatusType.Lock, c.lockAll);
            if (c.sleep > 0 && ctx.singleTarget != null) status.Apply(run, ctx.singleTarget, StatusType.Sleep, c.sleep);
            if (c.slow > 0 && ctx.singleTarget != null) status.Apply(run, ctx.singleTarget, StatusType.Slow, c.slow);
            if (c.panic > 0) foreach (var m in VisibleEnemies(run)) status.Apply(run, m, StatusType.Panic, c.panic);
            if (c.charm > 0 && ctx.singleTarget != null && !ctx.singleTarget.IsBoss)
                status.Apply(run, ctx.singleTarget, StatusType.Charm, c.charm);
            if (c.storm > 0) foreach (var m in VisibleEnemies(run)) DealMagic(m, c.storm); // TODO: 吹き飛ばし移動
            if (c.bright) status.Apply(p, StatusType.Bright, 999);
            if (c.silent) status.Apply(p, StatusType.Silent, 999);
            if (c.escape) run.escaped = true; // 撤退（loot持ち帰り §4.6）
            // map/search/teleport は盤面操作 → TODO（呼び出し側UI/移動と連携）

            // ---- バフ ----
            if (c.barrier > 0) status.Apply(p, StatusType.Barrier, c.barrier);
            if (c.invis > 0) status.Apply(p, StatusType.Invis, c.invis);
            if (c.combo > 0) status.Apply(p, StatusType.Combo, c.combo);
            if (c.speed > 0) status.Apply(p, StatusType.Speed, c.speed);
            if (c.glow) { p.atk += 2; p.def += 1; p.maxHp += 4; p.hp += 4; }
            if (c.power) { p.atk += 5; p.def += 3; p.maxHp += 10; p.hp += 10; }

            // ---- 装備（§3.5）----
            if (c.swordMul > 0) p.swordMul = c.swordMul;       // 剣は上書き（clearSwordSlot相当）
            if (c.swordAtk > 0) p.swordAtk = c.swordAtk;
            if (c.goldSword) p.goldSword = true;
            if (c.drain) p.drain = true;
            if (c.sleepSword) p.sleepSword = true;
            if (c.shieldDef > 0) { p.shieldDef += c.shieldDef; p.def += c.shieldDef; }
            if (c.pocket > 0) p.handMax = System.Math.Min(cfg.maxHandMax, p.handMax + c.pocket);
            if (!string.IsNullOrEmpty(c.ring))
            {
                if (c.ring == "revive") p.reviveCharge = true;
                else p.rings.Add(c.ring);
            }
        }

        void DealMagic(MonsterInstance m, int d)
        {
            if (m.killed || m.Role == MonsterRole.MagicImmune) return; // 魔法無効（§5.2）
            m.hp -= d; // hp<=0 の正式な撃破(loot/exp/演出)は呼び出し側が combat.KillMonster でスイープ
        }
    }
}
