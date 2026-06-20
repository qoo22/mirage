using MirageGate.Core;
using MirageGate.Runtime;

namespace MirageGate.Systems
{
    /// <summary>
    /// 視界計算（§5.2/§11）。プレイヤーの視界半径(job.sight)内で見通しのあるタイルを lit に、
    /// 一度見たタイルは seen に記録（fog of war）。bright/視界の指輪でフロア全体可視。
    /// </summary>
    public class VisionSystem
    {
        public int BaseSight = 3;

        public void Compute(RunState run)
        {
            var map = run.map;
            var p = run.player;
            if (map == null) return;

            for (int y = 0; y < map.height; y++)
                for (int x = 0; x < map.width; x++)
                    map.lit[x, y] = false;

            bool bright = p.Status(StatusType.Bright) > 0 || p.HasRing("bright");
            if (bright)
            {
                for (int y = 0; y < map.height; y++)
                    for (int x = 0; x < map.width; x++)
                        if (map.tiles[x, y] != TileKind.Wall) { map.lit[x, y] = true; map.seen[x, y] = true; }
                Reveal(map, p.x, p.y);
                return;
            }

            int sight = (p.job != null ? p.job.sight : BaseSight);
            for (int dy = -sight; dy <= sight; dy++)
                for (int dx = -sight; dx <= sight; dx++)
                {
                    int tx = p.x + dx, ty = p.y + dy;
                    if (!map.InBounds(tx, ty)) continue;
                    if (GridMap.Chebyshev(p.x, p.y, tx, ty) > sight) continue;
                    if (LineClear(map, p.x, p.y, tx, ty))
                    {
                        map.lit[tx, ty] = true;
                        map.seen[tx, ty] = true;
                    }
                }
            Reveal(map, p.x, p.y);
        }

        static void Reveal(GridMap map, int x, int y)
        {
            if (map.InBounds(x, y)) { map.lit[x, y] = true; map.seen[x, y] = true; }
        }

        /// <summary>始点→終点の見通し。途中セルに壁があれば遮られる（終点の壁自体は見える）。</summary>
        static bool LineClear(GridMap map, int x0, int y0, int x1, int y1)
        {
            int dx = System.Math.Abs(x1 - x0), dy = System.Math.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
            int err = dx - dy, cx = x0, cy = y0;
            while (true)
            {
                if (cx == x1 && cy == y1) return true;          // 終点に到達＝見通しOK
                if (!(cx == x0 && cy == y0))                    // 始点以外の途中セル
                    if (map.tiles[cx, cy] == TileKind.Wall) return false; // 壁で遮断
                int e2 = 2 * err;
                if (e2 > -dy) { err -= dy; cx += sx; }
                if (e2 < dx) { err += dx; cy += sy; }
            }
        }

        /// <summary>そのタイルにいる敵がプレイヤーから見えるか（描画用）。</summary>
        public static bool IsVisible(RunState run, int x, int y)
            => run.map != null && run.map.InBounds(x, y) && run.map.lit[x, y];
    }
}
