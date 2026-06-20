using MirageGate.Core;

namespace MirageGate.Runtime
{
    /// <summary>タイル種別。</summary>
    public enum TileKind { Wall = 0, Room = 1, Corridor = 2 }

    /// <summary>
    /// 1フロアのグリッド。原作 buildFloor（§6.2）の出力に対応。
    /// 8近傍チェビシェフ移動。論理座標のみ（描画は別レイヤー）。
    /// </summary>
    public class GridMap
    {
        public readonly int width, height;
        public TileKind[,] tiles;
        public GimmickType[,] gimmicks;
        public bool[,] seen;   // 既視（fog）
        public bool[,] lit;    // 現在視界

        public (int x, int y) start;
        public (int x, int y) goal;     // 最終F=クリスタル / 通常F=下り階段
        public bool goalIsCrystal;

        public GridMap(int w, int h)
        {
            width = w; height = h;
            tiles = new TileKind[w, h];
            gimmicks = new GimmickType[w, h];
            seen = new bool[w, h];
            lit = new bool[w, h];
        }

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < width && y < height;
        public bool IsWalkable(int x, int y) => InBounds(x, y) && tiles[x, y] != TileKind.Wall;

        /// <summary>チェビシェフ距離（8近傍1ターン移動）。</summary>
        public static int Chebyshev(int ax, int ay, int bx, int by)
        {
            int dx = ax > bx ? ax - bx : bx - ax;
            int dy = ay > by ? ay - by : by - ay;
            return dx > dy ? dx : dy;
        }
    }
}
