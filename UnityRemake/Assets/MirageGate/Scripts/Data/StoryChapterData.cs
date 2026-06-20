using System.Collections.Generic;
using UnityEngine;

namespace MirageGate.Data
{
    /// <summary>
    /// ストーリー1章の定義。原作 STORY / STORY2（§10.3）に対応。
    /// </summary>
    [CreateAssetMenu(menuName = "MirageGate/Story Chapter", fileName = "Chapter_")]
    public class StoryChapterData : ScriptableObject
    {
        public string key = "ch1";
        public string title = "はじまりの願い";
        [Tooltip("1=本編 / 2=探索者試験編")]
        public int campaign = 1;
        [Tooltip("章番号（0始まり）")]
        public int order = 0;

        [Header("舞台")]
        public DungeonData dungeon;
        public MonsterData boss;     // 章ボス（最終Fに配置）

        [Header("会話")]
        public DialogueData intro;   // 章開始
        public DialogueData bossTalk;// ボス前
        public DialogueData outro;   // クリア後

        [Header("特殊")]
        public bool isFinal = false; // 終章（endingChoiceを発火）
        [Tooltip("第1章のように序章会話を後回しにする（_introPending相当, §10.2）")]
        public bool deferIntroUntilClear = false;
        [Tooltip("第1章をチュートリアル(先生)化し、ヒントを発火（監修#3, §11）")]
        public bool enableTutorialHints = false;
    }

    /// <summary>キャンペーン全体（章の並びと固定職）。STORY配列のラッパ。</summary>
    [CreateAssetMenu(menuName = "MirageGate/Campaign", fileName = "Campaign_")]
    public class CampaignData : ScriptableObject
    {
        public int campaign = 1;
        public string title = "本編";
        public JobData fixedJob;                 // 物語は職業固定（レン）
        public List<StoryChapterData> chapters = new List<StoryChapterData>();
        public DialogueData openingPrologue;     // 序章会話（後回し対象）
    }
}
