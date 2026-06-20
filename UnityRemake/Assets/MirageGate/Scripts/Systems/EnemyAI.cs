using System.Collections;
using UnityEngine;
using MirageGate.Core;
using MirageGate.Runtime;

namespace MirageGate.Systems
{
    /// <summary>
    /// 敵フェーズのAI（§5.2）。role別の挙動を実行。原作 enemyPhase 相当。
    /// 移動・索敵・攻撃の判定は純ロジック、テンポ（揺れ収束待ち）はコルーチンで。
    /// </summary>
    public class EnemyAI : MonoBehaviour
    {
        CombatResolver combat;
        StatusEffectManager status;
        public float perEnemyDelay = 0.0f; // 同時多発回避の演出間隔（§2.3, 既定は即時）

        public void Init(CombatResolver combat, StatusEffectManager status)
        { this.combat = combat; this.status = status; }

        static readonly int[] DX = { -1, 0, 1, -1, 1, -1, 0, 1 };
        static readonly int[] DY = { -1, -1, -1, 0, 0, 1, 1, 1 };

        public IEnumerator RunEnemyPhase(RunState run)
        {
            // 味方(charmで寝返った敵)の行動は簡略（敵を攻撃）。TODO: 詳細化
            for (int i = 0; i < run.monsters.Count; i++)
            {
                var m = run.monsters[i];
                if (m.killed) continue;

                // 状態異常で行動可否（§5.3）
                if (m.HasStatus(StatusType.Sleep) || m.HasStatus(StatusType.Lock)) continue;
                if (m.HasStatus(StatusType.Slow)) { m.slowTick = !m.slowTick; if (m.slowTick) continue; }
                if (m.HasStatus(StatusType.Panic)) { Wander(run, m); continue; }

                if (!m.aggro && ShouldAggro(run, m)) m.aggro = true;
                if (!m.aggro) { if (Random.value < 0.25f) Wander(run, m); continue; }

                Act(run, m);
                if (run.player.hp <= 0) yield break;
                if (perEnemyDelay > 0) yield return new WaitForSecondsRealtime(perEnemyDelay);
            }
            yield break;
        }

        void Act(RunState run, MonsterInstance m)
        {
            var p = run.player;
            int dist = GridMap.Chebyshev(m.x, m.y, p.x, p.y);

            switch (m.Role)
            {
                case MonsterRole.Ranged:
                    if (TryRay(run, m, 5, out _, out _) && dist >= 2) { combat.EnemyAttack(m, p, run, true); return; }
                    break;
                case MonsterRole.Charge:
                    if (dist >= 2 && dist <= 4 && TryRay(run, m, 4, out int cdx, out int cdy))
                    { ChargeMove(run, m, cdx, cdy); return; }
                    break;
                case MonsterRole.Lunge:
                    if (dist == 2 && IsOpen(run, m) && TryRay(run, m, 2, out _, out _))
                    { combat.EnemyAttack(m, p, run, false); return; } // 飛びかかり
                    break;
                case MonsterRole.Healer:
                    if (HealNearbyAlly(run, m)) return;
                    if (dist <= 2) { StepAway(run, m); return; }
                    break;
                case MonsterRole.Summon:
                    if (Random.value < 0.25f && TrySummon(run, m)) return;
                    break;
                case MonsterRole.Coward:
                    if (dist <= 2) { if (Random.value < 0.5f) StepToward(run, m); else StepAway(run, m); return; }
                    break;
                case MonsterRole.Poison:
                    run.map.gimmicks[m.x, m.y] = GimmickType.Poison; // 足あとに毒床（§5.2）
                    break;
            }

            // 共通：隣接で攻撃、さもなくば接近
            if (dist <= 1) { m.animAtkT = Time.realtimeSinceStartup; combat.EnemyAttack(m, p, run, false); }
            else StepToward(run, m);
        }

        // ---------- 移動ヘルパ ----------
        bool Free(RunState run, int x, int y)
        {
            if (!run.map.IsWalkable(x, y)) return false;
            if (x == run.player.x && y == run.player.y) return false;
            foreach (var o in run.monsters) if (!o.killed && o.x == x && o.y == y) return false;
            foreach (var o in run.allies) if (!o.killed && o.x == x && o.y == y) return false;
            return true;
        }

        void StepToward(RunState run, MonsterInstance m)
        {
            var p = run.player;
            int best = int.MaxValue, bx = m.x, by = m.y;
            for (int k = 0; k < 8; k++)
            {
                int nx = m.x + DX[k], ny = m.y + DY[k];
                if (!Free(run, nx, ny)) continue;
                int d = GridMap.Chebyshev(nx, ny, p.x, p.y);
                if (d < best) { best = d; bx = nx; by = ny; }
            }
            if (bx != m.x || by != m.y) m.animMoveT = Time.realtimeSinceStartup; // 歩行アニメ
            m.x = bx; m.y = by;
        }

        void StepAway(RunState run, MonsterInstance m)
        {
            var p = run.player;
            int best = -1, bx = m.x, by = m.y;
            for (int k = 0; k < 8; k++)
            {
                int nx = m.x + DX[k], ny = m.y + DY[k];
                if (!Free(run, nx, ny)) continue;
                int d = GridMap.Chebyshev(nx, ny, p.x, p.y);
                if (d > best) { best = d; bx = nx; by = ny; }
            }
            m.x = bx; m.y = by;
        }

        void Wander(RunState run, MonsterInstance m)
        {
            int k = Random.Range(0, 8);
            int nx = m.x + DX[k], ny = m.y + DY[k];
            if (Free(run, nx, ny)) { m.x = nx; m.y = ny; }
        }

        /// <summary>突進（§5.2）：直線に最大3マス進み、終端隣接で1.4倍攻撃。</summary>
        void ChargeMove(RunState run, MonsterInstance m, int dx, int dy)
        {
            for (int step = 0; step < 3; step++)
            {
                int nx = m.x + dx, ny = m.y + dy;
                if (nx == run.player.x && ny == run.player.y) { combat.EnemyAttack(m, run.player, run, false, 1.4f); return; }
                if (!Free(run, nx, ny)) break;
                m.x = nx; m.y = ny;
            }
            if (GridMap.Chebyshev(m.x, m.y, run.player.x, run.player.y) <= 1)
                combat.EnemyAttack(m, run.player, run, false, 1.4f);
        }

        /// <summary>プレイヤーへの直線（8方向）が距離内＆見通しありか。dir成分を返す（§5.2）。</summary>
        bool TryRay(RunState run, MonsterInstance m, int maxDist, out int rdx, out int rdy)
        {
            rdx = rdy = 0;
            var p = run.player;
            int ddx = p.x - m.x, ddy = p.y - m.y;
            bool straight = ddx == 0 || ddy == 0 || Mathf.Abs(ddx) == Mathf.Abs(ddy);
            if (!straight) return false;
            int dist = GridMap.Chebyshev(m.x, m.y, p.x, p.y);
            if (dist < 1 || dist > maxDist) return false;
            int sx = System.Math.Sign(ddx), sy = System.Math.Sign(ddy);
            // 間のセルが見通せるか（壁・敵で遮られない）
            int cx = m.x + sx, cy = m.y + sy;
            while (cx != p.x || cy != p.y)
            {
                if (!run.map.IsWalkable(cx, cy)) return false;
                foreach (var o in run.monsters) if (!o.killed && o.x == cx && o.y == cy) return false;
                cx += sx; cy += sy;
            }
            rdx = sx; rdy = sy; return true;
        }

        bool IsOpen(RunState run, MonsterInstance m)
        {
            // 開放地形＝周囲8近傍がすべて床（廊下では飛び/突進しない, §5.2）
            for (int k = 0; k < 8; k++)
                if (!run.map.IsWalkable(m.x + DX[k], m.y + DY[k])) return false;
            return true;
        }

        bool HealNearbyAlly(RunState run, MonsterInstance m)
        {
            foreach (var o in run.monsters)
            {
                if (o.killed || o == m) continue;
                if (GridMap.Chebyshev(m.x, m.y, o.x, o.y) <= 2 && o.hp < o.maxHp && Random.value < 0.7f)
                { o.hp = System.Math.Min(o.maxHp, o.hp + 8); return true; }
            }
            return false;
        }

        bool TrySummon(RunState run, MonsterInstance m)
        {
            for (int k = 0; k < 8; k++)
            {
                int nx = m.x + DX[k], ny = m.y + DY[k];
                if (Free(run, nx, ny))
                {
                    // TODO: GameDatabaseからキラービーを引いて MakeInstance。ここでは省略。
                    return true;
                }
            }
            return false;
        }

        /// <summary>索敵：視界内/同部屋/距離<=4/直線<=8(見通し)でロックオン（§5.2）。</summary>
        bool ShouldAggro(RunState run, MonsterInstance m)
        {
            int dist = GridMap.Chebyshev(m.x, m.y, run.player.x, run.player.y);
            if (dist <= 4) return true;
            if (dist <= 8 && TryRay(run, m, 8, out _, out _)) return true;
            return false;
        }
    }
}
