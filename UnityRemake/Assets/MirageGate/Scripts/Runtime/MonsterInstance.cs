using System.Collections.Generic;
using MirageGate.Core;
using MirageGate.Data;

namespace MirageGate.Runtime
{
    /// <summary>
    /// 盤面に存在する敵1体の実体。MonsterData(定義)＋ランタイム状態。
    /// floorFactorで強化済みの実HP/ATKを保持する（§5.4）。
    /// </summary>
    public class MonsterInstance
    {
        public MonsterData data;

        public int x, y;
        public int hp, maxHp;   // floorFactor適用後
        public int atk;         // floorFactor適用後
        public int def;

        public bool aggro;      // 索敵済み（§5.2）
        public bool killed;     // 二重撃破防止
        public bool slowTick;   // slow用トグル
        public float animMoveT, animAtkT; // 歩行/攻撃アニメ用の最終時刻（realtime）

        // 状態異常（type→残量）
        public Dictionary<StatusType, int> status = new Dictionary<StatusType, int>();

        public bool forceBoss;  // 最終フロアのガーディアン等で昇格
        public string displayName; // 章ボス名など（任意）

        public MonsterRole Role => data.role;
        public int Pay => data.pay;
        public bool IsBoss => data.isBoss || forceBoss;

        public int Status(StatusType t) => status.TryGetValue(t, out var v) ? v : 0;
        public bool HasStatus(StatusType t) => Status(t) > 0;
    }
}
