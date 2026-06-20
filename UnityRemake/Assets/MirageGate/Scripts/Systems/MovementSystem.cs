using MirageGate.Core;
using MirageGate.Runtime;

namespace MirageGate.Systems
{
    /// <summary>移動解決の結果。</summary>
    public struct MoveResult
    {
        public bool moved;
        public bool blockedByMonster; // 進行先に敵＝攻撃に切替（呼び出し側で処理）
        public MonsterInstance target;
        public bool reachedGoal;
        public int gimmickDamage;     // 着地タイルの床ダメージ（毒1/溶岩2/呪2）
        public GimmickType gimmick;
    }

    /// <summary>
    /// グリッド移動の解決（§2.2/§6.4）。純ロジック（UnityEngine非依存）。
    /// 氷結タイルは同方向に自動スライド（壁/敵/ゴールで停止）。
    /// </summary>
    public class MovementSystem
    {
        public int FireDamage = 2, CurseDamage = 2, PoisonFloorDamage = 1;

        public MonsterInstance MonsterAt(RunState run, int x, int y)
        {
            foreach (var m in run.monsters) if (!m.killed && m.x == x && m.y == y) return m;
            return null;
        }

        /// <summary>(dx,dy)方向へ1歩。8方向。進行先が敵なら blockedByMonster=true で返す。</summary>
        public MoveResult ResolveMove(RunState run, int dx, int dy)
        {
            var map = run.map;
            var p = run.player;
            int nx = p.x + dx, ny = p.y + dy;
            var res = new MoveResult();

            if (!map.IsWalkable(nx, ny)) return res; // 壁＝移動不可
            var mon = MonsterAt(run, nx, ny);
            if (mon != null) { res.blockedByMonster = true; res.target = mon; return res; }

            // 移動
            p.x = nx; p.y = ny;
            if (dx != 0) p.faceX = dx < 0 ? -1 : 1; // 向き更新
            res.moved = true;

            // 氷結スライド（§6.4）：同方向へ滑走、壁/敵/ゴールで停止
            while (map.gimmicks[p.x, p.y] == GimmickType.Ice)
            {
                int tx = p.x + dx, ty = p.y + dy;
                if (!map.IsWalkable(tx, ty)) break;
                if (MonsterAt(run, tx, ty) != null) break;
                p.x = tx; p.y = ty;
                if (p.x == map.goal.x && p.y == map.goal.y) break; // ゴール上で停止
            }

            // 視界更新は呼び出し側（描画レイヤ）。ringHeal歩行回復（§3.5）
            if (p.HasRing("heal")) p.hp = System.Math.Min(p.maxHp, p.hp + 1);

            // 床ギミックダメージ（§6.4）
            res.gimmick = map.gimmicks[p.x, p.y];
            switch (res.gimmick)
            {
                case GimmickType.Poison: res.gimmickDamage = PoisonFloorDamage; break;
                case GimmickType.Fire: res.gimmickDamage = FireDamage; break;
                case GimmickType.Curse: res.gimmickDamage = CurseDamage; break;
            }
            if (res.gimmickDamage > 0) p.hp -= res.gimmickDamage;

            res.reachedGoal = (p.x == map.goal.x && p.y == map.goal.y);
            return res;
        }
    }
}
