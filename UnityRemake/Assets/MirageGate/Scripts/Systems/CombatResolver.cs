using UnityEngine;
using MirageGate.Data;
using MirageGate.Runtime;

namespace MirageGate.Systems
{
    /// <summary>
    /// 攻撃命中〜撃破の解決（§8）。論理ダメージ適用と、演出トリガの発火を仲介する。
    /// 「Visual Only」：演出は GameFeelDirector に渡し、論理座標は触らない（§1/§8.5）。
    /// </summary>
    public class CombatResolver
    {
        readonly GameBalanceConfig cfg;
        readonly DamageCalculator dmg;
        readonly ProgressionSystem prog;
        readonly EconomyManager econ;
        readonly GameFeelDirector feel;

        public CombatResolver(GameBalanceConfig cfg, DamageCalculator dmg, ProgressionSystem prog,
            EconomyManager econ, GameFeelDirector feel)
        { this.cfg = cfg; this.dmg = dmg; this.prog = prog; this.econ = econ; this.feel = feel; }

        /// <summary>プレイヤーの通常攻撃（§8.1）。</summary>
        public void PlayerAttack(PlayerState p, MonsterInstance m, RunState run)
        {
            bool crit = Random.value < dmg.CritChance(p);
            int d = dmg.PlayerToEnemy(p, m, crit);
            m.hp -= d;

            if (p.drain) p.hp = Mathf.Min(p.maxHp, p.hp + d / 4); // ドレインソード

            feel.OnPlayerHit(m, crit, m.x - p.x, m.y - p.y); // HS・揺れ・けぞり・SE（§8.3）
            feel.PopDamage(m.x, m.y, d, crit);

            if (m.hp <= 0) KillMonster(p, m, run);
        }

        /// <summary>撃破処理（§8.2）。</summary>
        public void KillMonster(PlayerState p, MonsterInstance m, RunState run)
        {
            if (m.killed) return;
            m.killed = true;

            feel.OnKill(m);                            // ヒットストップ ボス320/雑魚150・揺れampボス2.0（§8.3）
            prog.GrantKillExp(p, m);                   // 経験値（§3.4）
            p.loot += econ.KillLoot(m, p, run.floor);  // 配当（§7.2）

            // TODO: 仲間カード化12% / revive死骸 / split分裂 / killflash / fxDie
        }

        /// <summary>敵の攻撃（§5.4）。reviveCharge があれば力尽き時に半HP復活。</summary>
        public void EnemyAttack(MonsterInstance m, PlayerState p, RunState run, bool ranged, float mult = 1f)
        {
            int d = dmg.EnemyToPlayer(m, p, run, mult);

            if (p.Status(Core.StatusType.Barrier) > 0) { p.status[Core.StatusType.Barrier]--; d = 0; }
            p.hp -= d;

            feel.OnPlayerDamaged(d, ranged, p.x - m.x, p.y - m.y); // 被弾HS・けぞり（§8.3）
            feel.PopDamage(p.x, p.y, -d, false);        // 負値＝被弾(赤)表示

            if (p.hp <= 0 && p.reviveCharge)
            {
                p.reviveCharge = false;
                p.hp = Mathf.CeilToInt(p.maxHp / 2f);  // リバイブ（§3.5）
            }
        }
    }
}
