using UnityEngine;
using MirageGate.Core;

namespace MirageGate.Data
{
    /// <summary>
    /// 状態異常/バフの定義（§5.3）。敵・プレイヤー共用。
    /// 実際の毎ターン処理は StatusEffectManager が type を見て解決する。
    /// </summary>
    [CreateAssetMenu(menuName = "MirageGate/Status Effect", fileName = "Status_")]
    public class StatusEffectData : ScriptableObject
    {
        public StatusType type = StatusType.Poison;
        public string jpName = "毒";
        public string icon = "☠️";

        [Tooltip("既定持続ターン数（barrier/comboは回数）")]
        public int defaultDuration = 8;

        [Tooltip("毎ターンのHP増減（毒=-5, regen=+1 など。0なら無し）")]
        public int hpTickDelta = -5;

        [Header("対象・無効化")]
        public bool affectsEnemies = true;
        public bool affectsPlayer = false;
        [Tooltip("magicimmune役の敵には付与されない")]
        public bool blockedByMagicImmune = true;

        [Header("挙動フラグ")]
        public bool skipsTurn = false;       // sleep/lock：行動不可
        public bool everyOtherTurn = false;  // slow：隔ターン
        public bool randomWander = false;    // panic：うろつき
        public bool convertsToAlly = false;  // charm：味方化
        public bool countsAsHits = false;    // barrier：回数制
    }
}
