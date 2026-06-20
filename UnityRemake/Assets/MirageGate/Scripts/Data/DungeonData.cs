using System;
using System.Collections.Generic;
using UnityEngine;

namespace MirageGate.Data
{
    /// <summary>1フロアの敵出現帯。原作 bands[floor] に対応（最後の敵が最強）。</summary>
    [Serializable]
    public class FloorBand
    {
        [Tooltip("このフロアに出現しうるモンスター（弱→強の順）")]
        public List<MonsterData> monsters = new List<MonsterData>();
    }

    /// <summary>
    /// ダンジョンデータ。原作 DUNGEONS（§6.1 全11）に対応。
    /// </summary>
    [CreateAssetMenu(menuName = "MirageGate/Dungeon", fileName = "Dungeon_")]
    public class DungeonData : ScriptableObject
    {
        [Header("識別")]
        public string id = "easy";
        public string jpName = "やさしい谷";
        [TextArea] public string flavor;
        public string textureTheme = "cave"; // tex（描画テーマ）

        [Header("難易度・規模")]
        [Tooltip("難易度。floorFactor/ギミック/障害物に影響（§6.3）")]
        public int star = 1;
        public int floors = 3;
        [Tooltip("マップ一辺。実グリッドは size+4（外枠壁）")]
        public int size = 11;
        public int rooms = 4;
        [Tooltip("敵密度。部屋ごとの敵数 = max(2, round(面積*dense*0.9))")]
        public float dense = 0.10f;
        [Tooltip("追加湧き頻度")]
        public float spawn = 0.04f;

        [Header("経済")]
        public int bet = 5;   // 入場料（メダル）
        public int win = 60;  // クリア基本配当

        [Header("特殊")]
        public bool isTutorial = false;
        public bool hidden = false;          // 幻（25%出現・一度きり消滅）
        public int bagLimit = 0;             // 持ち込み上限（0=既定10。幻は5）
        [Tooltip("究極の門のように30Fへ自動生成するか（§6.5 deepenUltimate）")]
        public bool autoDeepen = false;
        public int autoDeepenFloors = 30;
        [Tooltip("autoDeepen用：難易度順のモンスター配列（スライディングウィンドウ生成）")]
        public List<MonsterData> deepenOrder = new List<MonsterData>();

        [Header("フロア別敵出現帯（autoDeepen時は無視）")]
        public List<FloorBand> bands = new List<FloorBand>();

        [Header("床ギミック出現傾向（§6.4 gimProfile）0..1")]
        public float gimIce = 0.1f;
        public float gimPoison = 0.2f;
        public float gimFire = 0.05f;
        public float gimCurse = 0f;
    }
}
