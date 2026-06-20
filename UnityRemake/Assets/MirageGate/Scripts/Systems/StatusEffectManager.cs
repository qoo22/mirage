using System.Collections.Generic;
using MirageGate.Core;
using MirageGate.Data;
using MirageGate.Runtime;

namespace MirageGate.Systems
{
    /// <summary>
    /// 状態異常・バフの付与と毎ターン処理（§5.3）。敵・プレイヤー共用。
    /// </summary>
    public class StatusEffectManager
    {
        readonly Dictionary<StatusType, StatusEffectData> defs;
        public int PoisonPerTurn = 5;     // 毒ダメージ/T（§5.3）
        public int MpRegenPerTurn = 1;    // 敵フェーズ後のMP自然回復（§4.4）

        public StatusEffectManager(IEnumerable<StatusEffectData> definitions)
        {
            defs = new Dictionary<StatusType, StatusEffectData>();
            if (definitions != null)
                foreach (var d in definitions) if (d != null) defs[d.type] = d;
        }

        /// <summary>敵への付与。magicimmuneは弾く（§5.2/§5.3）。charmは味方化。</summary>
        public void Apply(RunState run, MonsterInstance m, StatusType t, int duration)
        {
            bool blocked = !defs.TryGetValue(t, out var d) || d.blockedByMagicImmune;
            if (blocked && m.Role == MonsterRole.MagicImmune) return;
            m.status[t] = duration;
            if (t == StatusType.Charm && run != null && run.monsters.Remove(m))
                run.allies.Add(m);
        }

        public void Apply(PlayerState p, StatusType t, int duration) => p.status[t] = duration;

        /// <summary>ターン終了時の一括処理（§2.2）：毒ダメ・残量減・MP回復。</summary>
        public void TickAll(RunState run)
        {
            TickPlayer(run.player);
            // 敵（撃破済みは除外）
            for (int i = run.monsters.Count - 1; i >= 0; i--)
            {
                var m = run.monsters[i];
                if (m.killed) continue;
                TickMonster(m);
                if (m.hp <= 0) { m.killed = true; /* TODO: 毒死の撃破処理（演出/配当なし） */ }
            }
        }

        void TickPlayer(PlayerState p)
        {
            if (p.Status(StatusType.Poison) > 0)
            {
                p.hp -= PoisonPerTurn;
                Dec(p.status, StatusType.Poison);
            }
            if (p.Status(StatusType.Regen) > 0)
            {
                p.hp = System.Math.Min(p.maxHp, p.hp + 1);
                Dec(p.status, StatusType.Regen);
            }
            // 時限バフを1減（barrier/comboは回数制＝使用時に消費するのでtickしない）
            Dec(p.status, StatusType.Invis);
            Dec(p.status, StatusType.Speed);
            // MP自然回復
            p.mp = System.Math.Min(p.maxMp, p.mp + MpRegenPerTurn);
        }

        void TickMonster(MonsterInstance m)
        {
            if (m.Status(StatusType.Poison) > 0)
            {
                m.hp -= PoisonPerTurn;
                Dec(m.status, StatusType.Poison);
            }
            Dec(m.status, StatusType.Sleep);
            Dec(m.status, StatusType.Lock);
            Dec(m.status, StatusType.Slow);
            Dec(m.status, StatusType.Panic);
            Dec(m.status, StatusType.Charm);
        }

        static void Dec(Dictionary<StatusType, int> bag, StatusType t)
        {
            if (bag.TryGetValue(t, out var v) && v > 0)
            {
                if (v <= 1) bag.Remove(t);
                else bag[t] = v - 1;
            }
        }
    }
}
