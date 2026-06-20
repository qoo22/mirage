using System.Collections.Generic;
using MirageGate.Data;

namespace MirageGate.Runtime
{
    /// <summary>床に落ちているアイテム（カード/宝石/ショップ/スロットタイル）。</summary>
    public class FloorItem
    {
        public enum Kind { Card, Gem, Shop, Slot }
        public Kind kind;
        public int x, y;
        public string cardId;     // Card
        public int gemValue;      // Gem
        public Core.SlotMachineType slotType; // Slot
        public bool sold;         // Shop在庫の売切
    }

    /// <summary>
    /// 現在のダンジョン挑戦1回ぶんの状態。原作 G のラン域に対応。
    /// </summary>
    public class RunState
    {
        public DungeonData dungeon;
        public Core.GameMode mode;
        public int floor = 1;
        public bool escaped;          // エスケープカードで撤退（loot持ち帰り）
        public MonsterData bossOverride; // 最終フロアに置く固定ボス（物語など）

        public PlayerState player;
        public GridMap map;
        public List<MonsterInstance> monsters = new List<MonsterInstance>();
        public List<MonsterInstance> allies = new List<MonsterInstance>();
        public List<FloorItem> items = new List<FloorItem>();

        /// <summary>このダンジョンの実効フロア数（autoDeepen対応）。</summary>
        public int EffectiveFloors =>
            dungeon != null && dungeon.autoDeepen ? dungeon.autoDeepenFloors :
            dungeon != null ? dungeon.floors : 1;

        public bool IsFinalFloor => floor >= EffectiveFloors;
    }
}
