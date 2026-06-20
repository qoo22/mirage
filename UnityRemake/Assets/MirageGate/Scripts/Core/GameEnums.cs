namespace MirageGate.Core
{
    /// <summary>カードの系統。原作 CARDS.cat に対応（§4.2）。</summary>
    public enum CardCategory
    {
        Heal,   // 回復系（ポーション/MP回復/リジェネ）
        Attack, // 攻撃魔法（単体/範囲/貫通/毒/即死）
        Support,// 補助・制御（状態異常/視界/脱出/湧き停止）
        Buff,   // 自己強化（バリア/透明/連続行動/加速/ステ上昇）
        Equip   // 装備（剣/盾/指輪/ポケット）
    }

    /// <summary>レア度。原作 cardRarity() の cost 自動判定に対応（§4.5）。</summary>
    public enum Rarity
    {
        R,   // cost < 24
        SR,  // 24 <= cost < 44
        SSR, // 44 <= cost < 70
        UR,  // cost >= 70 または forbidden
        LR   // orbcall のみ
    }

    /// <summary>敵のAI役割。原作 MONSTERS.role に対応（§5.2）。</summary>
    public enum MonsterRole
    {
        Melee,        // 隣接時のみ攻撃
        Lunge,        // 1〜2マス飛びかかり
        Charge,       // 直線突進・終端で1.4倍
        Ranged,       // 遠隔射撃（直線・距離<=5）
        Healer,       // 傷ついた味方を回復
        Summon,       // 毎T25%でビー召喚
        Swarm,        // 群れ（基本melee）
        Revive,       // 撃破後に死骸→蘇生
        Poison,       // 移動先に毒床を残す
        Knock,        // 与ダメ0.85倍＋ノックバック
        Coward,       // 距離保持・追われると反撃
        Split,        // 撃破時80%で分裂
        MagicImmune   // 魔法/状態異常を無効
    }

    /// <summary>状態異常・バフ。敵/プレイヤー共通（§5.3）。</summary>
    public enum StatusType
    {
        // 敵・プレイヤー共通のデバフ
        Poison,   // 毎T -5HP
        Lock,     // 行動不可（視界内全敵）
        Sleep,    // 行動不可（単体）
        Slow,     // 隔ターン行動
        Panic,    // ランダムうろつき
        Charm,    // 味方化
        // プレイヤー専用バフ
        Barrier,  // 回数制ダメージ無効
        Invis,    // 敵に無視される
        Combo,    // 連続行動
        Speed,    // 移動2倍
        Regen,    // 歩行毎+1HP
        Bright,   // 視界MAX
        Silent    // フロア湧き停止
    }

    /// <summary>床ギミック（§6.4）。</summary>
    public enum GimmickType
    {
        None,
        Poison, // 歩行1ダメージ
        Fire,   // 歩行2ダメージ（溶岩）
        Curse,  // 歩行2ダメージ＋画面暗転
        Ice     // 同方向に自動スライド
    }

    /// <summary>障害物の種類（§6.4）。star依存で華やかさが変わる。</summary>
    public enum ObstacleType { Rock, Pillar, Mstone, Crystal }

    /// <summary>ゲームモード（§9）。</summary>
    public enum GameMode { Story, FreePlay, World }

    /// <summary>スロット台の種類。掛金倍率 SLOT_BET に対応（§7.5）。</summary>
    public enum SlotMachineType
    {
        Item,    // ×1
        Gambler, // ×2（SPECIAL=禁忌カードあり）
        Monster  // ×3（卵スロット・UR45%）
    }

    /// <summary>カードの対象指定。</summary>
    public enum TargetKind
    {
        Self,       // 自分（バフ/回復）
        SingleEnemy,// 単体敵
        AllVisible, // 視界内全敵（multi）
        Line,       // 直線貫通（spear）
        Floor       // フロア全体（map/silent/bright）
    }
}
