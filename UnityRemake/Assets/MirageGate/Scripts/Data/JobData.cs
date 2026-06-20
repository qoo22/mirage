using UnityEngine;

namespace MirageGate.Data
{
    /// <summary>
    /// 職業データ。原作 JOBS（§3.1）に対応。基本3職（戦士/魔法使い/ギャンブラー）＋隠し職。
    /// </summary>
    [CreateAssetMenu(menuName = "MirageGate/Job", fileName = "Job_")]
    public class JobData : ScriptableObject
    {
        [Header("識別")]
        public string id = "warrior";   // 原作 JOBS のキー
        public string jpName = "戦士";
        public string emoji = "⚔️";
        [TextArea] public string description;
        public bool hidden = false;      // 隠し職（unlockedフラグで解放）

        [Header("初期ステータス")]
        public int hp = 50;
        public int mp = 20;
        public int atk = 12;
        public int def = 6;
        public int sight = 3;

        [Header("特性")]
        [Tooltip("敵 cap[] のどの列を育成上限に使うか（0=物理/1=魔法/2=幸運系）")]
        public int capIndex = 0;
        [Tooltip("ポーション回復倍率")] public float potBonus = 1.0f;
        [Tooltip("魔法ダメージ倍率")] public float magBonus = 1.0f;
        [Tooltip("会心率（0なら職特性なし）")] public float crit = 0f;
        [Tooltip("撃破時30%で配当+pay（ギャンブラー）")] public bool luck = false;
        [Tooltip("MP=HP/2に常時同期（レン）")] public bool mpHalf = false;
    }
}
