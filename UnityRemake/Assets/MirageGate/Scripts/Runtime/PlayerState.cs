using System.Collections.Generic;
using MirageGate.Core;
using MirageGate.Data;

namespace MirageGate.Runtime
{
    /// <summary>
    /// ラン中のプレイヤー状態。原作 G のプレイヤー域（§3.2）に対応。
    /// 永続資産（medals/collection）は SaveManager / MetaProgress 側に分離する。
    /// </summary>
    public class PlayerState
    {
        public JobData job;

        // 戦闘ステータス
        public int hp, maxHp;
        public int mp, maxMp;
        public int atk;
        public int def;
        public int shieldDef;     // 盾加算
        public float swordMul = 1f;
        public int swordAtk;
        public int lvl = 1;
        public float defFrac;     // def端数（§3.3）

        // 位置・向き
        public int x, y;
        public int faceX = 1; // 最後に動いた左右方向（スプライト反転用）
        public float animMoveT, animAtkT; // 歩行/攻撃アニメ用の最終時刻（realtime）

        // 手札・デッキ
        public List<string> hand = new List<string>();   // カードid
        public int handMax = 10;
        public List<string> bag = new List<string>();
        public int bagMax = 10;

        // ラン内資産
        public int loot;          // クリアで medals へ統合

        // 装備・恒常フラグ
        public bool goldSword, drain, sleepSword, reviveCharge;
        public bool forbiddenUsed; // 禁忌カードを使ったか（真エンド判定 §10.4）
        public HashSet<string> rings = new HashSet<string>(); // magic/lucky/heal/tonic/bright

        // 状態異常・バフ（type→残量）
        public Dictionary<StatusType, int> status = new Dictionary<StatusType, int>();

        public bool HasRing(string key) => rings.Contains(key);
        public int Status(StatusType t) => status.TryGetValue(t, out var v) ? v : 0;
    }
}
