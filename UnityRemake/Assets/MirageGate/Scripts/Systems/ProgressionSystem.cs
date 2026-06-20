using UnityEngine;
using MirageGate.Data;
using MirageGate.Runtime;

namespace MirageGate.Systems
{
    /// <summary>
    /// レベルアップと撃破経験値（§3.3 / §3.4）。純粋ロジック。
    /// </summary>
    public class ProgressionSystem
    {
        readonly GameBalanceConfig cfg;
        readonly System.Func<float> rnd01;

        public ProgressionSystem(GameBalanceConfig cfg, System.Func<float> rnd01 = null)
        {
            this.cfg = cfg;
            this.rnd01 = rnd01 ?? (() => Random.value);
        }

        /// <summary>nレベル上昇（§3.3）。</summary>
        public void LevelUp(PlayerState p, int n)
        {
            for (int k = 0; k < n; k++)
            {
                p.lvl++;
                p.maxHp += cfg.hpPerLevel;
                p.atk += cfg.atkPerLevel;
                p.maxMp += cfg.mpPerLevel;
                p.defFrac += cfg.defFracPerLevel;
                if (p.defFrac >= 1f) { p.def += 1; p.defFrac -= 1f; }
            }
            p.hp = Mathf.Min(p.maxHp, p.hp + cfg.hpRecoverPerLevel * n);
            p.mp = Mathf.Min(p.maxMp, p.mp + cfg.mpRecoverPerLevel * n);
            if (p.job.mpHalf) { p.maxMp = Mathf.RoundToInt(p.maxHp / 2f); p.mp = Mathf.Min(p.mp, p.maxMp); }
        }

        /// <summary>撃破時の成長判定（§3.4）。倒した敵のcapと自Lvの差から確率/上昇量を決める。</summary>
        public void GrantKillExp(PlayerState p, MonsterInstance m)
        {
            int cap = (m.data.cap != null && m.data.cap.Length > p.job.capIndex)
                ? m.data.cap[p.job.capIndex] : 0;
            int diff = cap - p.lvl;

            float chance; int gain;
            if (diff >= 70) { chance = 1f; gain = 5; }
            else if (diff >= 50) { chance = 1f; gain = 4; }
            else if (diff >= 30) { chance = 1f; gain = 3; }
            else if (diff >= 15) { chance = 0.95f; gain = 2; }
            else if (diff >= 5) { chance = 0.85f; gain = 1 + (rnd01() < 0.5f ? 0 : 1); }
            else if (diff >= 1) { chance = 0.55f; gain = 1; }
            else return; // 同格以下は伸びない

            if (rnd01() < chance)
                LevelUp(p, Mathf.Min(gain, cap - p.lvl));
        }
    }
}
