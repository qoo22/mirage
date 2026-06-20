using System;
using System.Collections.Generic;
using MirageGate.Core;
using MirageGate.Data;
using MirageGate.Runtime;

namespace MirageGate.Systems
{
    /// <summary>部屋の矩形。</summary>
    public struct Room
    {
        public int x, y, w, h;
        public int CenterX => x + w / 2;
        public int CenterY => y + h / 2;
        public int Area => w * h;
        public bool Contains(int px, int py) => px >= x && py >= y && px < x + w && py < y + h;
    }

    /// <summary>
    /// フロアのプロシージャル生成（設計図§6.2）。原作 buildFloor 相当。
    /// UnityEngine非依存（System.Random注入）＝Seed確定で完全再現可能、単体検証も容易。
    /// </summary>
    public class DungeonGenerator
    {
        readonly GameBalanceConfig cfg;
        public DungeonGenerator(GameBalanceConfig cfg) { this.cfg = cfg; }

        // 任意の依存。割り当てると床アイテムに中身（カードid/宝石額）を入れる
        public CardDropTable dropTable;
        public EconomyManager econ;

        public List<Room> LastRooms { get; private set; } = new List<Room>();

        /// <summary>1フロアを生成し RunState に反映する。</summary>
        public GridMap Build(RunState run, int seed)
        {
            var rng = new System.Random(seed);
            var d = run.dungeon;
            int side = d.size + 4;                       // 外枠壁（§6.2）
            var map = new GridMap(side, side);

            // 1) 部屋生成：最大120試行・3〜5×3〜4・gap>=1で非重複
            var rooms = CarveRooms(map, d.rooms, rng);
            LastRooms = rooms;

            // 2) 通路：部屋中心を順に L字接続（1マス幅）
            for (int i = 1; i < rooms.Count; i++)
                ConnectRooms(map, rooms[i - 1], rooms[i], rng);

            // 3) スタート / ゴール
            map.start = (rooms[0].CenterX, rooms[0].CenterY);
            var last = rooms[rooms.Count - 1];
            map.goal = (last.CenterX, last.CenterY);
            map.goalIsCrystal = run.IsFinalFloor;        // 最終F=クリスタル / 通常F=下り階段

            // 4) 連結保証：start から全床へ到達できるか。孤立部屋があれば最寄り床へ追加接続
            EnsureConnectivity(map, rooms, rng);

            // 5) 敵配置（§6.2）：部屋ごと target = max(2, round(面積*dense*0.9))。スタート3マス回避
            PlaceMonsters(run, map, rooms, rng);

            // 5.5) 最終フロアのボス（固定ボス or 帯最強を強化したガーディアン §6.2/§10.3）
            if (run.IsFinalFloor)
            {
                if (run.bossOverride != null) PlaceBoss(run, map, rng);
                else PlaceGuardian(run, map, rng);
            }

            // 6) アイテム配置：1〜3個（45%宝石 / 55%カード）
            PlaceItems(run, map, rng);

            // 6.5) サービスタイル（ショップ/スロット）。チュートリアル・最終F以外（§6.6簡易版）
            PlaceServices(run, map, rng);

            // 7) 床ギミック（§6.4）：gimProfile傾向でパッチ配置。配置後の到達性は床を塞がないので保証済
            ScatterGimmicks(run, map, rooms, rng);

            run.map = map;
            return map;
        }

        // ---------- 1) 部屋 ----------
        List<Room> CarveRooms(GridMap map, int maxRooms, System.Random rng)
        {
            var rooms = new List<Room>();
            int attempts = 120;
            for (int a = 0; a < attempts && rooms.Count < Math.Max(1, maxRooms); a++)
            {
                int w = 3 + rng.Next(3); // 3..5
                int h = 3 + rng.Next(2); // 3..4
                int x = 1 + rng.Next(Math.Max(1, map.width - w - 2));
                int y = 1 + rng.Next(Math.Max(1, map.height - h - 2));
                var room = new Room { x = x, y = y, w = w, h = h };
                if (Overlaps(rooms, room, 1)) continue; // gap>=1
                for (int ry = y; ry < y + h; ry++)
                    for (int rx = x; rx < x + w; rx++)
                        map.tiles[rx, ry] = TileKind.Room;
                rooms.Add(room);
            }
            // 最低1部屋は保証
            if (rooms.Count == 0)
            {
                var r = new Room { x = 1, y = 1, w = 3, h = 3 };
                for (int ry = 1; ry < 4; ry++) for (int rx = 1; rx < 4; rx++) map.tiles[rx, ry] = TileKind.Room;
                rooms.Add(r);
            }
            return rooms;
        }

        static bool Overlaps(List<Room> rooms, Room cand, int gap)
        {
            foreach (var r in rooms)
                if (cand.x - gap < r.x + r.w && cand.x + cand.w + gap > r.x &&
                    cand.y - gap < r.y + r.h && cand.y + cand.h + gap > r.y)
                    return true;
            return false;
        }

        // ---------- 2) 通路 ----------
        void ConnectRooms(GridMap map, Room a, Room b, System.Random rng)
        {
            int ax = a.CenterX, ay = a.CenterY, bx = b.CenterX, by = b.CenterY;
            if (rng.Next(2) == 0) { CarveH(map, ax, bx, ay); CarveV(map, ay, by, bx); }
            else { CarveV(map, ay, by, ax); CarveH(map, ax, bx, by); }
        }

        void CarveH(GridMap map, int x0, int x1, int y)
        {
            for (int x = Math.Min(x0, x1); x <= Math.Max(x0, x1); x++)
                if (map.InBounds(x, y) && map.tiles[x, y] == TileKind.Wall) map.tiles[x, y] = TileKind.Corridor;
        }

        void CarveV(GridMap map, int y0, int y1, int x)
        {
            for (int y = Math.Min(y0, y1); y <= Math.Max(y0, y1); y++)
                if (map.InBounds(x, y) && map.tiles[x, y] == TileKind.Wall) map.tiles[x, y] = TileKind.Corridor;
        }

        // ---------- 4) 連結保証 ----------
        void EnsureConnectivity(GridMap map, List<Room> rooms, System.Random rng)
        {
            var reach = Reachable(map, map.start);
            foreach (var r in rooms)
            {
                if (reach[r.CenterX, r.CenterY]) continue;
                // 未到達部屋を start へ追加接続
                ConnectRooms(map, rooms[0], r, rng);
                reach = Reachable(map, map.start);
            }
        }

        /// <summary>start から8近傍で到達できる床のbool配列（BFS）。</summary>
        public static bool[,] Reachable(GridMap map, (int x, int y) start)
        {
            var seen = new bool[map.width, map.height];
            if (!map.IsWalkable(start.x, start.y)) return seen;
            var q = new Queue<(int, int)>();
            seen[start.x, start.y] = true; q.Enqueue(start);
            int[] dx = { -1, 0, 1, -1, 1, -1, 0, 1 };
            int[] dy = { -1, -1, -1, 0, 0, 1, 1, 1 };
            while (q.Count > 0)
            {
                var (cx, cy) = q.Dequeue();
                for (int k = 0; k < 8; k++)
                {
                    int nx = cx + dx[k], ny = cy + dy[k];
                    if (map.InBounds(nx, ny) && !seen[nx, ny] && map.IsWalkable(nx, ny))
                    { seen[nx, ny] = true; q.Enqueue((nx, ny)); }
                }
            }
            return seen;
        }

        // ---------- 5) 敵 ----------
        void PlaceMonsters(RunState run, GridMap map, List<Room> rooms, System.Random rng)
        {
            var band = SpawnBandForFloor(run);
            if (band == null || band.Count == 0) return;

            for (int i = 0; i < rooms.Count; i++)
            {
                if (i == 0) continue; // スタート部屋は安全
                var r = rooms[i];
                int target = Math.Max(2, (int)Math.Round(r.Area * run.dungeon.dense * 0.9));
                for (int n = 0; n < target; n++)
                {
                    int mx = r.x + rng.Next(r.w), my = r.y + rng.Next(r.h);
                    if (GridMap.Chebyshev(mx, my, map.start.x, map.start.y) <= 3) continue;
                    if (OccupiedByMonster(run, mx, my)) continue;
                    if (mx == map.goal.x && my == map.goal.y) continue;

                    var data = band[rng.Next(band.Count)];
                    run.monsters.Add(MakeInstance(run, data, mx, my));
                }
            }
        }

        /// <summary>帯の最強モンスターを強化したガーディアンを最終フロアに配置（§6.2）。</summary>
        void PlaceGuardian(RunState run, GridMap map, System.Random rng)
        {
            var band = SpawnBandForFloor(run);
            if (band == null || band.Count == 0) return;
            MonsterData strongest = band[0];
            foreach (var m in band) if (m.pay > strongest.pay) strongest = m;

            int[] dx = { 0, -1, 1, 0, 0 }, dy = { 0, 0, 0, -1, 1 };
            for (int k = 0; k < dx.Length; k++)
            {
                int bx = map.goal.x + dx[k], by = map.goal.y + dy[k];
                if (!map.IsWalkable(bx, by) || OccupiedByMonster(run, bx, by)) continue;
                if (bx == map.start.x && by == map.start.y) continue;
                var inst = MakeInstance(run, strongest, bx, by);
                inst.hp = inst.maxHp = (int)(inst.maxHp * 1.8f); // ガーディアン強化
                inst.atk = (int)(inst.atk * 1.3f);
                inst.forceBoss = true;
                run.monsters.Add(inst);
                return;
            }
        }

        /// <summary>固定ボスをゴール付近に配置（isBoss扱い・撃破でクリスタル出現相当）。</summary>
        void PlaceBoss(RunState run, GridMap map, System.Random rng)
        {
            // ゴール隣の空きを探す
            int[] dx = { 0, -1, 1, 0, 0, -1, 1, -1, 1 }, dy = { 0, 0, 0, -1, 1, -1, -1, 1, 1 };
            for (int k = 0; k < dx.Length; k++)
            {
                int bx = map.goal.x + dx[k], by = map.goal.y + dy[k];
                if (!map.IsWalkable(bx, by)) continue;
                if (bx == map.start.x && by == map.start.y) continue;
                if (OccupiedByMonster(run, bx, by)) continue;
                // 固定ボスは定義の正確ステ（floorFactor非適用・既にスケール済み）
                var d = run.bossOverride;
                run.monsters.Add(new MonsterInstance
                {
                    data = d, x = bx, y = by,
                    hp = d.hp, maxHp = d.hp, atk = d.atk, def = d.def, forceBoss = true
                });
                return;
            }
        }

        bool OccupiedByMonster(RunState run, int x, int y)
        {
            foreach (var m in run.monsters) if (m.x == x && m.y == y) return true;
            return false;
        }

        /// <summary>floorFactor適用後の実HP/ATKを持つ敵実体を生成（§5.4）。</summary>
        public MonsterInstance MakeInstance(RunState run, MonsterData data, int x, int y)
        {
            float ff = cfg.FloorFactor(run.floor, run.EffectiveFloors, run.dungeon.star);
            int hp = Math.Max(1, (int)Math.Round(data.hp * ff));
            int atk = Math.Max(1, (int)Math.Round(data.atk * ff));
            return new MonsterInstance
            {
                data = data, x = x, y = y,
                hp = hp, maxHp = hp, atk = atk, def = data.def
            };
        }

        /// <summary>このフロアに湧く敵帯（§6.3 floorBand）。深層ほど弱敵を除外。</summary>
        public List<MonsterData> SpawnBandForFloor(RunState run)
        {
            var d = run.dungeon;
            List<MonsterData> raw;

            if (d.autoDeepen && d.deepenOrder != null && d.deepenOrder.Count >= 4)
            {
                // スライディングウィンドウ（§6.5 deepenUltimate）
                int total = d.autoDeepenFloors;
                int span = d.deepenOrder.Count - 3;
                float t = total <= 1 ? 1f : (run.floor - 1f) / (total - 1f);
                int lo = (int)Math.Floor(span * t);
                lo = Math.Max(0, Math.Min(d.deepenOrder.Count - 4, lo));
                raw = new List<MonsterData> {
                    d.deepenOrder[lo], d.deepenOrder[lo + 1], d.deepenOrder[lo + 2], d.deepenOrder[lo + 3]
                };
            }
            else
            {
                if (d.bands == null || d.bands.Count == 0) return null;
                int idx = Math.Min(run.floor - 1, d.bands.Count - 1);
                raw = new List<MonsterData>(d.bands[Math.Max(0, idx)].monsters);
            }
            if (raw.Count <= 1) return raw;

            // 深層で弱敵を切り捨て：cutoff = maxPay*(base + slope*prog)
            int fl = run.EffectiveFloors;
            float prog = fl <= 1 ? 1f : Math.Min(1f, (run.floor - 1f) / (fl - 1f));
            int maxPay = 1;
            foreach (var m in raw) maxPay = Math.Max(maxPay, m.pay);
            float cutoff = maxPay * (cfg.bandCutoffBase + cfg.bandCutoffSlope * prog);
            var kept = new List<MonsterData>();
            foreach (var m in raw) if (m.pay >= cutoff) kept.Add(m);
            return kept.Count > 0 ? kept : raw;
        }

        // ---------- 6) アイテム ----------
        void PlaceItems(RunState run, GridMap map, System.Random rng)
        {
            int count = 1 + rng.Next(3); // 1..3
            var spots = WalkableSpots(map, run);
            for (int i = 0; i < count && spots.Count > 0; i++)
            {
                int s = rng.Next(spots.Count);
                var (x, y) = spots[s]; spots.RemoveAt(s);

                if (rng.NextDouble() < cfg.gemDropChance) // 宝石
                {
                    int val = econ != null ? econ.GemValue(run.dungeon)
                        : System.Math.Max(1, (int)System.Math.Round((double)run.dungeon.win / System.Math.Max(1, run.dungeon.floors) * 0.5));
                    run.items.Add(new FloorItem { kind = FloorItem.Kind.Gem, x = x, y = y, gemValue = val });
                }
                else // カード
                {
                    var card = dropTable != null ? dropTable.Roll(run) : null;
                    run.items.Add(new FloorItem { kind = FloorItem.Kind.Card, x = x, y = y, cardId = card != null ? card.id : "" });
                }
            }
        }

        /// <summary>ショップ/スロットのタイルを確率配置（チュートリアル・最終F以外）。</summary>
        void PlaceServices(RunState run, GridMap map, System.Random rng)
        {
            var d = run.dungeon;
            if (d.isTutorial || run.IsFinalFloor) return;
            var spots = WalkableSpots(map, run);

            // ショップ ≈42%
            if (rng.NextDouble() < 0.42 && spots.Count > 0)
            {
                int s = rng.Next(spots.Count); var (x, y) = spots[s]; spots.RemoveAt(s);
                run.items.Add(new FloorItem { kind = FloorItem.Kind.Shop, x = x, y = y });
            }
            // スロット ≈30%
            if (rng.NextDouble() < 0.30 && spots.Count > 0)
            {
                int s = rng.Next(spots.Count); var (x, y) = spots[s]; spots.RemoveAt(s);
                var types = new[] { Core.SlotMachineType.Item, Core.SlotMachineType.Gambler, Core.SlotMachineType.Monster };
                run.items.Add(new FloorItem { kind = FloorItem.Kind.Slot, x = x, y = y, slotType = types[rng.Next(3)] });
            }
        }

        List<(int x, int y)> WalkableSpots(GridMap map, RunState run)
        {
            var list = new List<(int, int)>();
            for (int y = 0; y < map.height; y++)
                for (int x = 0; x < map.width; x++)
                {
                    if (!map.IsWalkable(x, y)) continue;
                    if (x == map.start.x && y == map.start.y) continue;
                    if (x == map.goal.x && y == map.goal.y) continue;
                    list.Add((x, y));
                }
            return list;
        }

        // ---------- 7) ギミック ----------
        void ScatterGimmicks(RunState run, GridMap map, List<Room> rooms, System.Random rng)
        {
            var d = run.dungeon;
            int patches = Math.Min(7, (int)Math.Round(1 + d.star * 0.6));
            (GimmickType type, float p)[] profile = {
                (GimmickType.Ice, d.gimIce), (GimmickType.Poison, d.gimPoison),
                (GimmickType.Fire, d.gimFire), (GimmickType.Curse, d.gimCurse)
            };
            for (int p = 0; p < patches; p++)
            {
                // 種類を確率重みで選択
                float sum = 0; foreach (var e in profile) sum += e.p;
                if (sum <= 0) return;
                float r = (float)rng.NextDouble() * sum; GimmickType chosen = GimmickType.None;
                foreach (var e in profile) { r -= e.p; if (r <= 0) { chosen = e.type; break; } }
                if (chosen == GimmickType.None) continue;

                var room = rooms[rng.Next(rooms.Count)];
                if (room.Contains(map.start.x, map.start.y)) continue; // スタート部屋は避ける
                int size = 2 + rng.Next(chosen == GimmickType.Ice ? 5 : 3);
                int cx = room.x + rng.Next(room.w), cy = room.y + rng.Next(room.h);
                for (int n = 0; n < size; n++)
                {
                    int gx = Math.Max(0, Math.Min(map.width - 1, cx + rng.Next(3) - 1));
                    int gy = Math.Max(0, Math.Min(map.height - 1, cy + rng.Next(3) - 1));
                    if (!map.IsWalkable(gx, gy)) continue;
                    if (gx == map.start.x && gy == map.start.y) continue;
                    if (gx == map.goal.x && gy == map.goal.y) continue;
                    map.gimmicks[gx, gy] = chosen; // 床ギミックは床を塞がない＝到達性に影響しない
                }
            }
        }
    }
}
